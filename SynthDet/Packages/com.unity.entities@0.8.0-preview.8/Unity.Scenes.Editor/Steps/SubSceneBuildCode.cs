using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.Build.Internals;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Experimental;
using BuildCompression = UnityEngine.BuildCompression;
using BuildContext = Unity.Build.BuildContext;
using BuildPipeline = UnityEditor.BuildPipeline;

namespace Unity.Scenes.Editor
{
    static class SubSceneBuildCode
    {
        // This function is responsible for providing all the subscenes to the build.
        //
        // The way these files get generated is that we have a SceneWithBuildConfiguration file, (which is a bit of a hack to work around the inability for scriptable importers to take arguments, so
        // instead we create a different file that points to the scene we want to import, and points to the buildconfiguration we want to import it for).   The SubsceneImporter will import this file,
        // and it will make 3 (relevant) kind of files:
        // - headerfile
        // - entitybinaryformat file (the actual entities payloads)
        // - a SerializedFile that has an array of UnityEngine.Object PPtrs that are used by this entity file.
        //
        // The first two we deal with very simply: they just need to be copied into the build, and we're done.
        // the third one, we will feed as input to the Scriptable build pipeline (which is actually about creating assetbundles), and create an assetbundle that
        // has all those objects in it that the 3rd file referred to.  We do this with a batch api, first we loop through all subscenes, and register with this batch
        // api which assetbundles we'd like to see produced, and then at the end, we say "okay make them please".  this assetbundle creation api has a caching system
        // that is separate from the assetpipeline caching system, so if all goes well, the call to produce these assetbundles will return very fast and did nothing.
        //
        // The reason for the strange looking api, where a two callbacks get passed in is to make integration of the new incremental buildpipeline easier, as this code
        // needs to be compatible both with the current buildpipeline in the dots-repo, as well as with the incremental buildpipeline.  When that is merged, we can simplify this.
        
        public static void PrepareAdditionalFiles(string buildConfigurationGuid, Func<Type, IBuildComponent> getRequiredComponent, Action<string, string> RegisterFileCopy, string outputStreamingAssetsDirectory, string buildWorkingDirectory)
        {
            T GetRequiredComponent<T>() => (T)getRequiredComponent(typeof(T));
            
            var profile = GetRequiredComponent<ClassicBuildProfile>();
            if (profile.Target == BuildTarget.NoTarget)
                throw new InvalidOperationException($"Invalid build target '{profile.Target.ToString()}'.");

            if (profile.Target != EditorUserBuildSettings.activeBuildTarget)
                throw new InvalidOperationException($"ActiveBuildTarget must be switched before the {nameof(SubSceneBuildCode)} runs.");

            var content = new BundleBuildContent(new AssetBundleBuild[0]);
            var sceneList = GetRequiredComponent<SceneList>();
            var bundleNames = new List<string>();
            var subSceneGuids = sceneList.GetScenePathsForBuild().SelectMany(scenePath => SceneMetaDataImporter.GetSubSceneGuids(AssetDatabase.AssetPathToGUID(scenePath))).Distinct().ToList();
            
            foreach (var subSceneGuid in subSceneGuids)
            {
                var hash128Guid = SceneWithBuildConfigurationGUIDs.EnsureExistsFor(subSceneGuid, new Hash128(buildConfigurationGuid));
                
                var hash = AssetDatabaseExperimental.GetArtifactHash(hash128Guid.ToString(), typeof(SubSceneImporter));
                AssetDatabaseExperimental.GetArtifactPaths(hash, out var artifactPaths);

                foreach (var artifactPath in artifactPaths)
                {
                    var ext = Path.GetExtension(artifactPath).Replace(".", "");
                    if (ext == EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesHeader))
                    {
                        var destinationFile = outputStreamingAssetsDirectory+"/"+EntityScenesPaths.RelativePathInStreamingAssetsFolderFor(subSceneGuid, EntityScenesPaths.PathType.EntitiesHeader, -1);
                        RegisterFileCopy(artifactPath, destinationFile);
                    }

                    if (ext == EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesBinary))
                    {
                        var destinationFile = outputStreamingAssetsDirectory+"/"+EntityScenesPaths.RelativePathInStreamingAssetsFolderFor(subSceneGuid, EntityScenesPaths.PathType.EntitiesBinary, EntityScenesPaths.GetSectionIndexFromPath(artifactPath));
                        RegisterFileCopy(artifactPath, destinationFile);
                    }

                    if (ext == EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesUnityObjectReferences))
                    {
                        content.CustomAssets.Add(new CustomContent
                        {
                            Asset = hash128Guid,
                            Processor = (guid, processor) =>
                            {
                                var sectionIndex = EntityScenesPaths.GetSectionIndexFromPath(artifactPath);
                                processor.GetObjectIdentifiersAndTypesForSerializedFile(artifactPath, out ObjectIdentifier[] objectIds, out Type[] types);
                                var bundlePath = EntityScenesPaths.GetLoadPath(subSceneGuid, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, sectionIndex);
                                var bundleName = Path.GetFileName(bundlePath);
                                processor.CreateAssetEntryForObjectIdentifiers(objectIds, artifactPath, bundleName, bundleName, typeof(ReferencedUnityObjects));
                                bundleNames.Add(bundleName);
                            }
                        });
                    }
                }
            }

            if (content.CustomAssets.Count <= 0) 
                return;
            
            var group = BuildPipeline.GetBuildTargetGroup(profile.Target);
            var parameters = new BundleBuildParameters(profile.Target, @group, buildWorkingDirectory)
            {
                BundleCompression = BuildCompression.Uncompressed
            };

            var status = ContentPipeline.BuildAssetBundles(parameters, content, out IBundleBuildResults result);

            foreach (var bundleName in bundleNames)
                RegisterFileCopy(buildWorkingDirectory + "/" + bundleName, outputStreamingAssetsDirectory + "/SubScenes/" + bundleName);
                
            var succeeded = status >= ReturnCode.Success;
            if (!succeeded)
                throw new InvalidOperationException($"BuildAssetBundles failed with status '{status}'.");
        }
    }
}