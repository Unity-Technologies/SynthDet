using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;
using CustomAttributeNamedArgument = Mono.Cecil.CustomAttributeNamedArgument;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace Unity.Entities.CodeGen
{
    class CapturedVariableDescription
    {
        // In order to have our lambda interact with our DisplayClass as a struct, we need
        // to make sure that all fields exist in the root DisplayClass (since Roslyn will put captured variables
        // in nested DisplayClasses in the case where variables are captured from multiple scopes):
        // https://sharplab.io/#v2:CYLg1APgAgTAjAWAFBQMwAJboMLoN7LpGYZQAs6AsgBQCU6hxBSxr6AlgHYAu6AxgEMADgEF0AXnRwA3Izb5Wc+cS69BQgEIT0MWS2WL9BogBEA9gBUAFlwDm1OuIB8zY8qhwAnNXViw/YQ1aPTciAF9gpVYwqOIlGKMGRLRMCnNrO2oPGHRuG05bWiVXZTzMyMSEsKA
        //
        // We do this by building up the sequence of fields we use to get to our captured variable in
        // CloneClosureExecuteMethodAndItsLocalFunctions.  We then nop those
        // Ldflds (since the field now exists in our root JobStruct) and create the ReadFromDisplayClass
        // and WriteToDisplayClass methods to copy our new field from (and back to if necessary) the original field.
        public FieldReference[] ChainOfFieldsToOldField;
        public FieldReference NewField;
    }

    class JobStructForLambdaJob
    {
        public LambdaJobDescriptionConstruction LambdaJobDescriptionConstruction { get; }
        public TypeDefinition TypeDefinition;
        public MethodDefinition ScheduleTimeInitializeMethod;
        public MethodDefinition RunWithoutJobSystemMethod;
        public FieldDefinition RunWithoutJobSystemDelegateFieldBurst;
        public FieldDefinition RunWithoutJobSystemDelegateFieldNoBurst;
        public FieldDefinition PerformLambdaDelegateField;
        public MethodDefinition PerformLambdaMethod;
        public MethodDefinition ReadFromDisplayClassMethod;
        public MethodDefinition WriteToDisplayClassMethod;
        public MethodDefinition DeallocateOnCompletionMethod;
        public MethodDefinition ExecuteMethod;
        public FieldDefinition SystemInstanceField;
        
        public MethodDefinition[] ClonedMethods;
        public MethodDefinition ClonedLambdaBody => ClonedMethods.First();
        
        public Dictionary<FieldReference, CapturedVariableDescription> CapturedVariables;
        LambdaJobsComponentAccessPatcher _componentAccessPatcher;

        static Type InterfaceTypeFor(LambdaJobDescriptionConstruction lambdaJobDescriptionConstruction)
        {
            switch (lambdaJobDescriptionConstruction.Kind)
            {
                case LambdaJobDescriptionKind.Entities:
                case LambdaJobDescriptionKind.Chunk:
                    if (lambdaJobDescriptionConstruction.WithStructuralChanges)
                        return null;
                    else
                        return typeof(IJobChunk);
                case LambdaJobDescriptionKind.Job:
                    return typeof(IJob);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public static bool IsPermittedMethodToInvokeWithThis(MethodReference method)
        {
            if (!method.DeclaringType.TypeReferenceEquals(typeof(SystemBase)))
                return false;
            if (method.Name == nameof(SystemBase.GetComponent) ||
                method.Name == nameof(SystemBase.SetComponent) ||
                method.Name == nameof(SystemBase.HasComponent))
                return true;
            return false;
        }

        MethodDefinition AddMethod(MethodDefinition method)
        {
            TypeDefinition.Methods.Add(method);
            return method;
        }
        
        FieldDefinition AddField(FieldDefinition field)
        {
            TypeDefinition.Fields.Add(field);
            return field;
        }

        
        public static JobStructForLambdaJob CreateNewJobStruct(LambdaJobDescriptionConstruction lambdaJobDescriptionConstruction)
        {
            return new JobStructForLambdaJob(lambdaJobDescriptionConstruction);
        }
        
        JobStructForLambdaJob(LambdaJobDescriptionConstruction lambdaJobDescriptionConstruction)
        {
            LambdaJobDescriptionConstruction = lambdaJobDescriptionConstruction;
            var containingMethod = LambdaJobDescriptionConstruction.ContainingMethod;

            if (containingMethod.DeclaringType.NestedTypes.Any(t => t.Name == LambdaJobDescriptionConstruction.ClassName))
                UserError.DC0003(LambdaJobDescriptionConstruction.LambdaJobName, containingMethod,LambdaJobDescriptionConstruction.ScheduleOrRunInvocationInstruction).Throw();

            var moduleDefinition = containingMethod.Module;

            var typeAttributes = TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed |
                                 TypeAttributes.AnsiClass | TypeAttributes.SequentialLayout |
                                 TypeAttributes.NestedPrivate;
            
            TypeDefinition = new TypeDefinition(containingMethod.DeclaringType.Namespace, LambdaJobDescriptionConstruction.ClassName, 
                typeAttributes,moduleDefinition.ImportReference(typeof(ValueType)))
            {
                DeclaringType = containingMethod.DeclaringType
            };
            
            TypeDefinition.CustomAttributes.Add(
                new CustomAttribute(AttributeConstructorReferenceFor(typeof(DOTSCompilerGeneratedAttribute), TypeDefinition.Module)));

            _componentAccessPatcher = new LambdaJobsComponentAccessPatcher(TypeDefinition, LambdaJobDescriptionConstruction.MethodLambdaWasEmittedAs.Parameters);
            
            var structInterfaceType = InterfaceTypeFor(LambdaJobDescriptionConstruction);
            if (structInterfaceType != null)
                TypeDefinition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(structInterfaceType)));

            containingMethod.DeclaringType.NestedTypes.Add(TypeDefinition);
            
            if (LambdaJobDescriptionConstruction.LambdaWasEmittedAsInstanceMethodOnContainingType && !LambdaJobDescriptionConstruction.HasAllowedMethodInvokedWithThis)
            {
                //if you capture no locals, but you do use a field/method on the componentsystem, the lambda gets emitted as an instance method on the component system
                //this is inconvenient for us. To make the rest of our code not have to deal with this case, we will emit an OriginalLambda method on our job type, that calls
                //the lambda as it is emitted as an instance method on the component system.  See EntitiesForEachNonCapturingInvokingInstanceMethod test for more details.
                //example:
                //https://sharplab.io/#v2:CYLg1APgAgTAjAWAFBQMwAJboMLoN7LpGYZQAs6AsgJ4CSAdgM4AuAhvQMYCmlXzAFgHtgACgCU+AL6FiMomkwVK4/HOJEAogA8uHAK7MuIlQF4AfFTpM2nHnyGixYgNxrpSdVDgA2Rem26BkZeMOisYnjukkA==
                // Note, we have an exemption for the special case where we want to clone our methods into the JobStruct but we are not capturing.
                // This occurs because we call a method on our declaring type that we will later replace with codegen.

                MakeOriginalLambdaMethodThatRelaysToInstanceMethodOnComponentSystem();
            }
            else
                CloneLambdaMethodAndItsLocalMethods();

            ApplyFieldAttributes();

            var lambdaParameterValueProviderInformations = MakeLambdaParameterValueProviderInformations();

            MakeDeallocateOnCompletionMethod();
            
            if (LambdaJobDescriptionConstruction.WithStructuralChanges)
                AddStructuralChangeMembers(lambdaParameterValueProviderInformations);
            
            ExecuteMethod = MakeExecuteMethod(lambdaParameterValueProviderInformations);

            ScheduleTimeInitializeMethod = AddMethod(MakeScheduleTimeInitializeMethod(lambdaParameterValueProviderInformations));
            
            if (!LambdaJobDescriptionConstruction.WithStructuralChanges)
            {
                AddRunWithoutJobSystemMembers();
                ApplyBurstAttributeIfRequired();
            }
        }

        void MakeDeallocateOnCompletionMethod()
        {
            //we only have to clean up ourselves, in Run execution mode.
            if (LambdaJobDescriptionConstruction.ExecutionMode != ExecutionMode.Run)
                return;

            var fieldsToDeallocate =
                LambdaJobDescriptionConstruction.InvokedConstructionMethods
                    .Where(m => m.MethodName ==
                                nameof(LambdaJobDescriptionConstructionMethods.WithDeallocateOnJobCompletion))
                    .Select(ca => ca.Arguments.Single())
                    .Cast<FieldDefinition>()
                    .ToList();

            if (!fieldsToDeallocate.Any())
                return;

            DeallocateOnCompletionMethod = AddMethod(new MethodDefinition("DeallocateOnCompletionMethod",MethodAttributes.Public, TypeSystem.Void));
            var ilProcessor = DeallocateOnCompletionMethod.Body.GetILProcessor();
            
            foreach (var fieldToDeallocate in fieldsToDeallocate)
            {
                var capturedVariable = CapturedVariables[fieldToDeallocate];
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Ldflda, capturedVariable.NewField);
                
                var disposeReference = new MethodReference("Dispose", TypeSystem.Void, capturedVariable.NewField.FieldType){HasThis = true};
                var disposeMethod = ImportReference(disposeReference);
                //todo: check for null
                
                ilProcessor.Emit(OpCodes.Call, disposeMethod);
            }
            ilProcessor.Emit(OpCodes.Ret);
        }
        
        void AddRunWithoutJobSystemMembers()
        {
            if (LambdaJobDescriptionConstruction.ExecutionMode != ExecutionMode.Run)
                return;

            RunWithoutJobSystemMethod = CreateRunWithoutJobSystemMethod(TypeDefinition);

            RunWithoutJobSystemDelegateFieldNoBurst = AddField(new FieldDefinition("s_RunWithoutJobSystemDelegateFieldNoBurst", FieldAttributes.Static,ImportReference(ExecuteDelegateType)));

            if (LambdaJobDescriptionConstruction.UsesBurst)
                RunWithoutJobSystemDelegateFieldBurst = AddField(new FieldDefinition("s_RunWithoutJobSystemDelegateFieldBurst", FieldAttributes.Static,ImportReference(ExecuteDelegateType)));
        }

        void AddStructuralChangeMembers(LambdaParameterValueInformations providerInformations)
        {
            // Add our PerformLambda method to just do the work of setting up our parameters, calling into the original lambda and doing write-back
            PerformLambdaMethod = CreateStructuralChangesPerformLambdaMethod(providerInformations);
            
            // Perform lambda delegate field so that we can move most of the work in IterateEntities into StructuralChangeEntityProvider.IterateEntities
            PerformLambdaDelegateField = new FieldDefinition("_performLambdaDelegate", FieldAttributes.Public | FieldAttributes.Static, 
                ImportReference(typeof(StructuralChangeEntityProvider.PerformLambdaDelegate)));
            AddField(PerformLambdaDelegateField);

            // Add static constructor to set the above lambda delegate field
            var cctor = new MethodDefinition(".cctor",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, TypeSystem.Void);
            AddMethod(cctor);

            var ilProcessor = cctor.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldnull);
            ilProcessor.Emit(OpCodes.Ldftn, PerformLambdaMethod);
            
            var performLambdaDelegateConstructor =
                TypeDefinition.Module.ImportReference(typeof(StructuralChangeEntityProvider.PerformLambdaDelegate).GetConstructors().First());
            ilProcessor.Emit(OpCodes.Newobj, performLambdaDelegateConstructor);
            ilProcessor.Emit(OpCodes.Stsfld, PerformLambdaDelegateField);
            ilProcessor.Emit(OpCodes.Ret);
        }

        public Type ExecuteDelegateType => LambdaJobDescriptionConstruction.Kind == LambdaJobDescriptionKind.Job 
            ? typeof(InternalCompilerInterface.JobRunWithoutJobSystemDelegate) 
            : typeof(InternalCompilerInterface.JobChunkRunWithoutJobSystemDelegate);

        TypeReference ImportReference(Type t) => TypeDefinition.Module.ImportReference(t);
        TypeReference ImportReference(TypeReference t) => TypeDefinition.Module.ImportReference(t);
        MethodReference ImportReference(MethodReference m) => TypeDefinition.Module.ImportReference(m);
        MethodReference ImportReference(MethodInfo m) => TypeDefinition.Module.ImportReference(m);
        TypeSystem TypeSystem => TypeDefinition.Module.TypeSystem;

        LambdaParameterValueInformations MakeLambdaParameterValueProviderInformations()
        {
            switch (LambdaJobDescriptionConstruction.Kind)
            {
                case LambdaJobDescriptionKind.Entities:
                    return LambdaParameterValueInformations.For(this, LambdaJobDescriptionConstruction.WithStructuralChanges);
                case LambdaJobDescriptionKind.Job:
                    return null;
                case LambdaJobDescriptionKind.Chunk:
                    var allUsedParametersOfEntitiesForEachInvocations = ClonedMethods.SelectMany(
                            m =>
                                m.Body.Instructions.Where(IsChunkEntitiesForEachInvocation).Select(i =>
                                    (m,
                                        LambdaJobDescriptionConstruction.AnalyzeForEachInvocationInstruction(m, i)
                                            .MethodLambdaWasEmittedAs)))
                        .SelectMany(m_and_dem => m_and_dem.MethodLambdaWasEmittedAs.Parameters.Select(p => (m_and_dem.m, p)))
                        .ToArray();
                    return LambdaParameterValueInformations.For(this, false);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        MethodDefinition MakeExecuteMethod(LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            switch (LambdaJobDescriptionConstruction.Kind)
            {
                case LambdaJobDescriptionKind.Entities:
                    if (LambdaJobDescriptionConstruction.WithStructuralChanges)
                        return MakeExecuteMethod_EntitiesWithStructuralChanges(lambdaParameterValueInformations);
                    else
                        return MakeExecuteMethod_Entities(lambdaParameterValueInformations);
                case LambdaJobDescriptionKind.Job:
                    return MakeExecuteMethod_Job();
                case LambdaJobDescriptionKind.Chunk:
                    return MakeExecuteMethod_Chunk(lambdaParameterValueInformations);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void ApplyFieldAttributes()
        {
            //.Run mode doesn't go through the jobsystem, so there's no point in making all these attributes to explain the job system what everything means.
            if (LambdaJobDescriptionConstruction.ExecutionMode == ExecutionMode.Run)
                return;

            var containingMethod = LambdaJobDescriptionConstruction.ContainingMethod;
            foreach (var attribute in EntitiesForEachAttributes.Attributes)
            {
                foreach (var constructionMethod in LambdaJobDescriptionConstruction.InvokedConstructionMethods.Where(m => m.MethodName == attribute.MethodName))
                {
                    if (constructionMethod.Arguments.Single() is FieldDefinition fieldDefinition)
                    {
                        if (!fieldDefinition.DeclaringType.IsDisplayClass())
                            UserError.DC0038(containingMethod, fieldDefinition, constructionMethod).Throw();
                        attribute.CheckAttributeApplicable?.Invoke(containingMethod, constructionMethod, fieldDefinition)?.Throw();
                        
                        if (!CapturedVariables.TryGetValue(fieldDefinition, out var capturedVariable))
                            InternalCompilerError.DCICE007(containingMethod, constructionMethod).Throw();
                        var correspondingJobField = capturedVariable.NewField.Resolve();
                        correspondingJobField.CustomAttributes.Add(new CustomAttribute(TypeDefinition.Module.ImportReference(attribute.AttributeType.GetConstructor(Array.Empty<Type>()))));
                        continue;
                    }

                    UserError.DC0012(containingMethod, constructionMethod).Throw();
                }
            }
        }

        MethodDefinition CreateRunWithoutJobSystemMethod(TypeDefinition newJobStruct)
        {
            var moduleDefinition = newJobStruct.Module;
            var result =
                new MethodDefinition("RunWithoutJobSystem", MethodAttributes.Public | MethodAttributes.Static, moduleDefinition.TypeSystem.Void)
                {
                    HasThis = false,
                    Parameters =
                    {
                        new ParameterDefinition("jobData", ParameterAttributes.None, new PointerType(moduleDefinition.TypeSystem.Void)),
                    },
                };
            newJobStruct.Methods.Add(result);

            var ilProcessor = result.Body.GetILProcessor();
            if (LambdaJobDescriptionConstruction.Kind != LambdaJobDescriptionKind.Job)
            {
                result.Parameters.Insert(0,new ParameterDefinition("archetypeChunkIterator", ParameterAttributes.None,new PointerType(moduleDefinition.ImportReference(typeof(ArchetypeChunkIterator)))));
                ilProcessor.Emit(OpCodes.Ldarg_1);
                ilProcessor.Emit(OpCodes.Call,moduleDefinition.ImportReference(typeof(UnsafeUtilityEx).GetMethod(nameof(UnsafeUtilityEx.AsRef),BindingFlags.Public | BindingFlags.Static)).MakeGenericInstanceMethod(newJobStruct));
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Call,moduleDefinition.ImportReference(typeof(JobChunkExtensions).GetMethod(nameof(JobChunkExtensions.RunWithoutJobs),BindingFlags.Public | BindingFlags.Static)).MakeGenericInstanceMethod(newJobStruct));
                ilProcessor.Emit(OpCodes.Ret);
                return result;
            }
            else
            {
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Call,ExecuteMethod);
                ilProcessor.Emit(OpCodes.Ret);
                return result;    
            }
        }

        void CloneLambdaMethodAndItsLocalMethods()
        {
            var displayClassExecuteMethodAndItsLocalMethods = CecilHelpers.FindUsedInstanceMethodsOnSameType(LambdaJobDescriptionConstruction.MethodLambdaWasEmittedAs).Prepend(LambdaJobDescriptionConstruction.MethodLambdaWasEmittedAs).ToList();

            if (LambdaJobDescriptionConstruction.ExecutionMode != ExecutionMode.Run)
                VerifyClosureFunctionDoesNotWriteToCapturedVariable(displayClassExecuteMethodAndItsLocalMethods);
            
            var (doesMakeStructuralChange, changeMethod, changeInstruction) = DoesClosureFunctionMakeStructuralChanges(displayClassExecuteMethodAndItsLocalMethods); 
            if (!LambdaJobDescriptionConstruction.WithStructuralChanges && doesMakeStructuralChange)
                UserError.DC0027(changeMethod, changeInstruction).Throw();
            
            var (hasNestedLambdaJob, lambdaJobMethod, lambdaJobInstruction) = DoesClosureFunctionHaveNestedLambdaJob(displayClassExecuteMethodAndItsLocalMethods);
            if (hasNestedLambdaJob)
                UserError.DC0029(lambdaJobMethod, lambdaJobInstruction).Throw();

            IEnumerable<Instruction> PermittedCapturingInstructionsGenerator(IEnumerable<MethodDefinition> methodsToClone)
            {
                var instructionsThatPushThisForPermittedMethodCall = new List<Instruction>();
                foreach (var method in methodsToClone)
                {
                    instructionsThatPushThisForPermittedMethodCall.AddRange(
                        method.Body.Instructions.Where(i => i.IsInvocation(out var methodReference) && IsPermittedMethodToInvokeWithThis(methodReference))
                            .Select(i => CecilHelpers.FindInstructionThatPushedArg(method, 0, i)));
                }

                return instructionsThatPushThisForPermittedMethodCall;
            }
            
            (ClonedMethods, CapturedVariables) = 
                CecilHelpers.CloneClosureExecuteMethodAndItsLocalFunctions(displayClassExecuteMethodAndItsLocalMethods, TypeDefinition, "OriginalLambdaBody", 
                    PermittedCapturingInstructionsGenerator);

            _componentAccessPatcher.PatchComponentAccessInstructions(ClonedMethods);

            if (LambdaJobDescriptionConstruction.DelegateProducingSequence.CapturesLocals)
            {
                ReadFromDisplayClassMethod = AddMethodToTransferFieldsWithDisplayClass("ReadFromDisplayClass",TransferDirection.DisplayClassToJob);
                if (LambdaJobDescriptionConstruction.ExecutionMode == ExecutionMode.Run)
                    WriteToDisplayClassMethod = AddMethodToTransferFieldsWithDisplayClass("WriteToDisplayClass", TransferDirection.JobToDisplayClass);
            }

            // Kept around for reference when implementing IJobChunk ForEach 
            //ApplyPostProcessingOnJobCode(clonedMethods, providerInformations);

            VerifyDisplayClassFieldsAreValid();
        }

        void MakeOriginalLambdaMethodThatRelaysToInstanceMethodOnComponentSystem()
        {
            SystemInstanceField = new FieldDefinition("hostInstance", FieldAttributes.Public,
                LambdaJobDescriptionConstruction.ContainingMethod.DeclaringType);
            TypeDefinition.Fields.Add(SystemInstanceField);

            var fakeClonedLambdaBody = new MethodDefinition("OriginalLambdaBody", MethodAttributes.Public, TypeSystem.Void);
            foreach (var p in LambdaJobDescriptionConstruction.MethodLambdaWasEmittedAs.Parameters)
            {
                fakeClonedLambdaBody.Parameters.Add(new ParameterDefinition(p.Name ?? "p" + p.Index, p.Attributes,
                    p.ParameterType));
            }

            var ilProcessor = fakeClonedLambdaBody.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldfld, SystemInstanceField);
            for (int i = 0; i != fakeClonedLambdaBody.Parameters.Count; i++)
                ilProcessor.Emit(OpCodes.Ldarg, i + 1);
            ilProcessor.Emit(OpCodes.Callvirt, LambdaJobDescriptionConstruction.MethodLambdaWasEmittedAs);
            ilProcessor.Emit(OpCodes.Ret);

            TypeDefinition.Methods.Add(fakeClonedLambdaBody);
            ClonedMethods = new[] {fakeClonedLambdaBody};
        }

        void VerifyDisplayClassFieldsAreValid()
        {
            if (LambdaJobDescriptionConstruction.AllowReferenceTypes)
                return;
            
            foreach (var capturedVariable in CapturedVariables.Values)
            {
                var typeDefinition = capturedVariable.NewField.FieldType.Resolve();
                if (typeDefinition.TypeReferenceEquals(LambdaJobDescriptionConstruction.ContainingMethod.DeclaringType))
                {
                    foreach (var method in ClonedMethods)
                    {
                        var thisLoadingInstructions = method.Body.Instructions.Where(i => i.Operand is FieldReference fr && fr.FieldType.TypeReferenceEquals(typeDefinition));

                        foreach (var thisLoadingInstruction in thisLoadingInstructions)
                        {
                            var next = thisLoadingInstruction.Next;
                            if (next.Operand is FieldReference fr)
                                UserError.DC0001(method, next, fr).Throw();
                        }
                    }
                }

                if (typeDefinition.IsDelegate())
                    continue;

                if (!typeDefinition.IsValueType())
                {
                    foreach (var clonedMethod in ClonedMethods)
                    {
                        var methodInvocations = clonedMethod.Body.Instructions.Where(i => i.Operand is MethodReference mr && mr.HasThis);
                        foreach(var methodInvocation in methodInvocations)
                        {
                            var pushThisInstruction = CecilHelpers.FindInstructionThatPushedArg(clonedMethod, 0, methodInvocation);
                            if (pushThisInstruction == null)
                                continue;
                            if (pushThisInstruction.Operand is FieldReference fr && fr.FieldType.TypeReferenceEquals(typeDefinition))
                            {
                                var method = (MethodReference)methodInvocation.Operand;
                                UserError.DC0002(clonedMethod, methodInvocation, method, method.DeclaringType).Throw();
                            }
                        }
                    }

                    UserError.DC0004(LambdaJobDescriptionConstruction.ContainingMethod,LambdaJobDescriptionConstruction.WithCodeInvocationInstruction, capturedVariable.NewField.Resolve()).Throw();
                }
            }
        }
        

        static bool IsChunkEntitiesForEachInvocation(Instruction instruction)
        {
            if (!(instruction.Operand is MethodReference mr))
                return false;
            return mr.Name == nameof(LambdaJobChunkDescriptionConstructionMethods.ForEach) && mr.DeclaringType.Name == nameof(LambdaForEachDescriptionConstructionMethods);
        }

        // Kept around for reference when implementing IJobChunk ForEach
        private void ApplyPostProcessingOnJobCode(MethodDefinition[] methodUsedByLambdaJobs, LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            var forEachInvocations = new List<(MethodDefinition, Instruction)>();
            var methodDefinition = methodUsedByLambdaJobs.First();
            forEachInvocations.AddRange(methodDefinition.Body.Instructions.Where(IsChunkEntitiesForEachInvocation).Select(i => (methodDefinition, i)));

            foreach (var methodUsedByLambdaJob in methodUsedByLambdaJobs)
            {
                var methodBody = methodUsedByLambdaJob.Body;

                var displayClassVariable = methodBody.Variables.SingleOrDefault(v => v.VariableType.Name.Contains("DisplayClass"));
                if (displayClassVariable != null)
                {
                    TypeDefinition displayClass = displayClassVariable.VariableType.Resolve();
                    bool allDelegatesAreGuaranteedNotToOutliveMethod =
                        displayClass.IsValueType() ||
                        CecilHelpers.AllDelegatesAreGuaranteedNotToOutliveMethodFor(methodUsedByLambdaJob);

                    if (!displayClass.IsValueType() && allDelegatesAreGuaranteedNotToOutliveMethod)
                    {
                        CecilHelpers.PatchMethodThatUsedDisplayClassToTreatItAsAStruct(methodBody, displayClassVariable);
                        CecilHelpers.PatchDisplayClassToBeAStruct(displayClass);
                    }
                }
            }

            int counter = 1;
            foreach (var (methodUsedByLambdaJob, instruction) in forEachInvocations)
            {
                var methodBody = methodUsedByLambdaJob.Body;
                var (ldFtn, newObj) = FindClosureCreatingInstructions(methodBody, instruction);

                var newType = new TypeDefinition("", "InlineEntitiesForEachInvocation" + counter++, TypeAttributes.NestedPublic | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,methodUsedByLambdaJob.Module.ImportReference(typeof(ValueType)))
                {
                    DeclaringType = methodUsedByLambdaJob.DeclaringType
                };
                methodUsedByLambdaJob.DeclaringType.NestedTypes.Add(newType);

                CloneLambdaMethodAndItsLocalMethods();

                var iterateEntitiesMethod = CreateIterateEntitiesMethod(lambdaParameterValueInformations);

                var variable = new VariableDefinition(newType);
                methodBody.Variables.Add(variable);
                methodBody.InitLocals = true; // initlocals must be set for verifiable methods with one or more local variables

                InstructionExtensions.MakeNOP(ldFtn.Previous);
                InstructionExtensions.MakeNOP(ldFtn);
                newObj.OpCode = OpCodes.Ldnull;
                newObj.Operand = null;

                var displayClassVariable = methodBody.Variables.SingleOrDefault(v => v.VariableType.Name.Contains("DisplayClass"));
                if (displayClassVariable == null)
                    continue;
                var ilProcessor = methodBody.GetILProcessor();

                ilProcessor.InsertAfter(instruction, new List<Instruction>
                {
                    //no need to drop the delegate from the stack, because we just removed the function that placed it on the stack in the first place.
                    //do not drop the description from the stack, as the original method returns it, and we want to maintain stack behaviour.

                    //call our new method
                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Initobj, newType),

                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Ldloca, displayClassVariable),
                    Instruction.Create(OpCodes.Call, ReadFromDisplayClassMethod),

                    Instruction.Create(OpCodes.Ldloca, variable),
                    Instruction.Create(OpCodes.Ldarga, methodBody.Method.Parameters.First(p=>p.ParameterType.Name == nameof(ArchetypeChunk))),
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldfld, lambdaParameterValueInformations._runtimesField),
                    Instruction.Create(OpCodes.Call, (MethodReference) iterateEntitiesMethod),
                });

