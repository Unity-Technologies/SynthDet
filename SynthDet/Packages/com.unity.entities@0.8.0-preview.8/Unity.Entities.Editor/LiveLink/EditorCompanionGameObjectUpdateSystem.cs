#if !UNITY_DISABLE_MANAGED_COMPONENTS && !DOTS_HYBRID_COMPONENTS_DEBUG
using Unity.Collections;
using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
class EditorCompanionGameObjectUpdateSystem : ComponentSystem
{
    EntityQuery m_WithoutTag;

    struct SceneAndMask
    {
        public Scene scene;
        public ulong mask;
    }

    FixedList512<SceneAndMask> m_CompanionScenes;

    protected override void OnCreate()
    {
        m_CompanionScenes = new FixedList512<SceneAndMask>();
        m_WithoutTag = Entities.WithAll<CompanionLink>().WithNone<EditorCompanionInPreviewSceneTag>().ToEntityQuery();
    }

    protected override void OnDestroy()
    {
        foreach (var sceneAndMask in m_CompanionScenes)
        {
            EditorSceneManager.ClosePreviewScene(sceneAndMask.scene);
        }
    }

    protected override void OnUpdate()
    {
        Entities.WithNone<EditorCompanionInPreviewSceneTag>().ForEach((EditorRenderData renderData, CompanionLink link) =>
        {
            foreach (var sceneAndMask in m_CompanionScenes)
            {
                if (sceneAndMask.mask == renderData.SceneCullingMask)
                {
                    EditorSceneManager.MoveGameObjectToScene(link.Companion, sceneAndMask.scene);
                    return;
                }
            }

            var scene = EditorSceneManager.NewPreviewScene();

            m_CompanionScenes.Add(new SceneAndMask
            {
                scene = scene,
                mask = renderData.SceneCullingMask
            });

            EditorSceneManager.SetSceneCullingMask(scene, renderData.SceneCullingMask);
            EditorSceneManager.MoveGameObjectToScene(link.Companion, scene);
        });

        EntityManager.AddComponent<EditorCompanionInPreviewSceneTag>(m_WithoutTag);
    }
}
#endif