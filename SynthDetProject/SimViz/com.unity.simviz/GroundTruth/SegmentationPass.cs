#if HDRP_PRESENT

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.SimViz.Sensors
{
    /// <summary>
    /// A CustomPass for creating object instance segmentation images. GameObjects containing Labeling components
    /// are assigned unique IDs, which are rendered into the target texture.
    /// </summary>
    public class SegmentationPass : GroundTruthPass
    {
        public const string SegmentationPassShaderName = "Renderers/SegmentationPassHDRPShader";
        public static readonly int SegmentationIdProperty = Shader.PropertyToID("_SegmentationId");

        //Filter settings
        public LayerMask layerMask = -1;
        readonly SortingCriteria m_SortingCriteria = SortingCriteria.CommonOpaque;

        //Serialize this field so that the shader is included in the build.
        //[SerializeField]
        Shader m_SegmentationShader;
        Material m_OverrideMaterial;
        public RenderTexture targetTexture;
        public bool reassignIds = false;
        public uint idStart = 1;
        public uint idStep = 1;
        int m_NextObjectIndex;

        Dictionary<uint, uint> m_Ids;

        public SegmentationPass(Camera targetCamera, RenderTexture targetTexture, uint idStart = 1, uint idStep = 1)
            :base(targetCamera)
        {
            if (targetCamera == null)
                throw new ArgumentNullException(nameof(targetCamera));

            //Activating in the constructor allows us to get correct labeling in the first frame.
            EnsureActivated();

            this.targetTexture = targetTexture;
            this.idStart = idStart;
            this.idStep = idStep;
        }

        public SegmentationPass() :base(null)
        {}

        static ProfilerMarker s_ExecuteMarker = new ProfilerMarker("SegmentationPass_Execute");

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            base.Setup(renderContext, cmd);
            m_SegmentationShader = Shader.Find(SegmentationPassShaderName);
#if UNITY_EDITOR || !UNITY_INCLUDE_TESTS
            //Shader.WarmupAllShaders() causes DX issues in players built from the Test Runner. See https://fogbugz.unity3d.com/f/cases/1194661/
            //Shader.WarmupAllShaders();
#endif
            m_OverrideMaterial = new Material(m_SegmentationShader);
        }

        //Render all objects to our target RenderTexture using `overrideMaterial` to use our shader
        protected override void ExecutePass(ScriptableRenderContext renderContext, CommandBuffer cmd, HDCamera hdCamera, CullingResults cullingResult)
        {
            using (s_ExecuteMarker.Auto())
            {
                var result = CreateRendererListDesc(hdCamera, cullingResult, "FirstPass", 0, m_OverrideMaterial, layerMask);

                CoreUtils.SetRenderTarget(cmd, new RenderTargetIdentifier(targetTexture), ClearFlag.All);
                HDUtils.DrawRendererList(renderContext, cmd, RendererList.Create(result));
            }
        }

        public override void SetupMaterialProperties(MaterialPropertyBlock mpb, MeshRenderer meshRenderer, Labeling labeling, uint instanceId)
        {
            if (reassignIds)
            {
                //Almost all the code here interacts with shared state, so just use a simple lock for now.
                lock (this)
                {
                    if (m_Ids == null)
                        m_Ids = new Dictionary<uint, uint>();

                    if (!m_Ids.TryGetValue(instanceId, out var actualId))
                    {
                        actualId = (uint)m_NextObjectIndex * idStep + idStart;
                        m_Ids.Add(instanceId, actualId);
                        instanceId = actualId;
                        m_NextObjectIndex++;
                    }
                }
            }
            mpb.SetInt(SegmentationIdProperty, (int)instanceId);
#if SIMVIZ_DEBUG
            Debug.Log($"Assigning id. Frame {Time.frameCount} id {id}");
#endif
        }
    }
}
#endif
