using System;
using Unity.Entities;
using UnityEngine;

[UpdateAfter(typeof(ForegroundObjectPlacer))]
[UpdateAfter(typeof(BackgroundGenerator))]
public class EndSimulationSystem : ComponentSystem
{
    EntityQuery m_EntityQuery;

    protected override void OnCreate()
    {
        m_EntityQuery = EntityManager.CreateEntityQuery(typeof(CurriculumState));
    }

    protected override void OnUpdate()
    {
        if (m_EntityQuery.CalculateEntityCount() != 1)
            return;

        var entity = m_EntityQuery.GetSingletonEntity();
        var curriculumState = EntityManager.GetComponentData<CurriculumState>(entity);
        var placementStatics = EntityManager.GetComponentObject<PlacementStatics>(entity);
        if (curriculumState.ScaleIndex >= placementStatics.ScaleFactors.Length || UnityEngine.Time.frameCount > placementStatics.MaxFrames)
        {
            Debug.Log($"Frames: {UnityEngine.Time.frameCount} Duration: {UnityEngine.Time.realtimeSinceStartup}s, Effective FPS: {(float)UnityEngine.Time.frameCount / UnityEngine.Time.realtimeSinceStartup}. Does not include cleanup.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            UnityEngine.Application.Quit();   
#endif
        }
    }
}
