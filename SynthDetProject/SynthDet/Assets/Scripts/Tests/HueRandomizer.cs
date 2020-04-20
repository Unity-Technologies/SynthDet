using Unity.Entities;
using UnityEngine;

// NOTE: This class is meant solely to be used for debugging/testing of our custom HueShift shader
[RequireComponent(typeof(MeshRenderer))]
public class HueRandomizer : MonoBehaviour
{
    [Range(-720, 720)]
    public float HueStep;
    private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_hueProperty;
    private float m_hueValue = 0f;


    void Start()
    {
        m_renderer = GetComponent<MeshRenderer>();
        var mat = m_renderer.sharedMaterial;
        
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<ForegroundObjectPlacer>().Enabled = false;
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<LightingRandomizerSystem>().Enabled = false;
        World.DefaultGameObjectInjectionWorld.GetExistingSystem<BackgroundGenerator>().Enabled = false;
        m_hueProperty = new MaterialPropertyBlock();
        m_hueProperty.SetFloat("_HueOffset", m_hueValue);
    }

    // Update is called once per frame
    void Update()
    {
        m_hueValue += HueStep * Time.deltaTime;
        while (m_hueValue > 360f)
        {
            m_hueValue -= 360f;
        }

        while (m_hueValue < 0f)
        {
            m_hueValue += 360f;
        }

        m_hueProperty.SetFloat("_HueOffset", m_hueValue);
        m_renderer.SetPropertyBlock(m_hueProperty, 0);
   }
}
