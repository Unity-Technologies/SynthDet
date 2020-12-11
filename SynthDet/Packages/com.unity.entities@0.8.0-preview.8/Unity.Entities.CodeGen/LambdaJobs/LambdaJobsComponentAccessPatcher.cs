using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace Unity.Entities.CodeGen
{
    class LambdaJobsComponentAccessPatcher
    {
        TypeDefinition TypeDefinition { get; }
        Dictionary<string, FieldDefinition> ComponentDataFromEntityFields { get; } = new Dictionary<string, FieldDefinition>();
        Collection<ParameterDefinition> LambdaParameters { get; }

        public LambdaJobsComponentAccessPatcher(TypeDefinition typeDefinition, Collection<ParameterDefinition> lambdaParameters)
        {
            TypeDefinition = typeDefinition;
            LambdaParameters = lambdaParameters;
        }

        // Walk through all instructions that call SystemBase.HasComponent/GetComponent/SetComponent
        // and change to direct ComponentDataFromEntity access instead.
        // https://sharplab.io/#v2:EYLgxg9gTgpgtADwGwBYA0AXEBLANmgExAGoAfAAQCYBGAWAChyBmAAipYGEWBvBl/tqwDOGKAFcwGFgFEAdhmwYAnjwC+fAcxbZ5MKADMAhmBgsAkhwgBbAA4RZMeQBFDGQ2o38tY2UMP7TEXFJTms7B2dXQwAxKGs5BWUAHgAVAD4WAHcACz1TFJYQFiCJDDRzS1t7RwwXN08eBoFBFgKMbOwhAG0ExRUavoBdJoFeemaJgQBzGAwRye4WecnNAHYWB0zWgAoASgBuZeb1cZWT44aGrRKQgFklOvciizDqyLcPU/4r1krwmsesXi8j6qQyAHFZn83rUokCrL1kultsAIBBcNohAAlGCGAgAeVkuBUAF4WEZcEIYLsjgIcnlWoViqJSuUXlUIrD6l9GjzmuR1ptQhyAXC4giQUi0ntDjzzgIGgVIRhoZywdtEf1JUpdllcrBGUUbmUKq9OY8GotlgKNjAtikZUs5ZceQ1VaK3PDNUl7o8MlYHlEAIJgExCbG4glEpSy5o/NgoFi3AD6HEMNgwYigOime15KxYAceIbDEbxhOJLDJyvd7xi4u9vqi0opVIOy1R6JYBAgABlDFZgAR3GSWTBYytlk4+wOh4ZthrtSwBspdSS0ssxgXmth9Cxtj3+4PhzS+Sst9vJgB6K+tLOyVodIQAOlftMmADdDFBu1FKFWWBrM0ah9QM3GlFcdVld8Jhvcx5AgFh2k6GDmhvL8f2HD4ySLYNQxgcMcXLaMukgwYJ0veVJlUdtnVdHktHIRMUwAOXsNMMyzHM8wvSZcLcEsCLLKNK2rKFgLrL1tVAv1tlbakKImKcZ2PedF0SLUNLXDcz34XjtzglJ70fTpX2fGCMN/Nx/zElUJIwGTm22SDaMvb5dNg28zAQpCnxg9DvyskdCzAwxBMIyMKyUUjtXI5YaMUqinX5VgmJYacjznbZyGoSgkk1DJjAUexT2afTmk1ZclzJIVNRlZYiuwexnO1VyBBOVQgA==
        public void PatchComponentAccessInstructions(MethodDefinition[] clonedMethods)
        {
            foreach (var method in clonedMethods)
            {
                foreach (var instruction in method.Body.Instructions.ToArray())
                {
                    if (instruction.IsInvocation(out var methodReference) && methodReference.DeclaringType.TypeReferenceEquals(typeof(SystemBase)))
                    {
                        if (PatchableMethod.For(methodReference, out var patchableMethod))
                            PatchInstructionToComponentAccessMethod(method, instruction, patchableMethod);
                    }
                }
            }
        }
        
        public void EmitScheduleInitializeOnLoadCode(ILProcessor scheduleIL)
        {
            // We have ComponentDataFromEntityFields, generate IL in the ScheduleTimeInitialize method to set them up
            foreach (var field in ComponentDataFromEntityFields.Values)
            {
                scheduleIL.Emit(OpCodes.Ldarg_0); // this
                scheduleIL.Emit(OpCodes.Ldarg_1); // System
                scheduleIL.Emit(field.HasReadOnlyAttribute() ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); // True/False

                var fieldComponentType = ((GenericInstanceType) (field.FieldType)).GenericArguments.First();
                var methodInfo = typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetComponentDataFromEntity));
                var getComponentDataFromEntityMethod = TypeDefinition.Module.ImportReference(methodInfo);
                var genericInstanceMethod = getComponentDataFromEntityMethod.MakeGenericInstanceMethod(fieldComponentType);

                scheduleIL.Emit(OpCodes.Call, genericInstanceMethod);
                scheduleIL.Emit(OpCodes.Stfld, field);
            }
        }
        
        // Gets or created a field definition for a type as needed.
        // This will first check if a RW one is available, if that is the case we should use that.
        // If not it will check to see if a RO one is available, use that and promote to RW if needed.
        // Finally, if we don't have one at all, let's create one with the appropriate access rights
        FieldDefinition GetOrCreateComponentDataFromEntityField(TypeReference type, bool asReadOnly)
        {
            if (ComponentDataFromEntityFields.TryGetValue(type.FullName, out var result))
            {
                if (result.HasReadOnlyAttribute() && !asReadOnly) 
                    result.RemoveReadOnlyAttribute();

                return result;
            }

            var fieldType = TypeDefinition.Module.ImportReference(typeof(ComponentDataFromEntity<>)).MakeGenericInstanceType(type);
            var uniqueCounter = ComponentDataFromEntityFields.Count;
            result = new FieldDefinition($"_ComponentDataFromEntity_{type.Name}_{uniqueCounter}", FieldAttributes.Private, fieldType);
            TypeDefinition.Fields.Add(result);
            result.AddNoAliasAttribute();

            if (asReadOnly) 
                result.AddReadOnlyAttribute();

            ComponentDataFromEntityFields[type.FullName] = result;
            return result;
        }

        void PatchInstructionToComponentAccessMethod(MethodDefinition method, Instruction instruction, PatchableMethod unpatchedMethod)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var componentAccessMethod = (GenericInstanceMethod)instruction.Operand;
            var componentDataType = componentAccessMethod.GenericArguments.First();
            var componentDataFromEntityField = GetOrCreateComponentDataFromEntityField(componentDataType, unpatchedMethod.ReadOnly);
            
            // Make sure our componentDataFromEntityField doesn't give write access to a lambda parameter of the same type
            // or there is a writable lambda parameter that gives access to this type (either could violate aliasing rules).
            foreach (var parameter in LambdaParameters)
            {
                if (parameter.ParameterType.GetElementType().TypeReferenceEquals(componentDataType))
                {
                    if (!unpatchedMethod.ReadOnly)
                        UserError.DC0046(method, componentAccessMethod.Name, componentDataType.Name, instruction).Throw();
                    else if (!parameter.HasCompilerServicesIsReadOnlyAttribute())
                        UserError.DC0047(method, componentAccessMethod.Name, componentDataType.Name, instruction).Throw();
                }
            }

            // Find where we pushed the this argument and make it nop
            // Note: we don't want to do this when our method was inserted into our declaring type (in the case where we aren't capturing).
            var instructionThatPushedThis = CecilHelpers.FindInstructionThatPushedArg(method, 0, instruction, true);
            if (instructionThatPushedThis == null)
                    UserError.DC0045(method, componentAccessMethod.Name, instruction).Throw();
            
            //this instruction is responsible for pushing the systembase 'this' object, that we called GetComponent<T>(Entity e) or its friends on.
            //there are two cases where this instance can come from, depending on how roslyn emitted the code. Either the original system
            //was captured into a <>_this variable in our displayclass.  in this case the IL will look like:
            //
            //ldarg0
            //ldfld <>__this
            //IL to load entity
            //call GetComponent<T>
            //
            //or we got emitted without a displayclass, and our method is on the
            //actual system itself, and in that case the system is just ldarg0:
            //
            //ldarg0
            //IL to load entity
            //call GetComponent<T>
            
            //the output IL that we want looks like this:
            //ldarg0
            //ldfld componentdatafromentity
            //IL to load entity
            //call componentdatafromentity.getcomponent<t>(entity e);
            //
            //so the changes we are going to do is remove that original ldfld if it existed, and add the ldfld for our componentdatafromentity
            //and then patch the callsite target.

            if (instructionThatPushedThis.OpCode == OpCodes.Ldfld) 
                instructionThatPushedThis.MakeNOP();

            // Insert Ldflda of componentDataFromEntityField after that point
            var componentDataFromEntityFieldInstruction = CecilHelpers.MakeInstruction(OpCodes.Ldflda, componentDataFromEntityField);
            ilProcessor.InsertAfter(instructionThatPushedThis, componentDataFromEntityFieldInstruction);
            
            // Replace method that we invoke from SystemBase method to ComponentDataFromEntity<T> method (HasComponent, get_Item or set_Item)
            var componentDataFromEntityTypeDef = componentDataFromEntityField.FieldType.Resolve();
            var itemAccessMethod = TypeDefinition.Module.ImportReference(
                componentDataFromEntityTypeDef.Methods.Single(m => m.Name == unpatchedMethod.PatchedMethod));
            var closedGetItemMethod = itemAccessMethod.MakeGenericHostMethod(componentDataFromEntityField.FieldType);
            instruction.Operand = TypeDefinition.Module.ImportReference(closedGetItemMethod);
        }
        
        public struct PatchableMethod
        {
            public string UnpatchedMethod;
            public string PatchedMethod;
            public bool ReadOnly;

            public static bool For(MethodReference invocationTarget, out PatchableMethod result)
            {
                foreach (var candidate in AllPatchableMethods)
                {
                    if (candidate.UnpatchedMethod == invocationTarget.Name)
                    {
                        result = candidate;
                        return true;
                    }
                }

                result = default;
                return false;
            }

            static PatchableMethod[] AllPatchableMethods =
            {
                new PatchableMethod() {UnpatchedMethod = "HasComponent", PatchedMethod = "HasComponent", ReadOnly = true},
                new PatchableMethod() {UnpatchedMethod = "GetComponent", PatchedMethod = "get_Item", ReadOnly = true},
                new PatchableMethod() {UnpatchedMethod = "SetComponent", PatchedMethod = "set_Item", ReadOnly = false},
            };
        }
    }
}