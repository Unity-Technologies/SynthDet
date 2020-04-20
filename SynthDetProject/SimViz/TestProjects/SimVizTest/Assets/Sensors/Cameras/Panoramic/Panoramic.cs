using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if HDRP_PRESENT
using UnityEngine.Rendering.HighDefinition;
#endif
using UnityEngine.Rendering;


namespace SimViz.Sensors.Cameras
{
    /// <summary>
    /// Fisheye type camera supporting up to 360 horizontal degrees and 180 horizontal degrees field of view 
    /// </summary>
    [ExecuteInEditMode, DisallowMultipleComponent]
    public class Panoramic : MonoBehaviour
    {
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float nearClipPlane;

        /// <summary>
        /// The far clipping plane distance.
        /// </summary>
        public float farClipPlane;

        /// <summary>
        /// Camera's depth in the camera rendering order.
        /// </summary>
        public float depth;

        /// <summary>
        /// The rendering path that should be used, if possible.
        /// </summary>
        public RenderingPath renderingPath;

        /// <summary>
        /// Whether or not the Camera will use occlusion culling during rendering.
        /// </summary>
        public bool useOcclusionCulling;

        /// <summary>
        /// High dynamic range rendering.
        /// </summary>
        public bool allowHdr;

        /// <summary>
        /// MSAA rendering.
        /// </summary>
        public bool allowMsaa;

        /// <summary>
        /// This is used to render parts of the scene selectively.
        /// </summary>
        public int cullingMask = -1;

        [SerializeField]
        public bool debugCamera = false;

        /// <summary>
        /// Type to define the type of depth output:
        /// </summary>
        public enum OutputFormat
        {
            /// <summary>
            /// (Red, Green, Blue, Depth)
            /// </summary>
            Raw,
            /// <summary>
            /// (Depth, Depth, Depth, 1)
            /// </summary>
            Depth
        }

        [SerializeField]
        OutputFormat m_OutputFormat = OutputFormat.Raw;
        /// <summary>
        /// Type of depth camera output
        /// </summary>
        public OutputFormat outputFormat
        {
            get
            {
                return m_OutputFormat;                
            }
            set
            {
                //Check value because disabling and enabling is expensive
                if (value != m_OutputFormat)
                {
                    this.enabled = false;
                    m_OutputFormat = value;
                    this.enabled = true;
                }
            }
        }

        const string k_SubCameraPrefix = "SubCamera ";
        /// <summary>
        /// Enumeration type for the subcameras on a 360 by 180 camera
        /// </summary>
        public enum SubCameraDirection { Front, Left, Right, Back, Top, Bottom };
        
        private static readonly Dictionary<SubCameraDirection, Vector3> k_CameraRotations = new Dictionary<SubCameraDirection, Vector3>()
        {
            { SubCameraDirection.Front, new Vector3(0f, 0f, 0f)},
            { SubCameraDirection.Left, new Vector3(0f, -90f, 0f)},
            { SubCameraDirection.Right, new Vector3(0f, 90f, 0f)},
            { SubCameraDirection.Back, new Vector3(0f, 180f, 0f)},
            { SubCameraDirection.Top, new Vector3(-90f, 0f, 0f)},
            { SubCameraDirection.Bottom, new Vector3(90f, 0f, 0f)}
        };
        Dictionary<SubCameraDirection, Camera> m_SubCameras = null;
        /// <summary>
        /// Subcameras used to render the whole panoramic camera
        /// </summary>
        private Dictionary<SubCameraDirection, Camera> subCameras
        {
            get
            {
                if (m_SubCameras == null && Application.isPlaying)
                {
                    
                    m_SubCameras = new Dictionary<SubCameraDirection, Camera>();
                    foreach( var kvp in k_CameraRotations)
                    {
                        var go = new GameObject(k_SubCameraPrefix + kvp.Key);
                        var cam = go.AddComponent<Camera>();

                        go.transform.SetParent(transform, false);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localScale = Vector3.one;
                        go.transform.localEulerAngles = kvp.Value;
                        if (!debugCamera)
                            go.hideFlags |= HideFlags.HideInHierarchy;
                        
                        go.hideFlags |= HideFlags.DontSave;
                        
                        m_SubCameras[kvp.Key] = cam;
                    }
                    RefreshSubCameras();
                }
                return m_SubCameras;
            }
        }