#if ENABLE_DOTS_COMPILER_CHUNKS
                var chunkEntitiesInvocation = LambdaJobDescriptionConstruction.FindInstructionThatPushedArg(methodBody.Method, 0, instruction);
                if (chunkEntitiesInvocation.Operand is MethodReference mr && mr.Name == "get_"+nameof(ArchetypeChunk.Entities) && mr.DeclaringType.Name == nameof(ArchetypeChunk))
                    CecilHelpers.EraseMethodInvocationFromInstructions(ilProcessor, chunkEntitiesInvocation);
#endif

                CecilHelpers.EraseMethodInvocationFromInstructions(ilProcessor, instruction);
            }
        }

        MethodDefinition MakeScheduleTimeInitializeMethod(LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            var scheduleTimeInitializeMethod =
                new MethodDefinition("ScheduleTimeInitialize", MethodAttributes.Public, TypeDefinition.Module.TypeSystem.Void)
                {
                    HasThis = true,
                    Parameters =
                    {
                        new ParameterDefinition("componentSystem", ParameterAttributes.None,LambdaJobDescriptionConstruction.ContainingMethod.DeclaringType),
                    },
                };

            if (ReadFromDisplayClassMethod != null)
                scheduleTimeInitializeMethod.Parameters.Add(ReadFromDisplayClassMethod.Parameters.Last());

            if (lambdaParameterValueInformations != null && !lambdaParameterValueInformations._withStructuralChanges)
                lambdaParameterValueInformations.EmitInvocationToScheduleTimeInitializeIntoJobChunkScheduleTimeInitialize(scheduleTimeInitializeMethod);

            var scheduleIL = scheduleTimeInitializeMethod.Body.GetILProcessor();

            if (ReadFromDisplayClassMethod != null)
            {
                scheduleIL.Emit(OpCodes.Ldarg_0);
                scheduleIL.Emit(OpCodes.Ldarg_2);
                scheduleIL.Emit(OpCodes.Call, ReadFromDisplayClassMethod);
            }

            if (SystemInstanceField != null)
            {
                scheduleIL.Emit(OpCodes.Ldarg_0);
                scheduleIL.Emit(OpCodes.Ldarg_1);
                scheduleIL.Emit(OpCodes.Stfld, SystemInstanceField);
            }

            _componentAccessPatcher.EmitScheduleInitializeOnLoadCode(scheduleIL);

            scheduleIL.Emit(OpCodes.Ret);
            return scheduleTimeInitializeMethod;
        }

        MethodDefinition MakeExecuteMethod_Job()
        {
            var executeMethod = CecilHelpers.AddMethodImplementingInterfaceMethod(TypeDefinition.Module, 
                TypeDefinition, typeof(IJob).GetMethod(nameof(IJob.Execute)));
            
            var executeIL = executeMethod.Body.GetILProcessor();
            executeIL.Emit(OpCodes.Ldarg_0);
            executeIL.Emit(OpCodes.Call, ClonedLambdaBody);
            EmitCallToDeallocateOnCompletion(executeIL);
            executeIL.Emit(OpCodes.Ret);
            return executeMethod;
        }

        MethodDefinition MakeExecuteMethod_Chunk(LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            var executeMethod = CecilHelpers.AddMethodImplementingInterfaceMethod(TypeDefinition.Module, 
                TypeDefinition, typeof(IJobChunk).GetMethod(nameof(IJobChunk.Execute)));

            lambdaParameterValueInformations.EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(executeMethod);

            var executeIL = executeMethod.Body.GetILProcessor();

            executeIL.Emit(OpCodes.Ldarg_0);
            executeIL.Emit(OpCodes.Ldarg_1);
            executeIL.Emit(OpCodes.Ldarg_2);
            executeIL.Emit(OpCodes.Ldarg_3);
            executeIL.Emit(OpCodes.Call, ClonedLambdaBody);
            EmitCallToDeallocateOnCompletion(executeIL);
            executeIL.Emit(OpCodes.Ret);
            return executeMethod;
        }

        MethodDefinition MakeExecuteMethod_Entities(LambdaParameterValueInformations providerInformations)
        {
            var executeMethod = CecilHelpers.AddMethodImplementingInterfaceMethod(TypeDefinition.Module, 
                TypeDefinition, typeof(IJobChunk).GetMethod(nameof(IJobChunk.Execute)));

            providerInformations.EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(executeMethod);

            var ilProcessor = executeMethod.Body.GetILProcessor();

            var iterateOnEntitiesMethod = CreateIterateEntitiesMethod(providerInformations);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldarga,1);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldfld,providerInformations._runtimesField);

            ilProcessor.Emit(OpCodes.Call, iterateOnEntitiesMethod);

            EmitCallToDeallocateOnCompletion(ilProcessor);
            
            ilProcessor.Emit(OpCodes.Ret);

            return executeMethod;
        }

        MethodDefinition MakeExecuteMethod_EntitiesWithStructuralChanges(LambdaParameterValueInformations providerInformations)
        {
            var executeMethod = new MethodDefinition("Execute", MethodAttributes.Public, TypeDefinition.Module.TypeSystem.Void)
            {
                Parameters =
                {
                    new ParameterDefinition("componentSystem", ParameterAttributes.None, TypeDefinition.Module.ImportReference(typeof(ComponentSystemBase))),
                    new ParameterDefinition("query", ParameterAttributes.None, TypeDefinition.Module.ImportReference(typeof(EntityQuery)))
                }
            };
            TypeDefinition.Methods.Add(executeMethod);

            var runtimesVariable = providerInformations.EmitInvocationToPrepareToRunOnEntitiesInIntoJobChunkExecute(executeMethod);

            var ilProcessor = executeMethod.Body.GetILProcessor();
            
            ilProcessor.Emit(OpCodes.Ldloca, runtimesVariable);
            ilProcessor.Emit(OpCodes.Ldflda, providerInformations._entityProviderField);
            
            ilProcessor.Emit(OpCodes.Ldarg_0);
            
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldfld, providerInformations._runtimesField);
            
            ilProcessor.Emit(OpCodes.Ldsfld, PerformLambdaDelegateField);

            ilProcessor.Emit(OpCodes.Call, ImportReference(typeof(StructuralChangeEntityProvider).GetMethod(nameof(StructuralChangeEntityProvider.IterateEntities))));

            ilProcessor.Emit(OpCodes.Ret);

            return executeMethod;
        }

        void EmitCallToDeallocateOnCompletion(ILProcessor ilProcessor)
        {
            if (DeallocateOnCompletionMethod == null)
                return;
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Call, DeallocateOnCompletionMethod);
        }

        private MethodDefinition CreateIterateEntitiesMethod(LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            var iterateEntitiesMethod = new MethodDefinition("IterateEntities", MethodAttributes.Public,TypeSystem.Void)
            {
                Parameters =
                {
                    new ParameterDefinition("chunk", ParameterAttributes.None, new ByReferenceType(ImportReference(typeof(ArchetypeChunk)))),
                    new ParameterDefinition("runtimes", ParameterAttributes.None, new ByReferenceType(lambdaParameterValueInformations.RuntimesType)),
                }
            };
            if (LambdaJobDescriptionConstruction.UsesBurst && LambdaJobDescriptionConstruction.UsesNoAlias)
            {
                // Needed as there is an issue with LLVM version < 10.0 not applying optimizations correctly if the method is inlined.
                // This should be fixed in LLVM 10+ and this can be removed once Burst relies on that version.
                iterateEntitiesMethod.NoInlining = true;
                
                var burstNoAliasAttributeConstructor = TypeDefinition.Module.ImportReference(
                    typeof(NoAliasAttribute).GetConstructors().Single(c=>!c.GetParameters().Any()));
                iterateEntitiesMethod.Parameters[1].CustomAttributes.Add(new CustomAttribute(burstNoAliasAttributeConstructor));
            }

            TypeDefinition.Methods.Add(iterateEntitiesMethod);

            var ilProcessor = iterateEntitiesMethod.Body.GetILProcessor();
            iterateEntitiesMethod.Body.InitLocals = true; // initlocals must be set for verifiable methods with one or more local variables

            var loopTerminator = new VariableDefinition(TypeSystem.Int32);
            iterateEntitiesMethod.Body.Variables.Add(loopTerminator);
            ilProcessor.Emit(OpCodes.Ldarg_1);
            ilProcessor.Emit(OpCodes.Call, ImportReference(typeof(ArchetypeChunk).GetMethod("get_"+nameof(ArchetypeChunk.Count))));
            ilProcessor.Emit(OpCodes.Stloc, loopTerminator);

            var loopCounter = new VariableDefinition(TypeSystem.Int32);
            iterateEntitiesMethod.Body.Variables.Add(loopCounter);
            ilProcessor.Emit(OpCodes.Ldc_I4_0);
            ilProcessor.Emit(OpCodes.Stloc, loopCounter);

            var beginLoopInstruction = Instruction.Create(OpCodes.Ldloc, loopCounter);
            ilProcessor.Append(beginLoopInstruction);
            ilProcessor.Emit(OpCodes.Ldloc, loopTerminator);

            // We use compare less than here so that the loop check is:
            // for (int i = 0; i < count; i++) { /* ... */ }
            // This is important because in C# integers have a defined wrapping behaviour on integer overflow - EG. if the integer
            // is int.MaxValue and you add one to it, it becomes int.MinValue. This means that if you used i != count for the loop
            // end condition, the compiler has to assume that count could have been negative and you are intending to increment
            // the entire integer range and wrap around to negative before doing the check. By using i < count you have
            // effectively told the compiler that within the for loop i can only ever be a positive integer [0..count), and this
            // allows the compiler to output less instructions for the loops counts as a result.
            ilProcessor.Emit(OpCodes.Clt);

            var exitDestination = Instruction.Create(OpCodes.Nop);
            ilProcessor.Emit(OpCodes.Brfalse, exitDestination);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            foreach (var parameterDefinition in ClonedLambdaBody.Parameters)
                lambdaParameterValueInformations.EmitILToLoadValueForParameterOnStack(parameterDefinition, ilProcessor, loopCounter);

            ilProcessor.Emit(OpCodes.Call, ClonedLambdaBody);

            ilProcessor.Emit(OpCodes.Ldloc, loopCounter);
            ilProcessor.Emit(OpCodes.Ldc_I4_1);
            ilProcessor.Emit(OpCodes.Add);
            ilProcessor.Emit(OpCodes.Stloc, loopCounter);

            ilProcessor.Emit(OpCodes.Br, beginLoopInstruction);
            ilProcessor.Append(exitDestination);
            ilProcessor.Emit(OpCodes.Ret);
            return iterateEntitiesMethod;
        }
        
        MethodDefinition CreateStructuralChangesPerformLambdaMethod(LambdaParameterValueInformations lambdaParameterValueInformations)
        {
            var performLambdaMethod = new MethodDefinition("PerformLambda", MethodAttributes.Public | MethodAttributes.Static, TypeSystem.Void)
            {
                Parameters =
                {
                    new ParameterDefinition("jobStructPtr", ParameterAttributes.None, new PointerType(TypeSystem.Void)),
                    new ParameterDefinition("runtimesPtr", ParameterAttributes.None, new PointerType(TypeSystem.Void)),
                    new ParameterDefinition("entity", ParameterAttributes.None, ImportReference(typeof(Entity)))
                }
            };
            
            TypeDefinition.Methods.Add(performLambdaMethod);
            var ilProcessor = performLambdaMethod.Body.GetILProcessor();
            performLambdaMethod.Body.InitLocals = true; // initlocals must be set for verifiable methods with one or more local variables

            void CastPerformLambdaParameterToTypeAndLeaveOnStack(TypeReference castToType, int performLambdaParameterParameterIdx)
            {
                var openAsRefMethod = ImportReference(typeof(UnsafeUtilityEx).GetMethod(nameof(UnsafeUtilityEx.AsRef)));
                var closedAsRefMethod = new GenericInstanceMethod(openAsRefMethod);
                closedAsRefMethod.GenericArguments.Add(castToType);
                ilProcessor.Emit(OpCodes.Ldarg, performLambdaParameterParameterIdx);
                ilProcessor.Emit(OpCodes.Call, closedAsRefMethod);
            }

            var runtimesFieldType = lambdaParameterValueInformations._runtimesField.FieldType.GetElementType();
            var runtimesVariable = new VariableDefinition(new ByReferenceType(runtimesFieldType));
            CastPerformLambdaParameterToTypeAndLeaveOnStack(runtimesFieldType, 1);  // void* -> ref LambdaParameterValueProviders.Runtimes
            performLambdaMethod.Body.Variables.Add(runtimesVariable);
            ilProcessor.Emit(OpCodes.Stloc, runtimesVariable);

            // Pull out copies of parameters from ElementProviders (and early out of this loop iteration if entity == Entity.Null)
            var parameterCopyVariables = new List<VariableDefinition>();
            var parameterVariableAddresses = new Dictionary<VariableDefinition, VariableDefinition>();
            for (var iParameter = 0; iParameter < ClonedLambdaBody.Parameters.Count; iParameter++)
            {
                var parameterDefinition = ClonedLambdaBody.Parameters[iParameter];
                
                var (parameterVariable, addressVariable) =
                    lambdaParameterValueInformations.EmitILToCreateCopyForParameter_WithStructuralChanges(runtimesVariable, parameterDefinition, ilProcessor);
                parameterCopyVariables.Add(parameterVariable);
                if (addressVariable != null)
                    parameterVariableAddresses[parameterVariable] = addressVariable;
            }

            // Call lambda body with copies of parameters
            CastPerformLambdaParameterToTypeAndLeaveOnStack(TypeDefinition, 0);  // void* -> ref OurJobStruct
            int parameterIdx = 0;
            foreach (var parameterVariable in parameterCopyVariables)
            {
                if (!parameterVariable.VariableType.IsByReference && ClonedLambdaBody.Parameters[parameterIdx].ParameterType.IsByReference)
                    ilProcessor.Emit(OpCodes.Ldloca, parameterVariable);
                else
                    ilProcessor.Emit(OpCodes.Ldloc, parameterVariable);
                parameterIdx++;
            }
            ilProcessor.Emit(OpCodes.Call, ClonedLambdaBody);

            // Write-back parameters to ElementProviders for IComponentData
            for (var iParameter = 0; iParameter < ClonedLambdaBody.Parameters.Count; iParameter++)
            {
                var parameterDefinition = ClonedLambdaBody.Parameters[iParameter];

                lambdaParameterValueInformations.EmitILToWriteBackParameter_WithStructuralChanges(
                    runtimesVariable, parameterDefinition, ilProcessor, parameterCopyVariables[iParameter], 
                    parameterVariableAddresses.ContainsKey(parameterCopyVariables[iParameter]) ? parameterVariableAddresses[parameterCopyVariables[iParameter]] : null);
            }

            ilProcessor.Emit(OpCodes.Ret);
            return performLambdaMethod;
        }

        void ApplyBurstAttributeIfRequired()
        {
            if (!LambdaJobDescriptionConstruction.UsesBurst)
                return;
            
            var module = TypeDefinition.Module;
            var burstCompileAttributeConstructor = AttributeConstructorReferenceFor(typeof(BurstCompileAttribute), module);
            var burstCompileAttribute = new CustomAttribute(burstCompileAttributeConstructor);
            var useBurstMethod = LambdaJobDescriptionConstruction.InvokedConstructionMethods.FirstOrDefault(m=>m.MethodName == nameof(LambdaJobDescriptionConstructionMethods.WithBurst));

#if !UNITY_DOTSPLAYER
            // Adding MonoPInvokeCallbackAttribute needed for IL2CPP to work when burst is disabled
            var monoPInvokeCallbackAttributeConstructors = typeof(MonoPInvokeCallbackAttribute).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var monoPInvokeCallbackAttribute = new CustomAttribute(module.ImportReference(monoPInvokeCallbackAttributeConstructors[0]));
            monoPInvokeCallbackAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ImportReference(typeof(Type)), ImportReference(ExecuteDelegateType)));

            // Temporary workaround for DOTSR-1016
            CustomAttributeNamedArgument CustomAttributeNamedArgumentFor(string name, Type type, object value)
            {
                return new CustomAttributeNamedArgument(name,
                    new CustomAttributeArgument(module.ImportReference(type), value));
            }

            if (useBurstMethod != null && useBurstMethod.Arguments.Length == 3)
            {
                burstCompileAttribute.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.FloatMode),typeof(FloatMode), useBurstMethod.Arguments[0]));
                burstCompileAttribute.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.FloatPrecision),typeof(FloatPrecision), useBurstMethod.Arguments[1]));
                burstCompileAttribute.Properties.Add(CustomAttributeNamedArgumentFor(nameof(BurstCompileAttribute.CompileSynchronously),typeof(bool), useBurstMethod.Arguments[2]));
            }
