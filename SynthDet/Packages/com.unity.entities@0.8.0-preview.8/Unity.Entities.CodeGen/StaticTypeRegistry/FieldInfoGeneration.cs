
using System.Linq;
#if UNITY_DOTSPLAYER
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;
using TypeGenInfoList = System.Collections.Generic.List<Unity.Entities.CodeGen.StaticTypeRegistryPostProcessor.TypeGenInfo>;
using SystemList = System.Collections.Generic.List<Mono.Cecil.TypeDefinition>;

namespace Unity.Entities.CodeGen
{
    internal partial class StaticTypeRegistryPostProcessor : EntitiesILPostProcessor
    {
        class FieldInfoData
        {
            public TypeReference BaseType;
            public string FieldPath;
            public TypeReference FieldType;
            public int FieldOffset;
        }

        class FieldInfoDataComparer : IEqualityComparer<FieldInfoData>
        {
            public bool Equals(FieldInfoData lhs, FieldInfoData rhs)
            {
                return lhs.BaseType.FullName.Equals(rhs.BaseType.FullName) && lhs.FieldPath.Equals(rhs.FieldPath);
            }

            public int GetHashCode(FieldInfoData obj)
            {
                return obj.BaseType.GetHashCode() * 347 ^ obj.FieldPath.GetHashCode();
            }
        }

        bool InjectFieldInfo()
        {
            var getFieldInfoFn = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager).GetMethod("GetFieldInfo"));
            var getFieldInfoFnGenericName = getFieldInfoFn.Resolve().FullName;

            var fieldNameInstructionMap = new Dictionary<FieldInfoData, List<(ILProcessor, Instruction)>>(new FieldInfoDataComparer());

