using System;
using NUnit.Framework;
using UnityEditor;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Scenes.Hybrid.Tests
{
    public class SubSceneTests
    {
        //string m_ScenePath = "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/TestSceneWithSubScene.unity";
        #if UNITY_EDITOR
        string m_SubScenePath =
            "Packages/com.unity.entities/Unity.Scenes.Hybrid.Tests/Playmode/TestSceneWithSubScene/TestSubScene.unity";
        #endif

        [OneTimeSetUp]
        public void SetUp()
        {
            /*
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(m_ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            */
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            /*
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAt(scenes.Count-1);
            EditorBuildSettings.scenes = scenes.ToArray();
            */
        }

        private void RegisterStreamingSystems(World world)
        {
            var systems = new List<Type>();
            systems.AddRange(new[]
            {
                typeof(SceneSystemGroup),
                typeof(SceneSystem),
                typeof(ResolveSceneReferenceSystem),
                typeof(SceneSectionStreamingSystem)
            });
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        }

        // Only works in Editor for now until we can support SubScene building with new build settings in a test
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void LoadAndUnloadSubScene()
        {
            using (var worldA = new World("World A"))
            using (var worldB = new World("World B"))
            {
                RegisterStreamingSystems(worldA);
                RegisterStreamingSystems(worldB);

                var loadParams = new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                };
                
#if UNITY_EDITOR
                Debug.Log(AssetDatabase.AssetPathToGUID(m_SubScenePath));
                Unity.Entities.Hash128 sceneGuid = new GUID(AssetDatabase.AssetPathToGUID(m_SubScenePath));
#else
                var sceneGuid = new Unity.Entities.Hash128();
#endif

                var worldAScene = worldA.GetOrCreateSystem<SceneSystem>().LoadSceneAsync(sceneGuid, loadParams);
                worldA.Update();

                var worldBScene = worldB.GetOrCreateSystem<SceneSystem>().LoadSceneAsync(sceneGuid, loadParams);
                worldB.Update();

                var worldAEntities = worldA.EntityManager.GetAllEntities(Allocator.TempJob);
                var worldBEntities = worldB.EntityManager.GetAllEntities(Allocator.TempJob);
                using (worldAEntities)
                using (worldBEntities)
                {
                    Assert.AreEqual(worldAEntities.Length, worldBEntities.Length);
                }

                var worldAQuery = worldA.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                var worldBQuery = worldB.EntityManager.CreateEntityQuery(typeof(SharedWithMaterial));
                Assert.AreEqual(worldAQuery.CalculateEntityCount(), worldBQuery.CalculateEntityCount());
                Assert.AreEqual(1, worldAQuery.CalculateEntityCount());

                // Get Material on RenderMesh
                var sharedEntitiesA = worldAQuery.ToEntityArray(Allocator.TempJob);
                var sharedEntitiesB = worldBQuery.ToEntityArray(Allocator.TempJob);

                SharedWithMaterial sharedA;
                SharedWithMaterial sharedB;
                using (sharedEntitiesA)
                using (sharedEntitiesB)
                {
                    sharedA = worldA.EntityManager.GetSharedComponentData<SharedWithMaterial>(sharedEntitiesA[0]);
                    sharedB = worldB.EntityManager.GetSharedComponentData<SharedWithMaterial>(sharedEntitiesB[0]);
                }

                Assert.AreSame(sharedA.material, sharedB.material);
                Assert.IsTrue(sharedA.material != null, "sharedA.material != null");

                var material = sharedA.material;

#if !UNITY_EDITOR
                Assert.AreEqual(1, SceneBundleHandle.GetLoadedCount());
#else
                Assert.AreEqual(0, SceneBundleHandle.GetLoadedCount());
#endif
                Assert.AreEqual(0, SceneBundleHandle.GetUnloadingCount());

                worldA.GetOrCreateSystem<SceneSystem>().UnloadScene(worldAScene);
                worldA.Update();

                worldB.GetOrCreateSystem<SceneSystem>().UnloadScene(worldBScene);
                worldB.Update();

                Assert.AreEqual(0, SceneBundleHandle.GetLoadedCount());
                Assert.AreEqual(0, SceneBundleHandle.GetUnloadingCount());
#if !UNITY_EDITOR
                Assert.IsTrue(material == null);
#endif
            }
        }
    }
}
