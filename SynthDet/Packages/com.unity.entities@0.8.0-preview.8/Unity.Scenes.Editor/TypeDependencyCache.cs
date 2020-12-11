using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using AssetImportContext = UnityEditor.Experimental.AssetImporters.AssetImportContext;
using System.Reflection;
using Unity.Entities.Serialization;
using Unity.Profiling;

namespace Unity.Scenes.Editor
{
    [InitializeOnLoad]
    class TypeDependencyCache
    {
        const string SystemsVersion = "DOTSAllSystemsVersion";

        struct NameAndVersion : IComparable<NameAndVersion>
        {
            public string FullName;
            public string    UserName;
            public int    Version;

            public void Init(Type type, string fullName)
            {
                FullName = fullName;
                var systemVersionAttribute = type.GetCustomAttribute<ConverterVersionAttribute>();
                if (systemVersionAttribute != null)
                {
                    Version = systemVersionAttribute.Version;
                    UserName = systemVersionAttribute.UserName;
                }
            }

            public int CompareTo(NameAndVersion other)
            {
                return FullName.CompareTo(other.FullName);
            }
        }

        static ProfilerMarker kRegisterComponentTypes = new ProfilerMarker("TypeDependencyCache.RegisterComponentTypes");
        static ProfilerMarker kRegisterConversionSystemVersions = new ProfilerMarker("TypeDependencyCache.RegisterConversionSystems");

        static unsafe TypeDependencyCache()
        {
            //TODO: Find a better way to enforce Version 2 compatibility
            bool v2Enabled = (bool)typeof(AssetDatabase).GetMethod("IsV2Enabled", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            if(!v2Enabled)
                throw new System.InvalidOperationException("com.unity.entities requires Asset Pipeline Version 2. Please enable Version 2 in Project Settings / Editor / Asset Pipeline / Mode");

            // Custom dependencies are transmitted to the import worker so dont spent time on registering them
            if (UnityEditor.Experimental.AssetDatabaseExperimental.IsAssetImportWorkerProcess())
                return;

            using(kRegisterComponentTypes.Auto())
                RegisterComponentTypes();
        
            using(kRegisterConversionSystemVersions.Auto())
                RegisterConversionSystems();

            int fileFormatVersion = SerializeUtility.CurrentFileFormatVersion;
            UnityEngine.Hash128 fileFormatHash = default;
            HashUnsafeUtilities.ComputeHash128(&fileFormatVersion, sizeof(int), &fileFormatHash);
            UnityEditor.Experimental.AssetDatabaseExperimental.RegisterCustomDependency("EntityBinaryFileFormatVersion", fileFormatHash);
        }
    
        static void RegisterComponentTypes()
        {
            TypeManager.Initialize();

            UnityEditor.Experimental.AssetDatabaseExperimental.UnregisterCustomDependencyPrefixFilter("DOTSType/");
            int typeCount = TypeManager.GetTypeCount();

            for (int i = 1; i < typeCount; ++i)
            {
                var typeInfo = TypeManager.GetTypeInfo(i);
                var hash = typeInfo.StableTypeHash;
                UnityEditor.Experimental.AssetDatabaseExperimental.RegisterCustomDependency(TypeString(typeInfo.Type),
                    new UnityEngine.Hash128(hash, hash));
            }
        }

        static unsafe void RegisterConversionSystems()
        {
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.GameObjectConversion | WorldSystemFilterFlags.EntitySceneOptimizations);
            var behaviours = TypeCache.GetTypesDerivedFrom<IConvertGameObjectToEntity>();
            var nameAndVersion = new NameAndVersion[systems.Count + behaviours.Count];

            int count = 0;
            // System versions
            for (int i = 0; i != systems.Count; i++)
            {
                var fullName = systems[i].FullName;
                if (fullName == null)
                    continue;

                nameAndVersion[count++].Init(systems[i], fullName);
            }

            // IConvertGameObjectToEntity versions
            for (int i = 0; i != behaviours.Count; i++)
            {
                var fullName = behaviours[i].FullName;
                if (fullName == null)
                    continue;

                nameAndVersion[count++].Init(behaviours[i], fullName);
            }
        
            Array.Sort(nameAndVersion, 0, count);

            UnityEngine.Hash128 hash = default;
            for (int i = 0; i != count; i++)
            {
                var fullName = nameAndVersion[i].FullName;
                fixed (char* str = fullName)
                {
                    HashUnsafeUtilities.ComputeHash128(str, (ulong)(sizeof(char) * fullName.Length), &hash);
                }
                
                var userName = nameAndVersion[i].UserName;
                if (userName != null)
                {
                    fixed (char* str = userName)
                    {
                        HashUnsafeUtilities.ComputeHash128(str, (ulong)(sizeof(char) * userName.Length), &hash);
                    }
                }
                
                int version = nameAndVersion[i].Version;
                HashUnsafeUtilities.ComputeHash128(&version, sizeof(int), &hash);
            }

            UnityEditor.Experimental.AssetDatabaseExperimental.RegisterCustomDependency(SystemsVersion, hash);
        }

        public static void AddDependency(AssetImportContext ctx, ComponentType type)
        {
            var typeString = TypeString(type.GetManagedType());
            ctx.DependsOnCustomDependency(typeString);
        }

        public static void AddAllSystemsDependency(AssetImportContext ctx)
        {
            ctx.DependsOnCustomDependency(SystemsVersion);
        }
    
        static string TypeString(Type type) => $"DOTSType/{type.FullName}";
    }
}