#endif

            TypeDefinition.CustomAttributes.Add(burstCompileAttribute);
            RunWithoutJobSystemMethod?.CustomAttributes.Add(burstCompileAttribute);
#if !UNITY_DOTSPLAYER
            RunWithoutJobSystemMethod?.CustomAttributes.Add(monoPInvokeCallbackAttribute);
#endif

            // Need to make sure Burst knows the jobs struct doesn't alias with any pointer fields.
            if (LambdaJobDescriptionConstruction.UsesNoAlias)
            {
                var burstNoAliasAttribute = new CustomAttribute(TypeDefinition.Module.ImportReference(
                    typeof(NoAliasAttribute).GetConstructors().Single(c=>!c.GetParameters().Any())));
                TypeDefinition.CustomAttributes.Add(burstNoAliasAttribute);
            }
        }


        MethodDefinition AddMethodToTransferFieldsWithDisplayClass(string methodName, TransferDirection direction)
        {
            var method =new MethodDefinition(methodName, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot, TypeDefinition.Module.TypeSystem.Void);

            var displayClassTypeReference = LambdaJobDescriptionConstruction.DisplayClass;
            var parameterType = displayClassTypeReference.IsValueType()
                ? new ByReferenceType(displayClassTypeReference)
                : (TypeReference)displayClassTypeReference;
            method.Parameters.Add(new ParameterDefinition("displayClass", ParameterAttributes.None,parameterType));

            var ilProcessor = method.Body.GetILProcessor();
            foreach (var capturedVariable in CapturedVariables.Values)
            {
                if (direction == TransferDirection.DisplayClassToJob)
                {
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldarg_1);
                    foreach (var oldField in capturedVariable.ChainOfFieldsToOldField)
                        ilProcessor.Emit(OpCodes.Ldfld, oldField); //load field from displayClassVariable
                    ilProcessor.Emit(OpCodes.Stfld, capturedVariable.NewField); //store that value in corresponding field in newJobStruct
                }
                else
                {
                    ilProcessor.Emit(OpCodes.Ldarg_1);
                    for (var iOldField = 0; iOldField < capturedVariable.ChainOfFieldsToOldField.Length - 1; ++iOldField)
                    {
                        ilProcessor.Emit(capturedVariable.ChainOfFieldsToOldField[iOldField].FieldType.IsValueType() ? OpCodes.Ldflda : OpCodes.Ldfld,
                            capturedVariable.ChainOfFieldsToOldField[iOldField]); //load field from displayClassVariable
                    }
                    ilProcessor.Emit(OpCodes.Ldarg_0);
                    ilProcessor.Emit(OpCodes.Ldfld, capturedVariable.NewField); //load field from job
                    ilProcessor.Emit(OpCodes.Stfld, capturedVariable.ChainOfFieldsToOldField.Last()); //store that value in corresponding field in displayclass
                }
            }

            ilProcessor.Emit(OpCodes.Ret);
            AddMethod(method);
            return method;
        }

        static (Instruction, Instruction) FindClosureCreatingInstructions(MethodBody body, Instruction callInstruction)
        {
            body.EnsurePreviousAndNextAreSet();
            var cursor = callInstruction;
            while (cursor != null)
            {
                if ((cursor.OpCode == OpCodes.Ldftn) && cursor.Next?.OpCode == OpCodes.Newobj)
                {
                    return (cursor, cursor.Next);
                }

                cursor = cursor.Previous;
            }

            InternalCompilerError.DCICE002(body.Method, callInstruction).Throw();
            return (null,null);
        }

        private enum TransferDirection
        {
            DisplayClassToJob,
            JobToDisplayClass
        }

        public static MethodReference AttributeConstructorReferenceFor(Type attributeType, ModuleDefinition module)
        {
            return module.ImportReference(attributeType.GetConstructors().Single(c=>!c.GetParameters().Any()));
        }

        private static void VerifyClosureFunctionDoesNotWriteToCapturedVariable(IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                var typeDefinitionFullName = method.DeclaringType.FullName;

                var badInstructions = method.Body.Instructions.Where(i =>
                {
                    if (i.OpCode != OpCodes.Stfld)
                        return false;
                    return ((FieldReference) i.Operand).DeclaringType.FullName == typeDefinitionFullName;
                });

                var first = badInstructions.FirstOrDefault();
                if (first == null)
                    continue;

                UserError.DC0013(((FieldReference) first.Operand), method, first).Throw();
            }
        }
        
        static (bool, MethodDefinition, Instruction) DoesClosureFunctionMakeStructuralChanges(IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                var badInstructions = method.Body.Instructions.Where(i =>
                {
                    if (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
                    {
                        var methodReference = ((MethodReference) i.Operand);
                        if (methodReference.DeclaringType.Name != nameof(EntityManager))
                            return false;
                        
                        var methodDefinition = methodReference.Resolve();
                        if (methodDefinition.HasCustomAttributes &&
                            methodDefinition.CustomAttributes.Any(c => c.AttributeType.Name == "StructuralChangeMethodAttribute"))
                        return true;
                    }

                    return false;
                });

                if (badInstructions.Any())
                    return (true, method, badInstructions.FirstOrDefault());
            }
            
            return (false, default, default);
        }
        
        static (bool, MethodDefinition, Instruction) DoesClosureFunctionHaveNestedLambdaJob(IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                var lambdaJobStatementStartingInstructions = CodeGen.LambdaJobDescriptionConstruction.FindLambdaJobStatementStartingInstructions(method.Body.Instructions);

                if (lambdaJobStatementStartingInstructions.Any())
                    return (true, method, lambdaJobStatementStartingInstructions.FirstOrDefault());
            }
            
            return (false, default, default);
        }
    }
}