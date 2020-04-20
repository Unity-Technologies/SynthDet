#if HDRP_PRESENT

using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace UnityEngine.SimViz.Sensors
{
    /// <summary>
    /// Custom Pass which renders labeled images where each object labeled with a Labeling component is drawn with the
    /// value specified by the given LabelingConfiguration.
    /// </summary>
    public class LabelingPass : GroundTruthPass
    {
        public RenderTexture targetTexture;
        public LabelingConfiguration labelingConfiguration;

        //Serialize the shader so that the shader asset is included in player builds when the LabelingPass is used.
        //Currently commented out and shaders moved to Resources folder due to serialization crashes when it is enabled. See https://fogbugz.unity3d.com/f/cases/1187378/
        //[SerializeField]
        Shader m_ClassLabelingShader;
        Material m_OverrideMaterial;
        static readonly int k_LabelingId = Shader.PropertyToID("LabelingId");

        public LabelingPass(Camera targetCamera, RenderTexture targetTexture) : base(targetCamera)
        {
            this.targetTexture = targetTexture;
        }

        public LabelingPass() : base(null)
        {
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            base.Setup(renderContext, cmd);
            m_ClassLabelingShader = Shader.Find("Renderers/ClassLabeling");
#if UNITY_EDITOR || !UNITY_INCLUDE_TESTS
            //Shader.WarmupAllShaders() causes DX issues in players built from the Test Runner See https://fogbugz.unity3d.com/f/cases/1194661/
            //Shader.WarmupAllShaders();
#endif
            m_OverrideMaterial = new Material(m_ClassLabelingShader);
        }

        //Render all objects to our target RenderTexture using `overrideMaterial` to use our shader
        protected override void ExecutePass(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
        {
            var renderList = CreateRendererListDesc(hdCamera, cullingResult, "FirstPass", 0, m_OverrideMaterial, -1);

            CoreUtils.SetRenderTarget(cmd, new RenderTargetIdentifier(targetTexture), ClearFlag.All);
            HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(renderList));
        }
        public override void SetupMaterialProperties(MaterialPropertyBlock mpb, MeshRenderer meshRenderer, Labeling labeling, uint instanceId)
        {
            var entry = new LabelingConfigurationEntry();
            foreach (var l in labelingConfiguration.LabelingConfigurations)
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
#endif
