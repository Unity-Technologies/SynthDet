using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Collections.LowLevel.Unsafe;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen
{
    static class InternalCompilerError
    {
        public static DiagnosticMessage DCICE001(MethodDefinition method)
        {
            return UserError.MakeError(nameof(DCICE001), "Entities.ForEach Lambda expression uses something from its outer class. This is not supported.", method, null);
        }
        
        public static DiagnosticMessage DCICE002(MethodDefinition method, Instruction instruction)
        {
            return UserError.MakeError(nameof(DCICE002), $"Unable to find LdFtn & NewObj pair preceding call instruction in {method.Name}", method, instruction);
        }
        
        public static DiagnosticMessage DCICE003(MethodDefinition method, Instruction instruction)
        {
            return UserError.MakeError(nameof(DCICE003), $"Discovered multiple assignments of DisplayClass in {method.Name}.  This indicates unexpected IL from Roslyn.", method, instruction);
        }
        
        public static DiagnosticMessage DCICE004(MethodDefinition method, Instruction instruction)
        {
            return UserError.MakeError(nameof(DCICE004), $"Next instruction in initialization of DisplayClass needs to be stloc in {method.Name}", method, instruction);
        }
        
        public static DiagnosticMessage DCICE005(MethodDefinition method, Instruction instruction)
        {
            return UserError.MakeError(nameof(DCICE005), $"Previous instruction needs to be Ldfld or Ldflda while parsing IL to generate lambda for method {method.Name}.", method, instruction);
        }

        public static DiagnosticMessage DCICE006(MethodDefinition method)
        {
            return UserError.MakeError(nameof(DCICE006), $"Next instruction used in constructing the DisplayClass needs to be a store local instruction in {method.Name}", method, null);
        }

        public static DiagnosticMessage DCICE007(MethodDefinition methodToAnalyze, LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod)
        {
            return UserError.MakeError(nameof(DCICE007),$"Could not find field for local captured variable for argument of {constructionMethod.MethodName}.", methodToAnalyze, constructionMethod.InstructionInvokingMethod);
        }
    }
    
    static class UserError
    {
        public static DiagnosticMessage DC0001(MethodDefinition method, Instruction instruction, FieldReference fr)
        {
            return MakeError(nameof(DC0001),$"Entities.ForEach Lambda expression uses field '{fr.Name}'. Either assign the field to a local outside of the lambda expression and use that instead, or use .WithoutBurst() and .Run()", method, instruction);
        }

        public static DiagnosticMessage DC0002(MethodDefinition method, Instruction instruction, MethodReference mr, TypeReference argument)
        {
            return MakeError(nameof(DC0002),$"Entities.ForEach Lambda expression invokes '{mr.Name}' on a {argument.Name} which is a reference type. This is only allowed with .WithoutBurst() and .Run().", method, instruction);
        }
        
        public static DiagnosticMessage DC0003(string name, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0003),$"The name '{name}' is already used in this system.", method, instruction);
        }
        
        public static DiagnosticMessage DC0004(MethodDefinition methodToAnalyze, Instruction illegalInvocation, FieldDefinition field)
        {
            return MakeError(nameof(DC0004),$"Entities.ForEach Lambda expression captures a non-value type '{field.Name}'. This is only allowed with .WithoutBurst() and .Run()", methodToAnalyze, illegalInvocation);
        }
        
        public static DiagnosticMessage DC0005(MethodDefinition method, Instruction instruction, ParameterDefinition parameter)
        {
            return MakeError(nameof(DC0005),$"Entities.ForEach Lambda expression parameter '{parameter.Name}' with type {parameter.ParameterType.FullName} is not supported", method, instruction);
        }

        public static DiagnosticMessage DC0006(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Entities query requires a .{nameof(LambdaForEachDescriptionConstructionMethods.ForEach)} invocation", method, instruction);
        }

        public static DiagnosticMessage DC0017(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Lambda job requires a .{nameof(LambdaSingleJobDescriptionConstructionMethods.WithCode)} invocation", method, instruction);
        }
        
        public static DiagnosticMessage DC0018(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0006),$"Scheduling an Chunk job requires a .{nameof(LambdaJobChunkDescriptionConstructionMethods.ForEach)} invocation", method, instruction);
        }

        public static DiagnosticMessage DC0007(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0007),$"Unexpected code structure in Entities/Job query. Make sure to immediately end each ForEach query with a .Schedule(), .ScheduleParallel() or .Run() call.", method, instruction);
        }

        public static DiagnosticMessage DC0008(MethodDefinition method, Instruction instruction, MethodReference mr)
        {
            return MakeError(nameof(DC0008),$"The argument to {mr.Name} needs to be a literal value.", method, instruction);
        }

        public static DiagnosticMessage DC0009(MethodDefinition method, Instruction instruction, MethodReference mr)
        {
            return MakeError(nameof(DC0009),$"{mr.Name} is only allowed to be called once.", method, instruction);
        }

        public static DiagnosticMessage DC0010(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0010),$"The Entities.ForEach statement contains dynamic code that cannot be statically analyzed.", method, instruction);
        }

        public static DiagnosticMessage DC0011(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0011),$"Every Entities.ForEach statement needs to end with a .Schedule(), .ScheduleParallel() or .Run() invocation.", method, instruction);
        }
        
        public static DiagnosticMessage DC0012(MethodDefinition methodToAnalyze, LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod)
        {
            return MakeError(nameof(DC0012),$"{constructionMethod.MethodName} requires its argument to be a local variable that is captured by the lambda expression.", methodToAnalyze, constructionMethod.InstructionInvokingMethod);
        }
        
        public static DiagnosticMessage DC0013(FieldReference fieldReference, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0013), $"Entities.ForEach Lambda expression writes to captured variable '{fieldReference.Name}'. This is only supported when you use .Run().", method, instruction);
        }

        public static DiagnosticMessage DC0014(MethodDefinition method, Instruction instruction, ParameterDefinition parameter, string[] supportedParameters)
        {
            return MakeError(nameof(DC0014),$"Entities.ForEach Lambda expression parameter '{parameter.Name}' is not a supported parameter. Supported parameter names are {supportedParameters.SeparateByComma()}", method, instruction);
        }
        
        public static DiagnosticMessage DC0015(string noneTypeName, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0015),$"Entities.ForEach will never run because it both requires and excludes {noneTypeName}", method, instruction);
        }
        
        public static DiagnosticMessage DC0016(string noneTypeName, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0016),$"Entities.ForEach lists both WithAny<{noneTypeName}() and WithNone<{noneTypeName}().", method, instruction);
        }

        public static DiagnosticMessage DC0019(MethodDefinition containingMethod, TypeReference sharedComponentType, Instruction instruction)
        {
            return MakeError(nameof(DC0019),$"Entities.ForEach uses ISharedComponentType {sharedComponentType.Name}. This is only supported when using .WithoutBurst() and  .Run()",containingMethod, instruction);
        }
        
        public static DiagnosticMessage DC0020(MethodDefinition containingMethod, TypeReference sharedComponentType,Instruction instruction)
        {
            return MakeError(nameof(DC0020),$"ISharedComponentType {sharedComponentType.Name} can not be received by ref. Use by value or in.",containingMethod, instruction);
        }
        
        public static DiagnosticMessage DC0021(MethodDefinition containingMethod, string parameterName, TypeReference unsupportedType,Instruction instruction)
        {
            return MakeError(nameof(DC0021),$"parameter '{parameterName}' has type {unsupportedType.Name}. This type is not a IComponentData / ISharedComponentData and is therefore not a supported parameter type for Entities.ForEach.",containingMethod, instruction);
        }

        /* This message is no longer valid.  We now support capturing from multiple scopes.
        public static DiagnosticMessage DC0022(MethodDefinition containingMethod, Instruction instruction)
        {
            return MakeError(nameof(DC0022),$"It looks like you're capturing local variables from two different scopes in the method. This is not supported yet.",containingMethod, instruction);
        }
        */
        
        public static DiagnosticMessage DC0023(MethodDefinition containingMethod, TypeReference componentType, Instruction instruction)
        {
            return MakeError(nameof(DC0023),$"Entities.ForEach uses managed IComponentData {componentType.Name}. This is only supported when using .WithoutBurst() and .Run().",containingMethod, instruction);
        }

        public static DiagnosticMessage DC0024(MethodDefinition containingMethod, TypeReference componentType, Instruction instruction)
        {
            return MakeError(nameof(DC0024),$"Entities.ForEach uses managed IComponentData {componentType.Name} by ref. To get write access, receive it without the ref modifier." ,containingMethod, instruction);
        }
        
        // Invalid type used as LambdaJob parameter or in With method invocation.
        public static DiagnosticMessage DC0025(string message, MethodDefinition containingMethod, Instruction instruction)
        {
            return MakeError(nameof(DC0025), message, containingMethod, instruction);
        }
        
        public static DiagnosticMessage DC0026(string allTypeName, MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0026), $"Entities.ForEach lists has WithAll<{allTypeName}>() and a {nameof(LambdaJobQueryConstructionMethods.WithSharedComponentFilter)} method with a parameter of that type.  Remove the redundant WithAll method.", method, instruction);
        }

        //not allowed to implement OnCreateForCompiler
        public static DiagnosticMessage DC0026(string message, MethodDefinition containingMethod)
        {
            return MakeError(nameof(DC0026),message ,containingMethod,null);
        }
        
        public static DiagnosticMessage DC0027(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0027), $"Entities.ForEach Lambda expression makes a structural change. Use an {nameof(EntityCommandBuffer)} to make structural changes or add a .{nameof(LambdaJobDescriptionConstructionMethods.WithStructuralChanges)} invocation to the Entities.ForEach to allow for structural changes.  Note: {nameof(LambdaJobDescriptionConstruction)} is only allowed with .WithoutBurst() and .Run().", method, instruction);
        }

        public static DiagnosticMessage DC0028(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0028), $"Entities.ForEach Lambda expression makes a structural change with a Schedule call. Structural changes are only supported with .Run().", method, instruction);
        }

        public static DiagnosticMessage DC0029(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0029), $"Entities.ForEach Lambda expression has a nested Entities.ForEach Lambda expression.  Only a single Entities.ForEach Lambda expression is currently supported.", method, instruction);
        }

        public static DiagnosticMessage DC0030(TypeReference componentType)
        {
            return MakeError(nameof(DC0030), $"{nameof(GenerateAuthoringComponentAttribute)} is used on managed IComponentData {componentType.Name} without a default constructor. This is not supported.", null, null);
        }

        public static DiagnosticMessage DC0031(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0031), $"Entities.ForEach Lambda expression stores the EntityQuery with a .{nameof(LambdaJobQueryConstructionMethods.WithStoreEntityQueryInField)} invocation but does not store it in a valid field.  Entity Queries can only be stored in fields of the containing JobComponentSystem.", method, instruction);
        }
        
        public static DiagnosticMessage DC0032(TypeReference jobComponentSystemType, MethodDefinition method, Instruction instruction)
        {
            return MakeWarning(nameof(DC0032), $"Entities.ForEach Lambda expression exists in JobComponentSystem {jobComponentSystemType.Name} marked with ExecuteAlways.  This will result in a temporary exception being thrown during compilation, using it is not supported yet.  Please move this code out to a non-jobified ComponentSystem. This will be fixed in upcoming 19.3 releases.", method, instruction);
        }
        
        public static DiagnosticMessage DC0033(MethodDefinition containingMethod, string parameterName, TypeReference unsupportedType,Instruction instruction)
        {
            return MakeError(nameof(DC0033),$"{unsupportedType.Name} implements {nameof(IBufferElementData)} and must be used as DynamicBuffer<{unsupportedType.Name}>. Parameter '{parameterName}' is not a {nameof(IComponentData)} / {nameof(ISharedComponentData)} and is therefore not a supported parameter type for Entities.ForEach.", containingMethod, instruction);
        }

        public static DiagnosticMessage DC0034(MethodDefinition containingMethod, string argumentName, TypeReference unsupportedType, Instruction instruction)
        {
            return MakeError(nameof(DC0034),$"Entities.{nameof(LambdaJobDescriptionConstructionMethods.WithReadOnly)} is called with an argument {argumentName} of unsupported type {unsupportedType}. It can only be called with an argument that is marked with [{nameof(NativeContainerAttribute)}] or a type that has a field marked with [{nameof(NativeContainerAttribute)}].", containingMethod, instruction);
        }

        public static DiagnosticMessage DC0035(MethodDefinition containingMethod, string argumentName, TypeReference unsupportedType, Instruction instruction)
        {
            return MakeError(nameof(DC0035),$"Entities.{nameof(LambdaJobDescriptionConstructionMethods.WithDeallocateOnJobCompletion)} is called with an invalid argument {argumentName} of unsupported type {unsupportedType}. It can only be called with an argument that is marked with [{nameof(NativeContainerSupportsDeallocateOnJobCompletionAttribute)}] or a type that has a field marked with [{nameof(NativeContainerSupportsDeallocateOnJobCompletionAttribute)}].", containingMethod, instruction);
        }

        public static DiagnosticMessage DC0036(MethodDefinition containingMethod, string argumentName, TypeReference unsupportedType, Instruction instruction)
        {
            return MakeError(nameof(DC0036),$"Entities.{nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableContainerSafetyRestriction)} is called with an invalid argument {argumentName} of unsupported type {unsupportedType}. It can only be called with an argument that is marked with [{nameof(NativeContainerAttribute)}] or a type that has a field marked with [{nameof(NativeContainerAttribute)}].", containingMethod, instruction);
        }

        public static DiagnosticMessage DC0037(MethodDefinition containingMethod, string argumentName, TypeReference unsupportedType, Instruction instruction)
        {
            return MakeError(nameof(DC0037),$"Entities.{nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableParallelForRestriction)} is called with an invalid argument {argumentName} of unsupported type {unsupportedType}. It can only be called with an argument that is marked with [{nameof(NativeContainerAttribute)}] or a type that has a field marked with [{nameof(NativeContainerAttribute)}].", containingMethod, instruction);
        }

        public static DiagnosticMessage DC0038(MethodDefinition containingMethod, FieldDefinition field, LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod)
        {
            return MakeError(nameof(DC0038),$"Entities.{constructionMethod.MethodName} is called with an invalid argument {field.DeclaringType.Name}.{field.Name}. You cannot use Entities.{constructionMethod.MethodName} with fields of user-defined types as the argument. Please assign the field to a local variable and use that instead.", containingMethod, constructionMethod.InstructionInvokingMethod);
        }
        
        public static DiagnosticMessage DC0039(TypeReference bufferElementDataType, int numFieldsFound)
        {
            return MakeError(
                nameof(DC0039), 
                messageData: $"Structs implementing IBufferElementData and marked with a {nameof(GenerateAuthoringComponentAttribute)} attribute should have exactly" +
                             $" one field specifying its element type. However, '{bufferElementDataType.Name}' contains {numFieldsFound} fields." +
                             "Please implement your own authoring component.", 
                method: null, 
                instruction: null);
        }
        
        public static DiagnosticMessage DC0040(TypeReference bufferElementDataType)
        {
            return MakeError(
                nameof(DC0040), 
                messageData: "Structs implementing IBufferElementData may only contain fields of either primitive or blittable types. However," +
                             $" '{bufferElementDataType.Name}' has an element type which is NOT a primitive or blittable type.",
                method: null, 
                instruction: null);
        }

        public static DiagnosticMessage DC0041(TypeDefinition bufferElementDataType)
        {
            return MakeError(
                nameof(DC0041),
                messageData: $"IBufferElementData can only be implemented by structs. '{bufferElementDataType.Name}' is a class." +
                             $"Please change {bufferElementDataType} to a struct.",
                method: null,
                instruction: null);
        }

        public static DiagnosticMessage DC0042(TypeDefinition bufferElementDataType)
        {
            return MakeError(
                nameof(DC0042),
                messageData: $"Structs implementing IBufferElementData and marked with a {nameof(GenerateAuthoringComponentAttribute)} attribute cannot have an explicit layout." +
                             $"{bufferElementDataType} has an explicit layout. Please implement your own authoring component.",
                method: null,
                instruction: null);
        }

        public static DiagnosticMessage DC0043(MethodDefinition containingMethod, string name, Instruction instruction)
        {
            return MakeError(nameof(DC0043),$"Entities.{nameof(LambdaJobDescriptionConstructionMethods.WithName)} cannot be used with name '{name}'. The given name must consist of letters, digits, and underscores only, and may not contain two consecutive underscores.", containingMethod, instruction);
        }
        
        public static DiagnosticMessage DC0044(MethodDefinition method, Instruction instruction)
        {
            return MakeError(nameof(DC0044), $"Entities.ForEach can only be used with an inline lambda.  Calling it with a delegate stored in a variable, field, or returned from a method is not supported.", method, instruction);
        }
        
        public static DiagnosticMessage DC0045(MethodDefinition containingMethod, string methodName, Instruction instruction)
        {
            return MakeError(nameof(DC0045),$"Entities.ForEach cannot use {methodName} with branches in the method invocation.", containingMethod, instruction);
        }
        
        public static DiagnosticMessage DC0046(MethodDefinition containingMethod, string methodName, string typeName, Instruction instruction)
        {
            return MakeError(nameof(DC0046),$"Entities.ForEach cannot use component access method {methodName} that needs write access with the same type {typeName} that is used in lambda parameters.", containingMethod, instruction);
        }
        public static DiagnosticMessage DC0047(MethodDefinition containingMethod, string methodName, string typeName, Instruction instruction)
        {
            return MakeError(nameof(DC0047),$"Entities.ForEach cannot use component access method {methodName} with the same type {typeName} that is used in lambda parameters with write access (as ref).", containingMethod, instruction);
        }

        static DiagnosticMessage MakeInternal(DiagnosticType type, string errorCode, string messageData, MethodDefinition method, Instruction instruction)
        {
            var result = new DiagnosticMessage {Column = 0, Line = 0, DiagnosticType = type, File = ""};
            
            var seq = instruction != null ? CecilHelpers.FindBestSequencePointFor(method, instruction) : null;

            if (errorCode.Contains("ICE"))
            {
                messageData = messageData + " Seeing this error indicates a bug in the dots compiler. We'd appreciate a bug report (About->Report a Bug...). Thnx! <3";
            }

            messageData = $"error {errorCode}: {messageData}";
            if (seq != null)
            {
                result.File = seq.Document.Url;
                result.Column = seq.StartColumn;
                result.Line = seq.StartLine;
#if !UNITY_DOTSPLAYER
                result.MessageData = $"{seq.Document.Url}({seq.StartLine},{seq.StartColumn}): {messageData}";
#else
                result.MessageData = messageData;
#endif
            }
            else
            {
                result.MessageData = messageData;
            }
                
            return result;
        }

        public static DiagnosticMessage MakeError(string errorCode, string messageData, MethodDefinition method, Instruction instruction)
        {
            return MakeInternal(DiagnosticType.Error, errorCode, messageData, method, instruction);
        }

        public static DiagnosticMessage MakeWarning(string errorCode, string messageData, MethodDefinition method, Instruction instruction)
        {
            return MakeInternal(DiagnosticType.Warning, errorCode, messageData, method, instruction);
        }
        public static void Throw(this DiagnosticMessage dm)
        {
            throw new FoundErrorInUserCodeException(new[] { dm});
        }
    }
}