        [SerializeField]
        float m_HorizontalFieldOfView = 360;
        /// <summary>
        /// Horizontal field of view in degrees
        /// </summary>
        public float horizontalFieldOfView
        {
            get { return m_HorizontalFieldOfView; }
            set
            {
                if (m_HorizontalFieldOfView != value)
                {
                    m_HorizontalFieldOfView = value;
                    stitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                    RefreshSubCameras();
                }
            }
        }
        [SerializeField]
        float m_VerticalFieldOfView = 180;
        /// <summary>
        /// Vertical field of view in degrees
        /// </summary>
        public float verticalFieldOfView
        {
            get { return m_VerticalFieldOfView; }
            set
            {
                if (m_VerticalFieldOfView != value)
                {
                    m_VerticalFieldOfView = value;
                    stitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                    RefreshSubCameras();
                }
            }
        }

        [SerializeField]
        Vector2 m_ResolutionMultiplier = Vector2.one;
        /// <summary>
        /// The resolution of each subcamera will be the resolution of the main panoramic camera
        /// multiplied by this factor
        /// </summary>
        public Vector2 mResolutionMultiplier
        {
            get { return m_ResolutionMultiplier; }
            set
            {
                if (m_ResolutionMultiplier != value)
                {
                    m_ResolutionMultiplier = value;
                    RefreshSubCameras();
                }
            }
        }
        
        [SerializeField]
        public float depthMax = .2f;

        /// <summary>
        /// Destination render texture.
        /// </summary>
        public RenderTexture targetTexture;

        protected void OnEnable()
        {
            RefreshSubCameras();
            InitCommandBuffers();
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
        }

        protected void OnDisable()
        {
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
            if (m_SubCameras != null)
            {
                foreach (var cam in subCameras.Values)
                {
                    cam.gameObject.SetActive(false);
                }
            }
        }

        protected void OnDestroy()
        {
            // clean up all cameras, not only the ones in _cameras
            var aux = new List<Camera>();
            foreach (Transform child in transform)
            {
                if (child.gameObject.name.StartsWith(k_SubCameraPrefix))
                {
                    var c = child.GetComponent<Camera>();
                    if (c != null)
                    {
                        aux.Add(c);
                    }
                }
            }

            foreach (var cam in aux)
            {
                if (cam == null) continue;

                if (cam.targetTexture != null)
                {
                    var rt = cam.targetTexture;
                    cam.targetTexture = null;
                    rt.Release();
                    DestroyImmediate(rt);
                }

                cam.gameObject.SetActive(false);
                DestroyImmediate(cam.gameObject);
            }
            m_SubCameras = null;
            if (m_StitchMaterial != null)
            {
                DestroyImmediate(m_StitchMaterial);
            }
        }

