using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    class EntityScenesPaths
    {
        public static Type SubSceneImporterType = null;
        
        public enum PathType
        {
            EntitiesUnityObjectReferences,
            EntitiesUnityObjectRefGuids,
            EntitiesUnitObjectReferencesBundle,
            EntitiesBinary,
            EntitiesConversionLog,
            EntitiesHeader
        }

        public static string GetExtension(PathType pathType)
        {
            switch (pathType)
            {
                // these must all be lowercase
                case PathType.EntitiesUnityObjectReferences: return "asset";
                case PathType.EntitiesUnityObjectRefGuids: return "refguids";
                case PathType.EntitiesBinary : return "entities";
                case PathType.EntitiesUnitObjectReferencesBundle: return "bundle";
                case PathType.EntitiesHeader : return "entityheader";
                case PathType.EntitiesConversionLog : return "conversionlog";
            }

            throw new System.ArgumentException("Unknown PathType");
        }
        
#if UNITY_EDITOR

        static Dictionary<Hash128, string> s_HashToString = new Dictionary<Hash128, string>();

        public static Hash128 GetSubSceneArtifactHash(Hash128 sceneGUID, Hash128 buildConfigurationGUID, UnityEditor.Experimental.AssetDatabaseExperimental.ImportSyncMode syncMode)
        {
            var guid = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(sceneGUID, buildConfigurationGUID);
            if (!s_HashToString.TryGetValue(guid, out var guidString))
                guidString = s_HashToString[guid] = guid.ToString();
            return UnityEditor.Experimental.AssetDatabaseExperimental.GetArtifactHash(guidString, SubSceneImporterType, syncMode);
        }        
        
        public static string GetLoadPathFromArtifactPaths(string[] paths, PathType type, int? sectionIndex = null)
        {
            var extension = GetExtension(type);
            if (sectionIndex != null)
                extension = $"{sectionIndex}.{extension}";

            return paths.FirstOrDefault(p => p.EndsWith(extension));
        }
#endif // UNITY_EDITOR

        public static string GetLoadPath(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            return Application.streamingAssetsPath+"/"+RelativePathInStreamingAssetsFolderFor(sceneGUID,type,sectionIndex);
        }

        public static string RelativePathInStreamingAssetsFolderFor(Hash128 sceneGUID, PathType type, int sectionIndex)
        {
            var extension = GetExtension(type);
            switch (type)
            {
                case PathType.EntitiesBinary:
                    return $"SubScenes/{sceneGUID}.{sectionIndex}.{extension}";
                case PathType.EntitiesHeader:
                    return $"SubScenes/{sceneGUID}.{extension}";
                case PathType.EntitiesUnityObjectReferences:
                    return $"SubScenes/{sceneGUID}.{sectionIndex}.bundle";
                default:
                    throw new ArgumentException();
            }
        }
        
        public static string GetLiveLinkCachePath(UnityEngine.Hash128 targetHash, PathType type, int sectionIndex)
        {
            var extension = GetExtension(type);
            switch (type)
            {
                case PathType.EntitiesHeader:
                    return $"{Application.persistentDataPath}/{targetHash}.{extension}";
                case PathType.EntitiesBinary:
                case PathType.EntitiesUnityObjectRefGuids:
                case PathType.EntitiesUnityObjectReferences:
                case PathType.EntitiesUnitObjectReferencesBundle:
                    return $"{Application.persistentDataPath}/{targetHash}.{sectionIndex}.{extension}";
                default:
                    return "";
            }
        }

        public static string ComposeLiveLinkCachePath(string fileName)
        {
            return $"{Application.persistentDataPath}/{fileName}";
        }

        public static int GetSectionIndexFromPath(string path)
        {
            var components = Path.GetFileNameWithoutExtension(path).Split('.');
            if (components.Length == 1)
                return 0;
            return int.Parse(components[1]);
        }
    }
}