            // scan the assembly for calls to FieldInfo's constructor/implicit constructor and
            // replace the call with our own
            foreach (var type in AssemblyDefinition.MainModule.GetAllTypes())
            {
                bool shouldScan = type.CustomAttributes.Any(ca => ca.AttributeType.Name == nameof(GenerateFieldInfoAttribute) && ca.AttributeType.Namespace == "Unity.Entities");
                foreach (var method in type.GetMethods())
                {
                    if (method.Body == null) 
                        continue;

                    if (!shouldScan)
                        shouldScan = method.CustomAttributes.Any(ca => ca.AttributeType.Name == nameof(GenerateFieldInfoAttribute) && ca.AttributeType.Namespace == "Unity.Entities");
                    
                    if(!shouldScan)
                        continue;

                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.OpCode == OpCodes.Call)
                        {
                            var methodFn = instruction.Operand as MethodReference;
                            if (methodFn.Resolve()?.FullName == getFieldInfoFnGenericName)
                            {
                                // We know this call only takes a ldstr op as a parameter, if it isn't then error
                                var ldStringOp = instruction.Previous;
                                if (ldStringOp.OpCode != OpCodes.Ldstr)
                                    throw new ArgumentException($"{method.FullName} is constructing a {typeof(TypeManager.FieldInfo).FullName} but has not provided a string literal as the constructor argument.");

                                var fieldInfoData = new FieldInfoData();
                                fieldInfoData.BaseType = (methodFn as GenericInstanceMethod).GenericArguments[0];
                                fieldInfoData.FieldPath = ldStringOp.Operand as string;
                                if (!fieldNameInstructionMap.ContainsKey(fieldInfoData))
                                    fieldNameInstructionMap.Add(fieldInfoData, new List<(ILProcessor, Instruction)>());

                                // Fetch the ilprocessor, and cache the ldstr instruction and ilprocessor -- we will
                                // use them later to replace the ldstr and call ops with a load to a generated FieldInfo
                                var processor = method.Body.GetILProcessor();
                                fieldNameInstructionMap[fieldInfoData].Add((processor, ldStringOp));
                            }
                        }
                    }
                }
            }

            if (fieldNameInstructionMap.Count == 0)
                return false;

            var fieldInfoRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager.FieldInfo));
            var fieldInfoBaseTypeFieldRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager.FieldInfo).GetField(nameof(TypeManager.FieldInfo.BaseType)));
            var fieldInfoFieldTypeFieldRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager.FieldInfo).GetField(nameof(TypeManager.FieldInfo.FieldType)));
            var fieldInfoFieldOffsetFieldRef = AssemblyDefinition.MainModule.ImportReference(typeof(TypeManager.FieldInfo).GetField(nameof(TypeManager.FieldInfo.FieldOffset)));

            // Adds "static public class CodeGeneratedFieldInfoRegistry" type
            var fieldInfoRegistry = new TypeDefinition("Unity.Entities.CodeGeneratedRegistry", "FieldInfoRegistry", TypeAttributes.Class | TypeAttributes.Public, AssemblyDefinition.MainModule.ImportReference(typeof(object)));
            AssemblyDefinition.MainModule.Types.Add(fieldInfoRegistry);

            // Declares: "static CodeGeneratedFieldInfoRegistry() { }" (i.e. the static ctor / .cctor)
            var fieldInfoRegistryCctorDef = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, AssemblyDefinition.MainModule.ImportReference(typeof(void)));
            fieldInfoRegistry.Methods.Add(fieldInfoRegistryCctorDef);
            fieldInfoRegistry.IsBeforeFieldInit = true;
            fieldInfoRegistryCctorDef.Body.InitLocals = true;
            var scratchFieldInfo = new VariableDefinition(fieldInfoRef);
            fieldInfoRegistryCctorDef.Body.Variables.Add(scratchFieldInfo);

            // We now know what fields we need to provide FieldInfo for, and where we need to inject
            // so now lets produce the FieldInfo for the given strings, generate a static FieldInfo for each unique
            // FieldInfo and then replace the ldstr+call instructions with a nop+ldsfld to our new static Field info
            int i = 0;
            var il = fieldInfoRegistryCctorDef.Body.GetILProcessor();
            foreach (var fieldInfoData in fieldNameInstructionMap.Keys)
            {
                // Determine field offset, field type etc...
                fieldInfoData.FieldOffset = TypeUtils.GetFieldOffsetByFieldPath(fieldInfoData.FieldPath, fieldInfoData.BaseType, kArchBits, out fieldInfoData.FieldType);

                if (fieldInfoData.FieldType == null)
                    throw new ArgumentException($"Could not find the field '{fieldInfoData.FieldPath}' within the type '{fieldInfoData.BaseType.FullName}'. Please double check your TypeManager.GetFieldInfo() field path argument");

                // Create a new FieldInfo field in our registry
                var fieldInfoField = new FieldDefinition("FieldInfo" + i++, Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.InitOnly, fieldInfoRef);
                fieldInfoRegistry.Fields.Add(fieldInfoField);

                // Initialize the new field in our cctor

                // BaseType = typeof(fieldInfoData.BaseType)
                il.Emit(OpCodes.Ldloca, scratchFieldInfo);
                il.Emit(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(fieldInfoData.BaseType));
                il.Emit(OpCodes.Call, m_GetTypeFromHandleFnRef);
                il.Emit(OpCodes.Stfld, fieldInfoBaseTypeFieldRef);

                // FieldType = typeof(fieldInfoData.FieldType)
                il.Emit(OpCodes.Ldloca, scratchFieldInfo);
                il.Emit(OpCodes.Ldtoken, AssemblyDefinition.MainModule.ImportReference(fieldInfoData.FieldType));
                il.Emit(OpCodes.Call, m_GetTypeFromHandleFnRef);
                il.Emit(OpCodes.Stfld, fieldInfoFieldTypeFieldRef);

                // FieldOffset = fieldInfoData.FieldOffset
                il.Emit(OpCodes.Ldloca, scratchFieldInfo);
                il.Emit(OpCodes.Ldc_I4, fieldInfoData.FieldOffset);
                il.Emit(OpCodes.Stfld, fieldInfoFieldOffsetFieldRef);

                // Store top of stack to static field
                il.Emit(OpCodes.Ldloc, scratchFieldInfo);
                il.Emit(OpCodes.Stsfld, fieldInfoField);

                // Now replace ldstr with a nop and call to a load from our new field
                foreach (var (methodProcessor, ldstrOp) in fieldNameInstructionMap[fieldInfoData])
                {
                    var module = methodProcessor.Body.Method.Module;
                    var callOp = ldstrOp.Next;

                    methodProcessor.Replace(callOp, Instruction.Create(OpCodes.Nop));
                    methodProcessor.Replace(ldstrOp, Instruction.Create(OpCodes.Ldsfld, module.ImportReference(fieldInfoField)));
                }
            }

            il.Emit(OpCodes.Ret);

            return true;
        }
    }
}
#endif