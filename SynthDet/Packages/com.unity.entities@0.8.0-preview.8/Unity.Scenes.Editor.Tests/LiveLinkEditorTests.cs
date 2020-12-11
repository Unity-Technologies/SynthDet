using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Hash128 = Unity.Entities.Hash128;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    /*
     * These tests provide some coverage for LiveLink in the editor. LiveLink, by default, is used in edit mode and in
     * play mode whenever there is an open subscene. Its contents are converted to entities in the background, that is
     * the essential feature of LiveLink.
     *
     * The setup here is as follows:
     *  - all subscenes are created in a new temporary directory per test,
     *  - that directory is cleaned up when the test finished,
     *  - we also flush the entity scene paths cache to get rid of any subscene build files,
     *  - we clearly separate all tests into setup and test, because the latter might run in play mode.
     * That last point is crucial: Entering playmode serializes the test fixture, but not the contents of variables
     * within the coroutine that represents a test. This means that you cannot rely on the values of any variables and
     * you can get very nasty exceptions by assigning a variable from setup in play mode (due to the way enumerator
     * functions are compiled). Any data that needs to persist between edit and play mode must be stored on the class
     * itself.
     */
    [Serializable]
    [TestFixture]
    class LiveLinkEditorTests
    {
        [SerializeField]
        string m_TempAssetDir;
        [SerializeField]
        bool m_WasLiveLinkEnabled;
        [SerializeField]
        EnterPlayModeOptions m_EnterPlayModeOptions;
        [SerializeField]
        bool m_UseEnterPlayerModeOptions;
        [SerializeField]
        int m_SceneCounter;

        [SerializeField]
        string m_PrefabPath;

        [OneTimeSetUp]
        public void SetUp()
        {
            if (m_TempAssetDir != null)
            {
                // this setup code is run again when we switch to playmode
                return;
            }

            // Create a temporary folder for test assets
            string path;
            do
            {
                path = Path.GetRandomFileName();
            } while (AssetDatabase.IsValidFolder(Path.Combine("Assets", path)));

            var guid = AssetDatabase.CreateFolder("Assets", path);
            m_TempAssetDir = AssetDatabase.GUIDToAssetPath(guid);
            m_WasLiveLinkEnabled = SubSceneInspectorUtility.LiveLinkEnabledInEditMode;
            m_EnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            m_UseEnterPlayerModeOptions = EditorSettings.enterPlayModeOptionsEnabled;
            SubSceneInspectorUtility.LiveLinkEnabledInEditMode = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Clean up all test assets
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            AssetDatabase.DeleteAsset(m_TempAssetDir);
            SceneWithBuildConfigurationGUIDs.ClearBuildSettingsCache();
            SubSceneInspectorUtility.LiveLinkEnabledInEditMode = m_WasLiveLinkEnabled;
            EditorSettings.enterPlayModeOptions = m_EnterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = m_UseEnterPlayerModeOptions;
        }

        static SubScene CreateSubSceneInSceneFromObjects(string name, bool keepOpen, Scene parentScene, Func<List<GameObject>> createObjects = null)
        {
            var args = new SubSceneContextMenu.NewSubSceneArgs
            {
                parentScene = parentScene,
                newSubSceneMode = SubSceneContextMenu.NewSubSceneMode.EmptyScene
            };
            SceneManager.SetActiveScene(parentScene);

            var subScene = SubSceneContextMenu.CreateNewSubScene(name, args, InteractionMode.AutomatedAction);
            SubSceneInspectorUtility.EditScene(subScene);
            var objects = createObjects?.Invoke();
            if (objects != null)
            {
                foreach (var obj in objects)
                    SceneManager.MoveGameObjectToScene(obj, subScene.EditingScene);
            }

            EditorSceneManager.SaveScene(subScene.EditingScene);
            EditorSceneManager.SaveScene(parentScene);
            if (!keepOpen)
                SubSceneInspectorUtility.CloseSceneWithoutSaving(subScene);
            return subScene;
        }

        static Scene CreateScene(string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SceneManager.SetActiveScene(scene);
            var dir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            AssetDatabase.DeleteAsset(scenePath);
            EditorSceneManager.SaveScene(scene, scenePath);
            return scene;
        }

        Scene CreateTmpScene()
        {
            var parentScenePath = Path.Combine(m_TempAssetDir, $"TestSceneParent___{m_SceneCounter}.unity");
            m_SceneCounter++;
            return CreateScene(parentScenePath);
        }

        SubScene CreateSubSceneFromObjects(string name, bool keepOpen, Func<List<GameObject>> createObjects)
        {
            return CreateSubSceneInSceneFromObjects(name, keepOpen, CreateTmpScene(), createObjects);
        }

        SubScene CreateEmptySubScene(string name, bool keepOpen) => CreateSubSceneFromObjects(name, keepOpen, null);

        static World GetLiveLinkWorld(bool playMode)
        {
            if (playMode)
                return World.DefaultGameObjectInjectionWorld;
            DefaultWorldInitialization.DefaultLazyEditModeInitialize();
            return World.DefaultGameObjectInjectionWorld;
        }

        static IEditModeTestYieldInstruction GetEnterPlayMode(bool usePlayMode)
            => usePlayMode ? new EnterPlayMode() : null;

        static void SetDomainReload(EnteringPlayMode useDomainReload)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = useDomainReload == EnteringPlayMode.WithDomainReload ? EnterPlayModeOptions.None : EnterPlayModeOptions.DisableDomainReload;
        }

        public enum EnteringPlayMode
        {
            WithDomainReload,
            WithoutDomainReload,
        }

        [UnityTest, Explicit]
        public IEnumerator OpenSubSceneStaysOpen_Play([Values] EnteringPlayMode useDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(true);

            {
                var subScene = Object.FindObjectOfType<SubScene>();
                Assert.IsTrue(subScene.IsLoaded);
            }
        }

        [UnityTest, Explicit]
        public IEnumerator ClosedSubSceneStaysClosed_Play([Values] EnteringPlayMode useDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", false);
            }

            yield return GetEnterPlayMode(true);

            {
                var subScene = Object.FindObjectOfType<SubScene>();
                Assert.IsFalse(subScene.IsLoaded);
            }
        }

        [UnityTest, Explicit]
        public IEnumerator ClosedSubSceneCanBeOpened_Play([Values] EnteringPlayMode useDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", false);
            }

            yield return GetEnterPlayMode(true);

            {
                var subScene = Object.FindObjectOfType<SubScene>();
                Assert.IsFalse(subScene.IsLoaded);
                SubSceneInspectorUtility.EditScene(subScene);
                yield return null;
                Assert.IsTrue(subScene.IsLoaded);
            }
        }

        [UnityTest]
        public IEnumerator LiveLinkConvertsSubScenes_Edit() => LiveLinkConvertsSubScenes(false);

        [UnityTest, Explicit]
        public IEnumerator LiveLinkConvertsSubScenes_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkConvertsSubScenes(true, useDomainReload);

        IEnumerator LiveLinkConvertsSubScenes(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                var scene = CreateTmpScene();
                CreateSubSceneInSceneFromObjects("TestSubScene1", true, scene);
                CreateSubSceneInSceneFromObjects("TestSubScene2", true, scene);
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);

                var query = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SubScene>());
                var subScenes = query.ToComponentArray<SubScene>();
                var subSceneObjects = Object.FindObjectsOfType<SubScene>();
                foreach (var subScene in subSceneObjects)
                    Assert.Contains(subScene, subScenes);
            }
        }
        
        [UnityTest]
        public IEnumerator LiveLinkRemovesDeletedSubScene_Edit() => LiveLinkRemovesDeletedSubScene(false);

        [UnityTest, Explicit]
        public IEnumerator LiveLinkRemovesDeletedSubScene_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkRemovesDeletedSubScene(true, useDomainReload);

        IEnumerator LiveLinkRemovesDeletedSubScene(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                var scene = CreateTmpScene();
                CreateSubSceneInSceneFromObjects("TestSubScene1", true, scene);
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);

                var query = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<SubScene>());
                var subScene = Object.FindObjectOfType<SubScene>();
                Assert.Contains(subScene, query.ToComponentArray<SubScene>(), "SubScene was not loaded");

                Object.DestroyImmediate(subScene.gameObject);

                w.Update();

                Assert.IsTrue(query.IsEmptyIgnoreFilter, "SubScene was not unloaded");
            }
        }

        [UnityTest]
        public IEnumerator LiveLinkConvertsObjects_Edit() => LiveLinkConvertsObjects(false);

        [UnityTest, Explicit]
        public IEnumerator LiveLinkConvertsObjects_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkConvertsObjects(true, useDomainReload);

        IEnumerator LiveLinkConvertsObjects(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestPrefabComponentAuthoring>();
                    return new List<GameObject> { go };
                });
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);
                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            }
        }

        [UnityTest]
        public IEnumerator LiveLinkCreatesEntitiesWhenObjectIsCreated_Edit() => LiveLinkCreatesEntitiesWhenObjectIsCreated(false);

        [UnityTest, Explicit, Ignore("Doesn't currently work, since Undo.RegisterCreatedObjectUndo isn't reliably picked up by Undo.postprocessModifications and Scenes are never marked dirty in play mode. A reconversion is never triggered.")]
        public IEnumerator LiveLinkCreatesEntitiesWhenObjectIsCreated_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkCreatesEntitiesWhenObjectIsCreated(true, useDomainReload);

        IEnumerator LiveLinkCreatesEntitiesWhenObjectIsCreated(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var subScene = Object.FindObjectOfType<SubScene>();

                var w = GetLiveLinkWorld(usePlayMode);
                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount());

                // should this be working?
                SceneManager.SetActiveScene(subScene.EditingScene);
                var go = new GameObject("CloneMe", typeof(TestPrefabComponentAuthoring));
                Undo.RegisterCreatedObjectUndo(go, "Create new object");
                Assert.AreEqual(go.scene, subScene.EditingScene);

                w.Update();

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Undo.PerformUndo();

                w.Update();

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed, undo failed");
                Undo.PerformRedo();

                w.Update();

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted, redo failed");
            }
        }
        
        [UnityTest]
        public IEnumerator LiveLinkCreatesEntitiesWhenObjectMoves_Edit() => LiveLinkCreatesEntitiesWhenObjectMoves(false);

        [UnityTest, Explicit]
        public IEnumerator LiveLinkCreatesEntitiesWhenObjectMoves_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkCreatesEntitiesWhenObjectMoves(true, useDomainReload);

        IEnumerator LiveLinkCreatesEntitiesWhenObjectMoves(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var subScene = Object.FindObjectOfType<SubScene>();

                var w = GetLiveLinkWorld(usePlayMode);
                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(0, testTagQuery.CalculateEntityCount());

                var go = new GameObject("TestGameObject");
                go.AddComponent<TestPrefabComponentAuthoring>();
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move1");

                // this doesn't work:
                //    SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);

                w.Update();

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
                Undo.PerformUndo();

                w.Update();

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed, undo failed");
                Undo.PerformRedo();

                w.Update();

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted, redo failed");
            }
        }
        
        [UnityTest]
        public IEnumerator LiveLinkDestroysEntitiesWhenObjectMoves_Edit() => LiveLinkCreatesEntitiesWhenObjectMoves(false);
        [UnityTest, Explicit, Ignore("Doesn't currently work, since Undo.MoveGameObjectToScene isn't reliably picked up by Undo.postprocessModifications and Scenes are never marked dirty in play mode. A reconversion is never triggered.")]
        public IEnumerator LiveLinkDestroysEntitiesWhenObjectMoves_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkDestroysEntitiesWhenObjectMoves(true, useDomainReload);

        IEnumerator LiveLinkDestroysEntitiesWhenObjectMoves(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestPrefabComponentAuthoring>();
                    return new List<GameObject> { go };
                });
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var subScene = Object.FindObjectOfType<SubScene>();

                var w = GetLiveLinkWorld(usePlayMode);
                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");

                var go = Object.FindObjectOfType<TestPrefabComponentAuthoring>().gameObject;
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move1");

                w.Update();

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed");
                Undo.PerformUndo();

                w.Update();

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted, undo failed");
                Undo.PerformRedo();

                w.Update();

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected an entity to be removed, redo failed");
            }
        }

        [UnityTest]
        public IEnumerator LiveLinkSupportsAddComponentAndUndo_Edit() => LiveLinkSupportsAddComponentAndUndo(false);

        [UnityTest, Explicit, Ignore("Doesn't currently work, since Undo.AddComponent isn't picked up by Undo.postprocessModifications and Scenes are never marked dirty in play mode. A reconversion is never triggered.")]
        public IEnumerator LiveLinkSupportsAddComponentAndUndo_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkSupportsAddComponentAndUndo(true, useDomainReload);

        IEnumerator LiveLinkSupportsAddComponentAndUndo(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", true);
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);

                var subScene = Object.FindObjectOfType<SubScene>();
                var go = new GameObject("TestGameObject");
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move");
                Undo.IncrementCurrentGroup();

                yield return null;

                Undo.AddComponent<TestPrefabComponentAuthoring>(go);
                Undo.IncrementCurrentGroup();
                Assert.IsNotNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and gain a component");

                Undo.PerformUndo();
                Assert.IsNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and lose a component, undo add failed");

                Undo.PerformRedo();
                Assert.IsNotNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and gain a component, redo add failed");
            }
        }
        
        [UnityTest]
        public IEnumerator LiveLinkSupportsRemoveComponentAndUndo_Edit() => LiveLinkSupportsRemoveComponentAndUndo(false);

        [UnityTest, Explicit, Ignore("Doesn't currently work, since Undo.DestroyObjectImmediate isn't picked up by Undo.postprocessModifications and Scenes are never marked dirty in play mode. A reconversion is never triggered.")]
        public IEnumerator LiveLinkSupportsRemoveComponentAndUndo_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkSupportsRemoveComponentAndUndo(true, useDomainReload);

        IEnumerator LiveLinkSupportsRemoveComponentAndUndo(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateEmptySubScene("TestSubScene", true);
            }
            
            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);

                var subScene = Object.FindObjectOfType<SubScene>();
                var go = new GameObject("TestGameObject");
                go.AddComponent<TestPrefabComponentAuthoring>();
                Undo.MoveGameObjectToScene(go, subScene.EditingScene, "Test Move");
                Undo.IncrementCurrentGroup();

                yield return null;

                var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted with a component");

                Undo.DestroyObjectImmediate(go.GetComponent<TestPrefabComponentAuthoring>());
                Undo.IncrementCurrentGroup();

                Assert.IsNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and lose a component");

                Undo.PerformUndo();
                Assert.IsNotNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and gain a component, undo remove failed");

                Undo.PerformRedo();
                Assert.IsNull(go.GetComponent<TestPrefabComponentAuthoring>());

                yield return null;

                Assert.AreEqual(0, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted and lose a component, redo remove failed");
            }
        }

        [UnityTest]
        public IEnumerator LiveLinkReflectsChangedComponentValues_Edit() => LiveLinkReflectsChangedComponentValues(false);

        [UnityTest, Explicit]
        public IEnumerator LiveLinkReflectsChangedComponentValues_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkReflectsChangedComponentValues(true, useDomainReload);

        IEnumerator LiveLinkReflectsChangedComponentValues(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            SetDomainReload(useDomainReload);
            var subScene = CreateEmptySubScene("TestSubScene", true);

            var go = new GameObject("TestGameObject");
            var authoring = go.AddComponent<TestPrefabComponentAuthoring>();
            authoring.IntValue = 15;
            SceneManager.MoveGameObjectToScene(go, subScene.EditingScene);

            yield return GetEnterPlayMode(usePlayMode);

            authoring = Object.FindObjectOfType<TestPrefabComponentAuthoring>();
            Assert.AreEqual(authoring.IntValue, 15);

            var w = GetLiveLinkWorld(usePlayMode);
            var testTagQuery = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
            Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            Assert.AreEqual(15, testTagQuery.GetSingleton<TestPrefabComponent>().IntValue);

            Undo.RecordObject(authoring, "Change component value");
            authoring.IntValue = 2;

            // it takes an extra frame to establish that something has changed when using RecordObject unless Flush is called
            Undo.FlushUndoRecordObjects();

            yield return null;

            Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            Assert.AreEqual(2, testTagQuery.GetSingleton<TestPrefabComponent>().IntValue, "Expected a component value to change");

            Undo.PerformUndo();

            yield return null;

            Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            Assert.AreEqual(15, testTagQuery.GetSingleton<TestPrefabComponent>().IntValue, "Expected a component value to change, undo failed");

            Undo.PerformRedo();

            yield return null;

            Assert.AreEqual(1, testTagQuery.CalculateEntityCount(), "Expected a game object to be converted");
            Assert.AreEqual(2, testTagQuery.GetSingleton<TestPrefabComponent>().IntValue, "Expected a component value to change, redo failed");
        }
        
        [UnityTest]
        public IEnumerator LiveLinkDisablesEntityWhenGameObjectIsDisabled_Edit() => LiveLinkDisablesEntityWhenGameObjectIsDisabled(false);

        [UnityTest, Explicit, Ignore("Doesn't currently work, since Scenes are never marked dirty in play mode. A reconversion is never triggered.")]
        public IEnumerator LiveLinkDisablesEntityWhenGameObjectIsDisabled_Play([Values] EnteringPlayMode useDomainReload) => LiveLinkDisablesEntityWhenGameObjectIsDisabled(true, useDomainReload);

        IEnumerator LiveLinkDisablesEntityWhenGameObjectIsDisabled(bool usePlayMode, EnteringPlayMode useDomainReload = EnteringPlayMode.WithoutDomainReload)
        {
            {
                SetDomainReload(useDomainReload);
                CreateSubSceneFromObjects("TestSubScene", true, () =>
                {
                    var go = new GameObject("TestGameObject");
                    go.AddComponent<TestPrefabComponentAuthoring>();
                    return new List<GameObject> { go };
                });
            }

            yield return GetEnterPlayMode(usePlayMode);

            {
                var w = GetLiveLinkWorld(usePlayMode);
                var queryWithoutDisabled = w.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<TestPrefabComponent>());
                Assert.AreEqual(1, queryWithoutDisabled.CalculateEntityCount(), "Expected a game object to be converted");
                
                var go = Object.FindObjectOfType<TestPrefabComponentAuthoring>().gameObject;
                Undo.RecordObject(go, "DisableObject");
                go.SetActive(false);
                Undo.FlushUndoRecordObjects();
                
                w.Update();

                var queryWithDisabled = w.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadWrite<TestPrefabComponent>(), ComponentType.ReadWrite<Disabled>() },
                    Options = EntityQueryOptions.IncludeDisabled
                });
                Assert.AreEqual(1, queryWithDisabled.CalculateEntityCount(), "Expected a game object to be converted and disabled");
                
                Assert.AreEqual(0, queryWithoutDisabled.CalculateEntityCount(), "Expected a game object to be converted and disabled");
            }
        }
    }
}