        void RefreshSubCameras()
        {
            if (subCameras == null)
                return;

            foreach (var subCamera in subCameras.Keys)
            {
                RefreshSubCamera(subCamera);                    
            }
        }
        void RefreshSubCamera(SubCameraDirection subCameraDirection)
        {
            if (!enabled) return;

            if (targetTexture == null) return;

            Camera cam = subCameras[subCameraDirection];
           
            switch (subCameraDirection)
            {
                case SubCameraDirection.Left:
                    cam.gameObject.SetActive(m_HorizontalFieldOfView > 90f);
                    break;
                case SubCameraDirection.Right:
                    cam.gameObject.SetActive(m_HorizontalFieldOfView > 90f);
                    break;
                case SubCameraDirection.Back:
                    cam.gameObject.SetActive(m_HorizontalFieldOfView > 270f);
                    break;
                case SubCameraDirection.Top:
                    cam.gameObject.SetActive(m_VerticalFieldOfView > 68f);
                    break;
                case SubCameraDirection.Bottom:
                    cam.gameObject.SetActive(m_VerticalFieldOfView > 68f);
                    break;
                case SubCameraDirection.Front:
                default:
                    cam.gameObject.SetActive(true);
                    break;
            }

            if (cam.isActiveAndEnabled)
            {
                var targetDimensions = new Vector2Int(
                    Mathf.CeilToInt(mResolutionMultiplier.x * targetTexture.width),
                    Mathf.CeilToInt(mResolutionMultiplier.y * targetTexture.height));
                int targetDepth = 0;
                RenderTextureFormat targetFormat;

                switch (outputFormat)
                {
                    case OutputFormat.Depth:
                        targetFormat = RenderTextureFormat.Depth;
                        stitchMaterial.SetVector("_DepthRange", new Vector4(0f, depthMax));
                        stitchMaterial.EnableKeyword("DEPTH");
                        break;
                    case OutputFormat.Raw:
                        targetFormat = RenderTextureFormat.ARGB32;
                        stitchMaterial.DisableKeyword("DEPTH");
                        break;
                    default:
                        throw new NotSupportedException("Unsupported TOutput");
                }
                
                var targetFilterMode = FilterMode.Bilinear;

                if (cam.targetTexture)
                {
                    if (cam.targetTexture.width != targetDimensions.x ||
                        cam.targetTexture.height != targetDimensions.y ||
                        targetDepth != cam.targetTexture.depth ||
                        targetFormat != cam.targetTexture.format ||
                        targetFilterMode != cam.targetTexture.filterMode)
                    {
                        var rt = cam.targetTexture;
                        cam.targetTexture = null;
                        rt.Release();
                        DestroyImmediate(rt);
                    }
                }

                if (cam.targetTexture == null)
                {
                    RenderTexture texture = new RenderTexture(
                        targetDimensions.x,
                        targetDimensions.y,
                        targetDepth, targetFormat,
                        RenderTextureReadWrite.Linear) 
                    {
                        filterMode = targetFilterMode                        
                    };
                    texture.Create();
                    cam.targetTexture = texture;

                    var textureName = "_" + cam.gameObject.name.Substring(k_SubCameraPrefix.Length);
                    stitchMaterial.SetTexture(textureName, cam.targetTexture);
                }
            }
            else
            {
                if (cam.targetTexture != null)
                {
                    var rt = cam.targetTexture;
                    cam.targetTexture = null;
                    rt.Release();
                    DestroyImmediate(rt);
                }
            }

            cam.clearFlags = clearFlags;
            cam.backgroundColor = backgroundColor;

            cam.nearClipPlane = nearClipPlane;
            cam.farClipPlane = farClipPlane;
                
            cam.depth = depth;
            cam.renderingPath = renderingPath;
                
            cam.useOcclusionCulling = useOcclusionCulling;
            cam.allowHDR = allowHdr;
            cam.allowMSAA = allowMsaa;

            cam.fieldOfView = 90f;
            cam.aspect = 1;
            cam.layerCullSpherical = true;
            cam.cullingMask = cullingMask;

#if HDRP_PRESENT
            var cameraData = cam.GetComponent<HDAdditionalCameraData>() ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
            cameraData.volumeLayerMask = LayerMask.NameToLayer("Everything");
#endif
        }

        [NonSerialized]
        Material m_StitchMaterial = null;
        Material stitchMaterial
        {
            get
            {
                if (m_StitchMaterial == null)
                {
                    m_StitchMaterial = new Material(Shader.Find("Hidden/SimViz/Cameras/Panoramic"));
                    m_StitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                }
                return m_StitchMaterial;
            }
        }

        CommandBuffer m_StitchCommandBuffer;

        void InitCommandBuffers()
        {
            m_StitchCommandBuffer = new CommandBuffer();
            m_StitchCommandBuffer.name = nameof(Panoramic) + " Stitch";
            
            m_StitchCommandBuffer.SetRenderTarget(targetTexture);
            m_StitchCommandBuffer.ClearRenderTarget(true, true, Color.red, 0.0f);
            CoreUtils.DrawFullScreen(m_StitchCommandBuffer, stitchMaterial, targetTexture);
        }

        private void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!this.isActiveAndEnabled)
                Debug.LogError("OnEndFrameRendering called on inactive Panoramic");

            //OnEndFrameRendering will be called separately between the main camera and the sub-cameras.
            //Using OnEndFrameRendering after the main camera causes issues with UGUI, so be sure to only render after our sub-cameras.
            if (subCameras == null || subCameras.Count == 0 || !cameras.Contains(subCameras[SubCameraDirection.Front]))
                return;
            
            context.ExecuteCommandBuffer(m_StitchCommandBuffer);
            context.Submit();
        }
    }
}