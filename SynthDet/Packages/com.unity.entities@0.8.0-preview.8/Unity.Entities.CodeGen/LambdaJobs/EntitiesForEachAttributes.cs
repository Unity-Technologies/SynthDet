using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.CompilationPipeline.Common.Diagnostics;

namespace Unity.Entities.CodeGen {
    static class EntitiesForEachAttributes
    {
        public delegate DiagnosticMessage CheckAttributeApplicable(
            MethodDefinition inMethod,
            LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod,
            FieldDefinition argument
        );
        
        public struct AttributeData
        {
            public AttributeData(string methodName, Type attributeType, CheckAttributeApplicable check = null)
            {
                MethodName = methodName;
                AttributeType = attributeType;
                CheckAttributeApplicable = check;
            }
            public Type AttributeType;
            public string MethodName;
            public CheckAttributeApplicable CheckAttributeApplicable;
        } 
        
        public static readonly List<AttributeData> Attributes = new List<AttributeData>
        {
            new AttributeData(nameof(LambdaJobDescriptionConstructionMethods.WithReadOnly), typeof(ReadOnlyAttribute), CheckReadOnly),
            new AttributeData(nameof(LambdaJobDescriptionConstructionMethods.WithDeallocateOnJobCompletion), typeof(DeallocateOnJobCompletionAttribute), CheckDeallocateOnJobCompletion),
            new AttributeData(nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableContainerSafetyRestriction), typeof(NativeDisableContainerSafetyRestrictionAttribute), CheckNativeDisableContainerSafetyRestriction),
            new AttributeData(nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableUnsafePtrRestriction), typeof(NativeDisableUnsafePtrRestrictionAttribute)),
            new AttributeData(nameof(LambdaJobDescriptionConstructionMethods.WithNativeDisableParallelForRestriction), typeof(NativeDisableParallelForRestrictionAttribute), CheckNativeDisableParallelForRestriction),
        };

        static bool IsType(TypeReference typeRef, Type type) => typeRef.Name == type.Name && typeRef.Namespace == type.Namespace;
        static bool HasAttribute(TypeDefinition typeDef, Type attributeType) => typeDef.HasCustomAttributes &&
            typeDef.CustomAttributes.Any(attr => IsType(attr.AttributeType, attributeType));

        static bool HasAttributeOrFieldWithAttribute(this TypeReference type, Type checkAttribute)
        {
            var typeDef = type.CheckedResolve();
            if (HasAttribute(typeDef, checkAttribute))
                return true;

            return typeDef.Fields.Any(f =>
            {
                if (f.IsStatic)
                    return false;
                if (f.FieldType.IsPrimitive)
                    return false;
                if (f.FieldType is GenericParameter)
                    return false;
                if (f.FieldType is PointerType)
                    return false;
                if (f.FieldType is ArrayType)
                    return false;
                return f.FieldType.HasAttributeOrFieldWithAttribute(checkAttribute);
            });
        }
        
        static DiagnosticMessage CheckReadOnly(
            MethodDefinition method,
            LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod,
            FieldDefinition field)
        {
            if (field.FieldType.HasAttributeOrFieldWithAttribute(typeof(NativeContainerAttribute)))
                return null;
            return UserError.DC0034(method, field.Name, field.FieldType, constructionMethod.InstructionInvokingMethod);
        }
        
        static DiagnosticMessage CheckDeallocateOnJobCompletion(
            MethodDefinition method,
            LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod,
            FieldDefinition field)
        {
            if (field.FieldType.HasAttributeOrFieldWithAttribute(typeof(NativeContainerSupportsDeallocateOnJobCompletionAttribute)))
                return null;
            return UserError.DC0035(method, field.Name, field.FieldType, constructionMethod.InstructionInvokingMethod);
        }
        
        static DiagnosticMessage CheckNativeDisableContainerSafetyRestriction(
            MethodDefinition method,
            LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod,
            FieldDefinition field)
        {
            if (field.FieldType.HasAttributeOrFieldWithAttribute(typeof(NativeContainerAttribute)))
                return null;
            return UserError.DC0036(method, field.Name, field.FieldType, constructionMethod.InstructionInvokingMethod);
        }
        
        static DiagnosticMessage CheckNativeDisableParallelForRestriction(
            MethodDefinition method,
            LambdaJobDescriptionConstruction.InvokedConstructionMethod constructionMethod,
            FieldDefinition field)
        {
            if (field.FieldType.HasAttributeOrFieldWithAttribute(typeof(NativeContainerAttribute)))
                return null;
            return UserError.DC0037(method, field.Name, field.FieldType, constructionMethod.InstructionInvokingMethod);
        }
    }
}
