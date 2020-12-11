#if UNITY_DOTSPLAYER
using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Unity.Cecil.Awesome;

namespace Unity.Entities.CodeGen
{
    public class TypeHash
    {
        // http://www.isthe.com/chongo/src/fnv/hash_64a.c
        // with basis and prime:
        const ulong kFNV1A64OffsetBasis = 14695981039346656037;
        const ulong kFNV1A64Prime = 1099511628211;

        public static ulong FNV1A64(string text)
        {
            ulong result = kFNV1A64OffsetBasis;
            foreach (var c in text)
            {
                result = kFNV1A64Prime * (result ^ (byte)(c & 255));
                result = kFNV1A64Prime * (result ^ (byte)(c >> 8));
            }
            return result;
        }

        public static ulong FNV1A64(int val)
        {
            ulong result = kFNV1A64OffsetBasis;
            unchecked
            {
                result = (((ulong)(val & 0x000000FF) >>  0) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0x0000FF00) >>  8) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0x00FF0000) >> 16) ^ result) * kFNV1A64Prime;
                result = (((ulong)(val & 0xFF000000) >> 24) ^ result) * kFNV1A64Prime;
            }

            return result;
        }

        public static ulong CombineFNV1A64(ulong hash, params ulong[] values)
        {
            foreach (var value in values)
            {
                hash ^= value;
                hash *= kFNV1A64Prime;
            }

            return hash;
        }

        static string GetSanitizedFullName(TypeReference typeRef)
        {
            string name = typeRef.FullName;

            if (typeRef.IsGenericInstance)
            {
                // Cecil produces a different field name than System.Reflection when a field contains a generic,
                // so we convert the name to System.Reflection's form since it is a more precise string
                var genericField = typeRef as GenericInstanceType;
                name = genericField.FullName.Substring(0, typeRef.FullName.IndexOf('<'));
                name += "[";
                for (int i = 0; i < genericField.GenericArguments.Count; ++i)
                {
                    var genericType = genericField.GenericArguments[i].Resolve();
                    name += "[";
                    name += Assembly.CreateQualifiedName(genericType.Module.Assembly.FullName, genericType.FullName);
                    name += "]";

                    if (i < genericField.GenericArguments.Count - 1)
                        name += ",";
                }
                name += "]";
            }

            // Reflection uses '+' as a delimiter for nested classes whereas cecil uses ECMA notation which uses '/'
            name = name.Replace('/', '+');

            return name;
        }

        public static ulong HashType(TypeReference typeRef, int fieldIndex = 0)
        {
            ulong hash = kFNV1A64OffsetBasis;
            var typeResolver = TypeResolver.For(typeRef);
            var typeDef = typeRef.Resolve();
            foreach (var field in typeDef.Fields)
            {
                if (!field.IsStatic)
                {
                    string fieldName = GetSanitizedFullName(typeResolver.Resolve(field.FieldType));
                    hash = CombineFNV1A64(hash, FNV1A64(fieldName));
                    hash = CombineFNV1A64(hash, FNV1A64(fieldIndex));
                    ++fieldIndex;
                }
            }

            return hash;
        }

        public static ulong HashType(Type type, int fieldIndex = 0)
        {
            ulong hash = kFNV1A64OffsetBasis;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (!field.IsStatic)
                {
                    var fieldName = field.FieldType.FullName;
                    hash = CombineFNV1A64(hash, FNV1A64(fieldName));
                    hash = CombineFNV1A64(hash, FNV1A64(fieldIndex));
                    ++fieldIndex;
                }
            }

            return hash;
        }

        public static ulong HashVersionAttribute(TypeReference typeRef)
        {
            int version = 0;
            var typeDef = typeRef.Resolve();
            if (typeDef.CustomAttributes.Count > 0)
            {
                var versionAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.TypeVersionAttribute));
                if (versionAttribute != null)
                {
                    version = (int)versionAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.Int32)
                        .Value;
                }
            }

            return FNV1A64(version);
        }

        private static ulong HashVersionAttribute(Type type)
        {
            int version = 0;
            if (type.CustomAttributes.Count() > 0)
            {
                var versionAttribute = type.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.TypeVersionAttribute));
                if (versionAttribute != null)
                {
                    version = (int)versionAttribute.ConstructorArguments
                        .First(arg => arg.ArgumentType.Name == "Int32")
                        .Value;
                }
            }

            return FNV1A64(version);
        }

        public static ulong CalculateStableTypeHash(TypeReference typeRef)
        {
            ulong asmNameHash = FNV1A64(Assembly.CreateQualifiedName(typeRef.Module.Assembly.FullName, typeRef.FullName));
            ulong typeHash = HashType(typeRef);
            ulong versionHash = HashVersionAttribute(typeRef);

            return CombineFNV1A64(asmNameHash, typeHash, versionHash);
        }

        // Note: We do not use method overloading for Type/TypeDefinition because overloading the method would force users
        // of the method to require an assembly reference on System.Reflection AND Mono.Cecil
        public static ulong CalculateStableTypeHashRefl(Type type)
        {
            ulong asmNameHash = FNV1A64(type.AssemblyQualifiedName);
            ulong typeHash = HashType(type);
            ulong versionHash = HashVersionAttribute(type);

            return CombineFNV1A64(asmNameHash, typeHash, versionHash);
        }

        public static ulong CalculateMemoryOrdering(TypeReference typeRef)
        {
            if (typeRef == null || typeRef.IsEntityType())
            {
                return 0;
            }

            var typeDef = typeRef.Resolve();
            if (typeDef.CustomAttributes.Count > 0)
            {
                var forcedMemoryOrderAttribute = typeDef.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.ForcedMemoryOrderingAttribute));
                if (forcedMemoryOrderAttribute != null)
                {
                    ulong memoryOrder = (ulong)forcedMemoryOrderAttribute.ConstructorArguments
                        .First(arg => arg.Type.MetadataType == MetadataType.UInt64)
                        .Value;

                    return memoryOrder;
                }
            }

            return CalculateStableTypeHash(typeRef);
        }

        // Note: We do not use method overloading for Type/TypeDefinition because overloading the method would force users
        // of the method to require an assembly reference on System.Reflection AND Mono.Cecil
        public static ulong CalculateMemoryOrderingRefl(Type type)
        {
            if (type == null || type.FullName == "Unity.Entities.Entity")
            {
                return 0;
            }

            if (type.CustomAttributes.Count() > 0)
            {
                var forcedMemoryOrderAttribute = type.CustomAttributes.FirstOrDefault(ca => ca.Constructor.DeclaringType.Name == nameof(TypeManager.ForcedMemoryOrderingAttribute));
                if (forcedMemoryOrderAttribute != null)
                {
                    ulong memoryOrder = (ulong)forcedMemoryOrderAttribute.ConstructorArguments
                        .First(arg => arg.ArgumentType.Name == "UInt64")
                        .Value;

                    return memoryOrder;
                }
            }

            return CalculateStableTypeHashRefl(type);
        }
    }
}
#endif