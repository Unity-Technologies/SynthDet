#if UNITY_DOTSPLAYER
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeDefinition>;

namespace Unity.Entities.CodeGen
{
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
        public List<bool> GetSystemIsGroupList(List<TypeReference> systems)
        {
            var inGroup = systems.Select(s => s.Resolve().IsChildTypeOf(AssemblyDefinition.MainModule.ImportReference(typeof(ComponentSystemGroup)).Resolve())).ToList();
            return inGroup;
        }

        public static List<string> GetSystemNames(List<TypeReference> systems)
        {
            return systems.Select(s => s.FullName).ToList();
        }
        
         public MethodDefinition InjectGetSystemAttributes(List<TypeReference> systems)
        {
            var createSystemsFunction = new MethodDefinition(
                "GetSystemAttributes",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(Attribute).MakeArrayType()));

            createSystemsFunction.Parameters.Add(
                new ParameterDefinition("systemType",
                ParameterAttributes.None,
                AssemblyDefinition.MainModule.ImportReference(typeof(Type))));

            createSystemsFunction.Body.InitLocals = true;
            createSystemsFunction.Body.SimplifyMacros();

            var bc = createSystemsFunction.Body.Instructions;

            var allGroups = new string[]
            {
                typeof(UpdateBeforeAttribute).FullName,
                typeof(UpdateAfterAttribute).FullName,
                typeof(UpdateInGroupAttribute).FullName,
                typeof(AlwaysUpdateSystemAttribute).FullName,
                typeof(AlwaysSynchronizeSystemAttribute).FullName,
                typeof(DisableAutoCreationAttribute).FullName
            };

            foreach (var sysRef in systems)
            {
                var sysDef = sysRef.Resolve();
                bc.Add(Instruction.Create(OpCodes.Ldarg_0));
                bc.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(sysRef)));
                bc.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                // Stack: argtype Type
                bc.Add(Instruction.Create(OpCodes.Ceq));
                // Stack: bool
                int branchToNext = bc.Count;
                bc.Add(Instruction.Create(OpCodes.Nop));    // will be: Brfalse_S nextTestCase

                // Stack: <null>
                List<CustomAttribute> attrList = new List<CustomAttribute>();
                foreach (var g in allGroups)
                {
                    var list = sysDef.CustomAttributes.Where(t => t.AttributeType.FullName == g);
                    attrList.AddRange(list);
                }

                var disableAutoCreationAttr = sysRef.Module.Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.Name == nameof(DisableAutoCreationAttribute));
                if (disableAutoCreationAttr != null)
                    attrList.Add(disableAutoCreationAttr);

                int arrayLen = attrList.Count;
                bc.Add(Instruction.Create(OpCodes.Ldc_I4, arrayLen));
                // Stack: arrayLen
                bc.Add(Instruction.Create(OpCodes.Newarr, AssemblyDefinition.MainModule.ImportReference(typeof(Attribute))));
                // Stack: array[]

                for (int i = 0; i < attrList.Count; ++i)
                {
                    var attr = attrList[i];

                    // The stelem.ref will gobble up the array ref we need to return, so dupe it.
                    bc.Add(Instruction.Create(OpCodes.Dup));
                    bc.Add(Instruction.Create(OpCodes.Ldc_I4, i));       // the index we will write
                    // Stack: array[] array[] array-index

                    // If it has a parameter, then load the Type that is the only param to the constructor.
                    if (attr.HasConstructorArguments)
                    {
                        if (attr.ConstructorArguments.Count > 1)
                            throw new InvalidProgramException("Attribute with more than one argument.");

                        var arg = attr.ConstructorArguments[0].Value as TypeReference;
                        bc.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(arg)));
                        bc.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                        
                    }

                    // Stack: array[] array[] array-index type-param OR
                    //        array[] array[] array-index

                    // Construct the attribute; push it on the list.
                    var cctor = AssemblyDefinition.MainModule.ImportReference(attr.Constructor);
                    bc.Add(Instruction.Create(OpCodes.Newobj, cctor));

                    // Stack: array[] array[] array-index value(object)
                    bc.Add(Instruction.Create(OpCodes.Stelem_Ref));
                    // Stack: array[]
                }

                // Stack: array[]
                bc.Add(Instruction.Create(OpCodes.Ret));

                // Put a no-op to start the next test.
                var nextTest = Instruction.Create(OpCodes.Nop);
                bc.Add(nextTest);

                // And go back and patch the IL to jump to the next test no-op just created.
                bc[branchToNext] = Instruction.Create(OpCodes.Brfalse_S, nextTest);
            }
            bc.Add(Instruction.Create(OpCodes.Ldstr, "FATAL: GetSystemAttributes asked to create an unknown Type."));
            var arguementExceptionCtor = AssemblyDefinition.MainModule.ImportReference(typeof(ArgumentException)).Resolve().GetConstructors()
                                            .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
            bc.Add(Instruction.Create(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(arguementExceptionCtor)));
            bc.Add(Instruction.Create(OpCodes.Throw));

            createSystemsFunction.Body.OptimizeMacros();

            return createSystemsFunction;
        }

        public MethodDefinition InjectCreateSystem(List<TypeReference> systems)
        {
            var createSystemsFunction = new MethodDefinition(
                "CreateSystem",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig,
                AssemblyDefinition.MainModule.ImportReference(typeof(object)));

            createSystemsFunction.Parameters.Add(
                new ParameterDefinition("systemType",
                ParameterAttributes.None,
                AssemblyDefinition.MainModule.ImportReference(typeof(Type))));

            createSystemsFunction.Body.InitLocals = true;
            var bc = createSystemsFunction.Body.Instructions;

            foreach (var sysRef in systems)
            {
                var sysDef = sysRef.Resolve();
                var constructor = AssemblyDefinition.MainModule.ImportReference(sysDef.GetConstructors()
                        .FirstOrDefault(param => param.HasParameters == false));

                bc.Add(Instruction.Create(OpCodes.Ldarg_0));
                bc.Add(Instruction.Create(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(sysRef)));
                bc.Add(Instruction.Create(OpCodes.Call, m_GetTypeFromHandleFnRef));
                bc.Add(Instruction.Create(OpCodes.Ceq));
                int branchToNext = bc.Count;
                bc.Add(Instruction.Create(OpCodes.Nop));    // will be: Brfalse_S nextTestCase
                bc.Add(Instruction.Create(OpCodes.Newobj, constructor));
                bc.Add(Instruction.Create(OpCodes.Ret));

                var nextTest = Instruction.Create(OpCodes.Nop);
                bc.Add(nextTest);

                bc[branchToNext] = Instruction.Create(OpCodes.Brfalse_S, nextTest);
            }

            bc.Add(Instruction.Create(OpCodes.Ldstr, "FATAL: CreateSystem asked to create an unknown type. Only subclasses of ComponentSystemBase can be constructed."));
            var argumentExceptionCtor = AssemblyDefinition.MainModule.ImportReference(typeof(ArgumentException)).Resolve().GetConstructors()
                                            .Single(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.MetadataType == MetadataType.String);
            bc.Add(Instruction.Create(OpCodes.Newobj, AssemblyDefinition.MainModule.ImportReference(argumentExceptionCtor)));
            bc.Add(Instruction.Create(OpCodes.Throw));

            return createSystemsFunction;
        }
    }
}
#endif
