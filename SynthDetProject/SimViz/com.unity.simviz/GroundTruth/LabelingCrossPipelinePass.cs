using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace UnityEngine.SimViz.Sensors
{
    /// <summary>
    /// Custom Pass which renders labeled images where each object labeled with a Labeling component is drawn with the
    /// value specified by the given LabelingConfiguration.
    /// </summary>
    public class LabelingCrossPipelinePass : GroundTruthCrossPipelinePass
    {
        string m_ShaderName;
        RenderTexture m_TargetTexture;
        LabelingConfiguration m_LabelingConfiguration;

        //Serialize the shader so that the shader asset is included in player builds when the LabelingPass is used.
        //Currently commented out and shaders moved to Resources folder due to serialization crashes when it is enabled. See https://fogbugz.unity3d.com/f/cases/1187378/
        //[SerializeField]
        Shader m_ClassLabelingShader;
        Material m_OverrideMaterial;
        static readonly int k_LabelingId = Shader.PropertyToID("LabelingId");

        public LabelingCrossPipelinePass(Camera targetCamera, RenderTexture targetTexture, string shaderName, LabelingConfiguration labelingConfiguration) : base(targetCamera)
        {
            this.m_TargetTexture = targetTexture;
            this.m_ShaderName = shaderName;
            this.m_LabelingConfiguration = labelingConfiguration;
        }

        public override void Setup()
        {
            base.Setup();
            m_ClassLabelingShader = Shader.Find(m_ShaderName);
#if UNITY_EDITOR || !UNITY_INCLUDE_TESTS
            //Shader.WarmupAllShaders() causes DX issues in players built from the Test Runner See https://fogbugz.unity3d.com/f/cases/1194661/
            //Shader.WarmupAllShaders();
#endif
            m_OverrideMaterial = new Material(m_ClassLabelingShader);
        }

        protected override void ExecutePass(ScriptableRenderContext renderContext, CommandBuffer cmd, Camera camera, CullingResults cullingResult)
        {
            var renderList = CreateRendererListDesc(camera, cullingResult, "FirstPass", 0, m_OverrideMaterial, -1);
            cmd.ClearRenderTarget(true, true, Color.clear);
            DrawRendererList(renderContext, cmd, RendererList.Create(renderList));
        }

        public override void SetupMaterialProperties(MaterialPropertyBlock mpb, MeshRenderer meshRenderer, Labeling labeling, uint instanceId)
        {
            var entry = new LabelingConfigurationEntry();
            foreach (var l in m_LabelingConfiguration.LabelingConfigurations)
            {
                if (labeling.classes.Contains(l.label))
                {
                    entry = l;
                    break;
                }
            }

            //Set the labeling ID so that it can be accessed in ClassLabelingPass.shader
            mpb.SetInt(k_LabelingId, entry.value);
        }
    }
}
