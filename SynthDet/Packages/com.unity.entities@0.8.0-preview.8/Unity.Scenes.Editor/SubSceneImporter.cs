using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using AssetImportContext = UnityEditor.Experimental.AssetImporters.AssetImportContext;

namespace Unity.Scenes.Editor
{
    [UnityEditor.Experimental.AssetImporters.ScriptedImporter(71, "extDontMatter")]
    [InitializeOnLoad]
    class SubSceneImporter : UnityEditor.Experimental.AssetImporters.ScriptedImporter
    {
        static SubSceneImporter()
        {
            EntityScenesPaths.SubSceneImporterType = typeof(SubSceneImporter);
        }

        static unsafe NativeList<RuntimeGlobalObjectId> ReferencedUnityObjectsToRuntimeGlobalObjectIds(ReferencedUnityObjects referencedUnityObjects, Allocator allocator = Allocator.Temp)
        {
            var globalObjectIds = new GlobalObjectId[referencedUnityObjects.Array.Length];
            var runtimeGlobalObjIDs = new NativeList<RuntimeGlobalObjectId>(globalObjectIds.Length, allocator);
            
            GlobalObjectId.GetGlobalObjectIdsSlow(referencedUnityObjects.Array, globalObjectIds);
            
            for (int i = 0; i != globalObjectIds.Length; i++)
            {
                var globalObjectId = globalObjectIds[i];

                //@TODO: HACK (Object is a scene object)
                if (globalObjectId.identifierType == 2)
                {
                    Debug.LogWarning($"{referencedUnityObjects.Array[i]} is part of a scene, LiveLink can't transfer scene objects. (Note: LiveConvertSceneView currently triggers this)");
                    continue;
                }

                if (globalObjectId.assetGUID == new GUID())
                {
                    //@TODO: How do we handle this
                    Debug.LogWarning($"{referencedUnityObjects.Array[i]} has no valid GUID. LiveLink currently does not support built-in assets.");
                    continue;
                }

                var runtimeGlobalObjectId =
                    System.Runtime.CompilerServices.Unsafe.AsRef<RuntimeGlobalObjectId>(&globalObjectId);
                runtimeGlobalObjIDs.Add(runtimeGlobalObjectId);
            }

            return runtimeGlobalObjIDs;
        }

        static void WriteRefGuids(List<ReferencedUnityObjects> referencedUnityObjects, SceneSectionData[] sectionData, AssetImportContext ctx)
        {
            for (var index = 0; index < referencedUnityObjects.Count; index++)
            {
                var sectionIndex = sectionData[index].SubSectionIndex;
                
                var objRefs = referencedUnityObjects[index];
                if (objRefs == null)
                    continue;

                var refGuidsPath = ctx.GetResultPath($"{sectionIndex}.{EntityScenesPaths.GetExtension(EntityScenesPaths.PathType.EntitiesUnityObjectRefGuids)}");
                var runtimeGlobalObjectIds = ReferencedUnityObjectsToRuntimeGlobalObjectIds(objRefs);

                using (var refGuidWriter = new StreamBinaryWriter(refGuidsPath))
                {
                    refGuidWriter.Write(runtimeGlobalObjectIds.Length);
                    refGuidWriter.WriteArray(runtimeGlobalObjectIds.AsArray());
                }

                runtimeGlobalObjectIds.Dispose();
            }
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                ctx.DependsOnCustomDependency("EntityBinaryFileFormatVersion");

                var sceneWithBuildConfiguration = SceneWithBuildConfigurationGUIDs.ReadFromFile(ctx.assetPath);

                // Ensure we have as many dependencies as possible registered early in case an exception is thrown
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneWithBuildConfiguration.SceneGUID.ToString());
                ctx.DependsOnSourceAsset(scenePath);

                if (sceneWithBuildConfiguration.BuildConfiguration.IsValid)
                {
                    var buildConfigurationPath = AssetDatabase.GUIDToAssetPath(sceneWithBuildConfiguration.BuildConfiguration.ToString());
                    ctx.DependsOnSourceAsset(buildConfigurationPath);
                    var buildConfigurationDependencies = AssetDatabase.GetDependencies(buildConfigurationPath);
                    foreach (var dependency in buildConfigurationDependencies)
                        ctx.DependsOnSourceAsset(dependency);
                }

                var dependencies = AssetDatabase.GetDependencies(scenePath);
                foreach (var dependency in dependencies)
                {
                    if (dependency.ToLower().EndsWith(".prefab"))
                        ctx.DependsOnSourceAsset(dependency);
                }

                var config = BuildConfiguration.LoadAsset(sceneWithBuildConfiguration.BuildConfiguration);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                try
                {
                    var settings = new GameObjectConversionSettings();

                    settings.SceneGUID = sceneWithBuildConfiguration.SceneGUID;
                    settings.BuildConfiguration = config;
                    settings.AssetImportContext = ctx;

                    var sectionRefObjs = new List<ReferencedUnityObjects>();
                    var sectionData = EditorEntityScenes.WriteEntityScene(scene, settings, sectionRefObjs);
                    WriteRefGuids(sectionRefObjs, sectionData, ctx);
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            // Currently it's not acceptable to let the asset database catch the exception since it will create a default asset without any dependencies
            // This means a reimport will not be triggered if the scene is subsequently modified
            catch(Exception e)
            {
                Debug.Log($"Exception thrown during SubScene import: {e}");
            }
        }
    }
}