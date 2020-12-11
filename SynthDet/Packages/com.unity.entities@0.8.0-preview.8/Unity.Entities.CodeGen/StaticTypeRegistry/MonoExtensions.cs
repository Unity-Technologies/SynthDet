#if UNITY_DOTSPLAYER
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Unity.Cecil.Awesome;

namespace Unity.Entities.CodeGen
{
    public static class MonoExtensions
    {
        public static bool IsCppBasicType(this TypeDefinition type)
        {
            return type.MetadataType == MetadataType.Boolean || type.MetadataType == MetadataType.Void
                || type.MetadataType == MetadataType.SByte || type.MetadataType == MetadataType.Byte
                || type.MetadataType == MetadataType.Int16 || type.MetadataType == MetadataType.UInt16 || type.MetadataType == MetadataType.Char
                || type.MetadataType == MetadataType.Int32 || type.MetadataType == MetadataType.UInt32
                || type.MetadataType == MetadataType.Int64 || type.MetadataType == MetadataType.UInt64
                || type.MetadataType == MetadataType.Single || type.MetadataType == MetadataType.Double
                || type.MetadataType == MetadataType.IntPtr || type.MetadataType == MetadataType.UIntPtr;
        }

        public static TypeReference DynamicArrayElementType(this TypeReference typeRef)
        {
            var type = typeRef.Resolve();

            if (!type.IsDynamicArray())
                throw new ArgumentException("Expected DynamicArray type reference.");

            GenericInstanceType genericInstance = (GenericInstanceType)typeRef;
            return genericInstance.GenericArguments[0];
        }

        public static TypeDefinition FixedSpecialType(this TypeReference typeRef)
        {
            TypeDefinition type = typeRef.Resolve();
            if (type.MetadataType == MetadataType.IntPtr) return type.Module.TypeSystem.IntPtr.Resolve();
            if (type.MetadataType == MetadataType.Void) return type.Module.TypeSystem.Void.Resolve();
            if (type.MetadataType == MetadataType.String) return type.Module.TypeSystem.String.Resolve();
            if (IsCppBasicType(type)) return type;
            else return null;
        }

        public static bool IsEntityType(this TypeReference typeRef)
        {
            return (typeRef.FullName == "Unity.Entities.Entity");
        }

        public static bool IsManagedType(this TypeReference typeRef)
        {
            // We must check this before calling Resolve() as cecil loses this property otherwise
            if (typeRef.IsPointer)
                return false;

            if (typeRef.IsArray || typeRef.IsGenericParameter)
                return true;

            var type = typeRef.Resolve();

            if (type.IsDynamicArray())
                return true;

            TypeDefinition fixedSpecialType = type.FixedSpecialType();
            if (fixedSpecialType != null)
            {
                if (fixedSpecialType.MetadataType == MetadataType.String)
                    return true;
                return false;
            }

            if (type.IsEnum)
                return false;

            if (type.IsValueType)
            {
                // if none of the above check the type's fields
                var typeResolver = TypeResolver.For(typeRef);
                foreach (var field in type.Fields)
                {
                    if (field.IsStatic)
                        continue;

                    var fieldType = typeResolver.Resolve(field.FieldType);
                    if (fieldType.IsManagedType())
                        return true;
                }

                return false;
            }

            return true;
        }

        public static bool IsComplex(this TypeReference typeRef)
        {
            // We must check this before calling Resolve() as cecil loses this property otherwise
            if (typeRef.IsPointer)
                return false;

            var type = typeRef.Resolve();

            if (TypeUtils.ValueTypeIsComplex[0].ContainsKey(type))
                return TypeUtils.ValueTypeIsComplex[0][type];

            if (type.IsDynamicArray())
                return true;

            TypeDefinition fixedSpecialType = type.FixedSpecialType();
            if (fixedSpecialType != null)
            {
                if (fixedSpecialType.MetadataType == MetadataType.String)
                    return true;
                return false;
            }

            if (type.IsEnum)
                return false;

            TypeUtils.PreprocessTypeFields(type, 0);

            return TypeUtils.ValueTypeIsComplex[0][type];
        }

        public static bool IsDynamicArray(this TypeReference type)
        {
            return type.Name.StartsWith("DynamicArray`");
        }

        public static ulong CalculateStableTypeHash(this TypeReference typeRef)
        {
            return TypeHash.CalculateStableTypeHash(typeRef);
        }

        public static ulong CalculateMemoryOrdering(this TypeReference typeRef)
        {
            return TypeHash.CalculateMemoryOrdering(typeRef);
        }
    }
}
#endif