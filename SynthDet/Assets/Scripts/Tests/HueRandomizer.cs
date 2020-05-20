using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Random = Unity.Mathematics.Random;

// NOTE: This class is meant solely to be used for debugging/testing of our custom HueShift shader
[RequireComponent(typeof(MeshRenderer))]
[ExecuteInEditMode]
public class HueRandomizer : MonoBehaviour
{
    [Range(-720, 720)]
    public float HueStep;
    float m_HueValue;
    // ReSharper disable once NotAccessedField.Local
    Random m_Rand;

    void Start()
    {
        if (Application.isPlaying)
        {
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<ForegroundObjectPlacer>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<LightingRandomizerSystem>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetExistingSystem<BackgroundGenerator>().Enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        m_HueValue += HueStep * Time.deltaTime;
        while (m_HueValue > 360f)
        {
            m_HueValue -= 360f;
        }

        while (m_HueValue < 0f)
        {
            m_HueValue += 360f;
        }

        var meshRenderer = GetComponent<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        mpb.SetFloat("_HueOffset", m_HueValue);
        meshRenderer.SetPropertyBlock(mpb, 0);
   }
}
