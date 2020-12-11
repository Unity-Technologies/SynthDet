using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen
{
    [UsedImplicitly]
    internal class BlobAssetSafetyVerifier : EntitiesILPostProcessor
    {
        private static bool _enable = true;
        
        protected override bool PostProcessImpl()
        {
            if (_enable)
                AssertNoBlobAssetLeavesBlobAssetStorage();
            return false;
        }
        
        void AssertNoBlobAssetLeavesBlobAssetStorage()
        {
            HashSet<TypeReference> _nonRestrictedTypes = new HashSet<TypeReference>();
            
            foreach (var type in AssemblyDefinition.MainModule.GetAllTypes())
            {
                if (!type.HasMethods)
                    continue;
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody)
                        continue;
                    
                    var verifyDiagnosticMessages = VerifyMethod(method, _nonRestrictedTypes);
                    _diagnosticMessages.AddRange(verifyDiagnosticMessages);
                }
            }
        }

        public static List<DiagnosticMessage> VerifyMethod(MethodDefinition method, HashSet<TypeReference> _nonRestrictedTypes)
        {
            var diagnosticMessages = new List<DiagnosticMessage>();
            
            bool IsTypeRestrictedToBlobAssetStorage(TypeReference tr)
            {
                if (tr.IsPrimitive)
                    return false;
                if (tr is GenericParameter)
                    return false;
                if (tr is PointerType)
                    return false;
                if (tr is ArrayType)
                    return false;
                if (tr is RequiredModifierType || tr is GenericInstanceType)
                {
                    tr = tr.GetElementType();
                    return IsTypeRestrictedToBlobAssetStorage(tr);
                }
                if (_nonRestrictedTypes.Contains(tr))
                    return false;

                if (tr.Scope is AssemblyNameReference anr)
                    if (anr.Name == "UnityEngine" || anr.Name == "UnityEditor" || anr.Name == "mscorlib" ||
                        anr.Name == "System.Private.CoreLib")
                        return false;

                // Don't do a CheckedResolve here. If we somehow fail we don't want to block the user.
                var td = tr.Resolve();
                if (td == null)
                {
                    diagnosticMessages.Add(
                        UserError.MakeWarning("ResolveFailureWarning",
                            $"Unable to resolve type {tr.FullName} for verification.",
                            method, method.Body.Instructions.FirstOrDefault()));
                    _nonRestrictedTypes.Add(tr);
                    
                    return false;
                }

                if (td.IsValueType())
                {
                    if (HasMayOnlyLiveInBlobStorageAttribute(td))
                        return true;

                    foreach (var field in td.Fields)
                    {
                        if (field.IsStatic)
                            continue;
                        if (IsTypeRestrictedToBlobAssetStorage(field.FieldType))
                            return true;
                    }
                }

                _nonRestrictedTypes.Add(tr);
                return false;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldfld)
                {
                    var fieldReference = (FieldReference) instruction.Operand;
                    var tr = fieldReference.FieldType;
                    if (IsTypeRestrictedToBlobAssetStorage(tr))
                    {
                        var fancyName = FancyNameFor(fieldReference.FieldType);

                        string error =
                            $"ref {fancyName} yourVariable = ref your{fieldReference.DeclaringType.Name}.{fieldReference.Name}";

                        diagnosticMessages.Add(
                            UserError.MakeError("MayOnlyLiveInBlobStorageViolation",
                                $"You may only access .{fieldReference.Name} by ref, as it may only live in blob storage. try `{error}`",
                                method, instruction));
                    }
                }

                if (instruction.OpCode == OpCodes.Ldobj)
                {
                    var tr = (TypeReference) instruction.Operand;
                    if (IsTypeRestrictedToBlobAssetStorage(tr))
                    {
                        var pushingInstruction = CecilHelpers.FindInstructionThatPushedArg(method, 0, instruction);

                        string error = $"ref {tr.Name} yourVariable = ref ...";
                        if (pushingInstruction.Operand is FieldReference fr)
                        {
                            var typeName = fr.DeclaringType.Name;
                            error = $"ref {tr.Name} yourVariable = ref your{typeName}.{fr.Name}";
                        }

                        diagnosticMessages.Add(
                            UserError.MakeError("MayOnlyLiveInBlobStorageViolation",
                                $"{tr.Name} may only live in blob storage. Access it by ref instead: `{error}`", method,
                                instruction));
                    }
                }
            }

            return diagnosticMessages;
        }

        private static string FancyNameFor(TypeReference typeReference)
        {
            if (typeReference is GenericInstanceType git)
            {
                var sb = new StringBuilder();
                sb.Append(typeReference.Name.Split('`')[0]);
                sb.Append("<");
                bool first = true;
                foreach (var ga in git.GenericArguments)
                {
                    if (!first)
                        sb.Append(",");
                    sb.Append(FancyNameFor(ga));
                    first = false;
                }

                sb.Append(">");
                return sb.ToString();
            }

            return typeReference.Name;
        }

        static bool HasMayOnlyLiveInBlobStorageAttribute(TypeDefinition td)
        {
            if (!td.HasCustomAttributes)
                return false;
            foreach(var ca in td.CustomAttributes)
                if (ca.AttributeType.Name == nameof(MayOnlyLiveInBlobStorageAttribute))
                    return true;
            return false;
        }
    }
}
