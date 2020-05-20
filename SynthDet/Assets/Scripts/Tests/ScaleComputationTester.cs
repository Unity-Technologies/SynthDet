using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class ScaleComputationTester : MonoBehaviour
{
    public GameObject SourceObject;
    public List<GameObject> TargetObjects;
    
    float m_ProjectedSize;

    Text m_TextUI;
    Camera m_Camera;
    
    // Start is called before the first frame update
    void Start()
    {
        Init();
    }

    void OnEnable()
    {
        Init();
    }

    void Init()
    {
        if (Application.isPlaying)
        {
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<BackgroundGenerator>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<ForegroundObjectPlacer>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<LightingRandomizerSystem>().Enabled = false;
        }

        m_Camera = GetComponent<Camera>();
        m_TextUI = GetComponentInChildren<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        var tfSource = SourceObject.transform;
        // NOTE: The bounds from the meshfilter mesh are for the un-transformed mesh,
        //       the mesh renderer's mesh already has the transforms (rotation and scale) applied
        var boundsSource = SourceObject.GetComponent<MeshFilter>().sharedMesh.bounds;

        var transformer = new WorldToScreenTransformer(m_Camera);
        m_ProjectedSize = ObjectPlacementUtilities.ComputeProjectedArea(
            transformer, tfSource.position, tfSource.rotation, tfSource.localScale, boundsSource);
        m_TextUI.text = $"Area: {m_ProjectedSize:F2} px^2";

        foreach (var target in TargetObjects)
        {
            var tfTarget = target.transform;
            var boundsTarget = target.GetComponent<MeshFilter>().sharedMesh.bounds;
            var scalarTarget = ObjectPlacementUtilities.ComputeScaleToMatchArea(
                transformer, tfTarget.position, tfTarget.rotation, boundsTarget, m_ProjectedSize);
            target.transform.localScale = scalarTarget * Vector3.one;
        }
    }
}
