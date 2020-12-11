using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.Experimental.AssetImporters;

namespace Unity.Scenes.Editor
{
    static class AssetBundleTypeCache
    {
        static bool s_Initialized = false;

        public static string TypeString(Type type) => $"UnityEngineType/{type.FullName}";

        public static void RegisterMonoScripts()
        {
            if (AssetDatabaseExperimental.IsAssetImportWorkerProcess() || s_Initialized)
                return;
            s_Initialized = true;

            AssetDatabaseExperimental.UnregisterCustomDependencyPrefixFilter("UnityEngineType/");

            var behaviours = TypeCache.GetTypesDerivedFrom<UnityEngine.MonoBehaviour>();
            var scripts = TypeCache.GetTypesDerivedFrom<UnityEngine.ScriptableObject>();

            for (int i = 0; i != behaviours.Count; i++)
            {
                var type = behaviours[i];
                if (type.IsGenericType)
                    continue;
                var hash = TypeHash.CalculateStableTypeHash(type);
                AssetDatabaseExperimental.RegisterCustomDependency(TypeString(type),
                    new UnityEngine.Hash128(hash, hash));
            }

            for (int i = 0; i != scripts.Count; i++)
            {
                var type = scripts[i];
                if (type.IsGenericType)
                    continue;
                var hash = TypeHash.CalculateStableTypeHash(type);
                AssetDatabaseExperimental.RegisterCustomDependency(TypeString(type),
                    new UnityEngine.Hash128(hash, hash));
            }
        }
    }

    [ScriptedImporter(12, "liveLinkBundles")]
    public class LiveLinkBuildImporter : ScriptedImporter
    {
        const int k_CurrentFileFormatVersion = 1;
        const string k_DependenciesExtension = "dependencies";
        public const string k_BundleExtension = "bundle";
        public const string k_ManifestExtension = "manifest";

        const string k_PrefabExtension = ".prefab";
        const string k_SceneExtension = ".unity";

        [Serializable]
        public struct BuildMetaData
        {
            public BlobArray<Hash128> Dependencies;
        }

        public static Hash128 GetHash(string guid, BuildTarget target, AssetDatabaseExperimental.ImportSyncMode syncMode)
        {
            LiveLinkBuildPipeline.RemapBuildInAssetGuid(ref guid);
            AssetBundleTypeCache.RegisterMonoScripts();

            // TODO: GetArtifactHash needs to take BuildTarget so we can get Artifacts for other than the ActiveBuildTarget
            return AssetDatabaseExperimental.GetArtifactHash(guid, typeof(LiveLinkBuildImporter), syncMode);
        }

        // Recursive until new SBP APIs land in 2020.1
        public unsafe static Hash128[] GetDependencies(Hash128 artifactHash)
        {
            try
            {
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
                var metaPath = paths.First(o => o.EndsWith(k_DependenciesExtension));

                BlobAssetReference<BuildMetaData> buildMetaData;
                if (!BlobAssetReference<BuildMetaData>.TryRead(metaPath, k_CurrentFileFormatVersion, out buildMetaData))
                    return new Hash128[0];

                Hash128[] guids = buildMetaData.Value.Dependencies.ToArray();
                buildMetaData.Dispose();
                return guids;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Exception thrown getting dependencies for '{artifactHash}'.\n{e.Message}");
            }
            return new Hash128[0];
        }
        
        public static string GetBundlePath(Hash128 artifactHash, GUID guid)
        {
            try
            {
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
                return paths.First(o => o.EndsWith(k_BundleExtension));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Exception thrown getting bundle path for '{guid}'.\n{e.Message}");
            }
            return "";
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (ctx.assetPath.ToLower().EndsWith(k_SceneExtension))
                ImportSceneBundle(ctx);
            else
                ImportAssetBundle(ctx);
        }

        void WriteDependenciesResult(string resultPath, Hash128[] dependencies)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var metaData = ref builder.ConstructRoot<BuildMetaData>();

            builder.Construct(ref metaData.Dependencies, dependencies);
            BlobAssetReference<SceneMetaData>.Write(builder, resultPath, k_CurrentFileFormatVersion);
            builder.Dispose();
        }

        void AddImportDependencies(AssetImportContext ctx, IEnumerable<Hash128> dependencies, IEnumerable<Type> types)
        {
            ctx.DependsOnSourceAsset(ctx.assetPath);
            var extension = Path.GetExtension(ctx.assetPath).ToLower();
            if (extension.EndsWith(k_PrefabExtension) || extension.EndsWith(k_SceneExtension))
            {
                // We care about prefabs as they are baked in at build time and impact the result
                var prefabs = AssetDatabase.GetDependencies(ctx.assetPath).Where(x => x.ToLower().EndsWith(k_PrefabExtension));
                foreach (var prefab in prefabs)
                    ctx.DependsOnSourceAsset(prefab);
            }

            // All dependencies impact the build result until new SBP APIs land in 2020.1
            foreach (var dependency in dependencies)
            {
                var dependencyGuid = new GUID(dependency.ToString());
                // Built in asset bundles can be ignored
                if (dependencyGuid == LiveLinkBuildPipeline.k_UnityEditorResources || dependencyGuid == LiveLinkBuildPipeline.k_UnityBuiltinResources || dependencyGuid == LiveLinkBuildPipeline.k_UnityBuiltinExtraResources)
                    continue;

                if (LiveLinkBuildPipeline.TryRemapBuiltinExtraGuid(ref dependencyGuid, out _))
                    continue;
                
                var path = AssetDatabase.GUIDToAssetPath(dependencyGuid.ToString());
                ctx.DependsOnSourceAsset(path);
            }

            foreach (var type in types)
                ctx.DependsOnCustomDependency(AssetBundleTypeCache.TypeString(type));
        }

        private void ImportAssetBundle(AssetImportContext ctx)
        {
            var assetPath = ctx.assetPath;
            var fileIdent = LiveLinkBuildPipeline.RemapBuildInAssetPath(ref assetPath);
            var guid = new GUID(AssetDatabase.AssetPathToGUID(assetPath));

            var manifest = UnityEngine.ScriptableObject.CreateInstance<AssetObjectManifest>();
            AssetObjectManifestBuilder.BuildManifest(guid, manifest);
            var manifestPath = ctx.GetResultPath(k_ManifestExtension);
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { manifest }, manifestPath, true);

            var bundlePath = ctx.GetResultPath(k_BundleExtension);
            var dependencies = new HashSet<Hash128>();
            var types = new HashSet<Type>();
            LiveLinkBuildPipeline.BuildAssetBundle(manifestPath, guid, bundlePath, ctx.selectedBuildTarget, false, dependencies, types, fileIdent);

            var dependenciesPath = ctx.GetResultPath(k_DependenciesExtension);
            WriteDependenciesResult(dependenciesPath, dependencies.ToArray());

            AddImportDependencies(ctx, dependencies, types);
        }

        private void ImportSceneBundle(AssetImportContext ctx)
        {
            var guid = new GUID(AssetDatabase.AssetPathToGUID(ctx.assetPath));
            var bundlePath = ctx.GetResultPath(k_BundleExtension);
            var dependencies = new HashSet<Hash128>();
            var types = new HashSet<Type>();
            LiveLinkBuildPipeline.BuildSceneBundle(guid, bundlePath, ctx.selectedBuildTarget, false, dependencies, types);

            var dependenciesPath = ctx.GetResultPath(k_DependenciesExtension);
            WriteDependenciesResult(dependenciesPath, dependencies.ToArray());

            AddImportDependencies(ctx, dependencies, types);
        }
    }
}
