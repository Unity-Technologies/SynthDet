using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras
{
    /// <summary>
    /// Any component that would need to modify the subcameras of a panoramic camera should implement this
    /// interface
    /// </summary>
    public interface ISubCameraModifier
    {
        /// <summary>
        /// The component can register the CommandBuffers it needs to the main camera
        /// </summary>
        /// <param name="c">Main panoramic camera</param>
        void RegisterCommandBuffersMainCamera(Camera c);
        /// <summary>
        /// The component can register the CommandBuffers it needs to subcamera passed as parameter
        /// </summary>
        /// <param name="c">Subcamera expecting CommandBuffers</param>
        void RegisterCommandBuffersSubCamera(Camera c);
        /// <summary>
        /// The component should proceed to unregister the CommandBuffers previously registered to
        /// the main camera
        /// </summary>
        /// <param name="c">Main panoramic camera</param>
        void UnregisterCommandBuffersSubCamera(Camera c);
        /// <summary>
        /// The component should proceed to unregister the CommandBuffers previously registered to
        /// the subcamera
        /// </summary>
        /// <param name="c">Subcamera expecting the CommandsBuffers to be unregistered</param>
        void UnregisterCommandBuffersMainCamera(Camera c);
    }

    /// <summary>
    /// Fisheye type camera supporting up to 360 horizontal degrees and 180 horizontal degrees field of view 
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteInEditMode, DisallowMultipleComponent]
    public class PanoramicCamera : MonoBehaviour
    {
        [SerializeField]
        Camera _linkedCamera;
        Camera linkedCamera
        {
            get
            {
                if (_linkedCamera == null)
                {
                    _linkedCamera = GetComponent<Camera>();
                }
                return _linkedCamera;
            }
        }
        
        #region transferred properties
        /// <summary>
        /// How the camera clears the background.
        /// </summary>
        public CameraClearFlags clearFlags
        {
            get { return linkedCamera.clearFlags; }
            set
            {
                if (linkedCamera.clearFlags != value)
                {
                    linkedCamera.clearFlags = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// The color with which the screen will be cleared.
        /// </summary>
        public Color backgroundColor
        {
            get { return linkedCamera.backgroundColor; }
            set
            {
                if (linkedCamera.backgroundColor != value)
                {
                    linkedCamera.backgroundColor = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// The near clipping plane distance.
        /// </summary>
        public float nearClipPlane
        {
            get { return linkedCamera.nearClipPlane; }
            set
            {
                if (linkedCamera.nearClipPlane != value)
                {
                    linkedCamera.nearClipPlane = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// The far clipping plane distance.
        /// </summary>
        public float farClipPlane
        {
            get { return linkedCamera.farClipPlane; }
            set
            {
                if (linkedCamera.farClipPlane != value)
                {
                    linkedCamera.farClipPlane = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// Camera's depth in the camera rendering order.
        /// </summary>
        public float depth 
        {
            get { return linkedCamera.depth; }
            set
            {
                if (linkedCamera.depth != value)
                {
                    linkedCamera.depth = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// The rendering path that should be used, if possible.
        /// </summary>
        public RenderingPath renderingPath 
        {
            get { return linkedCamera.renderingPath; }
            set
            {
                if (linkedCamera.renderingPath != value)
                {
                    linkedCamera.renderingPath = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// Whether or not the Camera will use occlusion culling during rendering.
        /// </summary>
        public bool useOcclusionCulling 
        {
            get { return linkedCamera.useOcclusionCulling; }
            set
            {
                if (linkedCamera.useOcclusionCulling != value)
                {
                    linkedCamera.useOcclusionCulling = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// High dynamic range rendering.
        /// </summary>
        public bool allowHDR 
        {
            get { return linkedCamera.allowHDR; }
            set
            {
                if (linkedCamera.allowHDR != value)
                {
                    linkedCamera.allowHDR = value;
                    RefreshSubCameras();
                }
            }
        }
        /// <summary>
        /// MSAA rendering.
        /// </summary>
        public bool allowMSAA 
        {
            get { return linkedCamera.allowMSAA; }
            set
            {
                if (linkedCamera.allowMSAA != value)
                {
                    linkedCamera.allowMSAA = value;
                    RefreshSubCameras();
                }
                
            }
        }
        #endregion

        [SerializeField]
        int _cullingMask = -1;
        /// <summary>
        /// This is used to render parts of the scene selectively.
        /// </summary>
        public int cullingMask
        {
            get { return _cullingMask; }
            set
            {
                if (_cullingMask != value)
                {
                    _cullingMask = value;
                    RefreshSubCameras();
                }
            }
        }

        [SerializeField]
        public bool debugCamera = false;
        const string subCameraPrefix = "SubCamera ";
        /// <summary>
        /// Enumeration type for the subcameras on a 360 by 180 camera
        /// </summary>
        public enum TSubCamera { Front, Left, Right, Back, Top, Bottom };
        Camera[] _subCameras = null;
        /// <summary>
        /// Subcameras used to render the whole panoramic camera
        /// </summary>
        public Camera[] subCameras
        {
            get
            {
                if (_subCameras == null)
                {
                    var rots = new []
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(0f, -90f, 0f),
                        new Vector3(0f, 90f, 0f),
                        new Vector3(0f, 180f, 0f),
                        new Vector3(-90f, 0f, 0f),
                        new Vector3(90f, 0f, 0f),
                    };

                    _subCameras = new UnityEngine.Camera[rots.Length];
                    for (var i = 0; i < _subCameras.Length; i++)
                    {
                        var go = new GameObject(subCameraPrefix + ((TSubCamera)i).ToString());
                        var cam = go.AddComponent<UnityEngine.Camera>();

                        go.transform.SetParent(this.transform, false);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localScale = Vector3.one;
                        go.transform.localEulerAngles = rots[i];
                        if (!debugCamera)
                            go.hideFlags |= HideFlags.HideInHierarchy;
                            
                        go.hideFlags |= HideFlags.DontSave;
                        
                        _subCameras[i] = cam;
                        
                        subcameraModifiers.ForEach(scm =>
                        {
                            scm.RegisterCommandBuffersSubCamera(cam);
                        });
                    }
                    RefreshSubCameras();
                }
                return _subCameras;
            }
        }

        [SerializeField]
        float _horizontalFieldOfView = 360;
        /// <summary>
        /// Horizontal field of view in degrees
        /// </summary>
        public float horizontalFieldOfView
        {
            get { return _horizontalFieldOfView; }
            set
            {
                if (_horizontalFieldOfView != value)
                {
                    _horizontalFieldOfView = value;
                    stitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                    RefreshSubCameras();
                }
            }
        }
        [SerializeField]
        float _verticalFieldOfView = 180;
        /// <summary>
        /// Vertical field of view in degrees
        /// </summary>
        public float verticalFieldOfView
        {
            get { return _verticalFieldOfView; }
            set
            {
                if (_verticalFieldOfView != value)
                {
                    _verticalFieldOfView = value;
                    stitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                    RefreshSubCameras();
                }
            }
        }

        [SerializeField]
        Vector2 _resolutionMultiplier = Vector2.one;
        /// <summary>
        /// The resolution of each subcamera will be the resolution of the main panoramic camera
        /// multiplied by this factor
        /// </summary>
        public Vector2 resolutionMultiplier
        {
            get { return _resolutionMultiplier; }
            set
            {
                if (_resolutionMultiplier != value)
                {
                    _resolutionMultiplier = value;
                    RefreshSubCameras();
                }
            }
        }

        /// <summary>
        /// Destination render texture.
        /// </summary>
        public RenderTexture targetTexture
        {
            get { return linkedCamera.targetTexture; }
            set
            {
                if (linkedCamera.targetTexture != value)
                {
                    linkedCamera.targetTexture = value;
                    RefreshSubCameras();
                }
            }
        }

        protected void OnEnable()
        {
            linkedCamera.cullingMask = 0;
            RefreshSubCameras();
            RegisterCommandBuffers();
        }
        protected void OnDisable()
        {
            UnregisterCommandBuffers();
            linkedCamera.cullingMask = cullingMask;
            if (_subCameras != null)
            {
                foreach (var camera in subCameras)
                {
                    subcameraModifiers.ForEach(scm =>
                    {
                        scm.UnregisterCommandBuffersMainCamera(camera);
                    });

                    camera.gameObject.SetActive(false);
                }
            }
        }

        protected void OnDestroy()
        {
            // clean up all cameras, not only the ones in _cameras
            var aux = new List<Camera>();
            foreach (Transform child in this.transform)
            {
                if (child.gameObject.name.StartsWith(subCameraPrefix))
                {
                    var c = child.GetComponent<Camera>();
                    if (c != null)
                    {
                        aux.Add(c);
                    }
                }
            }

            var toDestroy = aux.ToArray();

            foreach (var camera in toDestroy)
            {
                if (camera == null) continue;

                if (camera.targetTexture != null)
                {
                    var rt = camera.targetTexture;
                    camera.targetTexture = null;
                    rt.Release();
                    DestroyImmediate(rt);
                }

                camera.gameObject.SetActive(false);
                subcameraModifiers.ForEach(scm =>
                {
                    scm.UnregisterCommandBuffersSubCamera(camera);
                });
                DestroyImmediate(camera.gameObject);
            }
            _subCameras = null;
            if (_stitchMaterial != null)
            {
                DestroyImmediate(_stitchMaterial);
            }
            subcameraModifiers.ForEach(scm =>
            {
                scm.UnregisterCommandBuffersMainCamera(linkedCamera);
            });
            UnregisterCommandBuffers();

            linkedCamera.hideFlags = HideFlags.None;
        }

        void RefreshSubCameras()
        {
            for (int i = 0; i < subCameras.Length; i++)
            {
                RefreshSubCamera((TSubCamera)i);                    
            }
        }
        void RefreshSubCamera(TSubCamera subCamera)
        {
            if (!this.enabled) return;

            Camera cam = subCameras[(int) subCamera];
           
            switch (subCamera)
            {
                case TSubCamera.Left:
                    cam.gameObject.SetActive(_horizontalFieldOfView > 90f);
                    break;
                case TSubCamera.Right:
                    cam.gameObject.SetActive(_horizontalFieldOfView > 90f);
                    break;
                case TSubCamera.Back:
                    cam.gameObject.SetActive(_horizontalFieldOfView > 270f);
                    break;
                case TSubCamera.Top:
                    cam.gameObject.SetActive(_verticalFieldOfView > 68f);
                    break;
                case TSubCamera.Bottom:
                    cam.gameObject.SetActive(_verticalFieldOfView > 68f);
                    break;
                case TSubCamera.Front:
                default:
                    cam.gameObject.SetActive(true);
                    break;
            }

            if (cam.isActiveAndEnabled)
            {
                var targetDimensions = new Vector2Int(
                    Mathf.CeilToInt(resolutionMultiplier.x * linkedCamera.pixelWidth),
                    Mathf.CeilToInt(resolutionMultiplier.y * linkedCamera.pixelHeight));
                var targetDepth = 24;
                var targetFormat = RenderTextureFormat.ARGB32;
                var targetFilterMode = FilterMode.Bilinear;

                if (targetTexture != null)
                {
                    targetDimensions = new Vector2Int(
                        Mathf.CeilToInt(resolutionMultiplier.x * targetTexture.width),
                        Mathf.CeilToInt(resolutionMultiplier.y * targetTexture.height));
                    targetDepth = targetTexture.depth;
                    targetFormat = targetTexture.format;
                    targetFilterMode = targetTexture.filterMode;
                }

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

                    var textureName = "_" + cam.gameObject.name.Substring(subCameraPrefix.Length);
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

            cam.clearFlags = linkedCamera.clearFlags;
            cam.backgroundColor = linkedCamera.backgroundColor;
            cam.cullingMask = linkedCamera.cullingMask;

            cam.nearClipPlane = linkedCamera.nearClipPlane;
            cam.farClipPlane = linkedCamera.farClipPlane;
                
            cam.depth = linkedCamera.depth;
            cam.renderingPath = linkedCamera.renderingPath;
                
            cam.useOcclusionCulling = linkedCamera.useOcclusionCulling;
            cam.allowHDR = linkedCamera.allowHDR;
            cam.allowMSAA = linkedCamera.allowMSAA;

            cam.fieldOfView = 90f;
            cam.aspect = 1;
            cam.layerCullSpherical = true;
            cam.cullingMask = cullingMask;

            subcameraModifiers.ForEach(scm =>
            {
                scm.UnregisterCommandBuffersSubCamera(cam);
                scm.RegisterCommandBuffersSubCamera(cam);
            });
        }

        [SerializeField]
        Material _stitchMaterial = null;
        Material stitchMaterial
        {
            get
            {
                if (_stitchMaterial == null)
                {
                    _stitchMaterial = new Material(Shader.Find("Hidden/Syncity/Cameras/Stitch"));
                    _stitchMaterial.SetVector("_FOV", new Vector2(horizontalFieldOfView / 360f, verticalFieldOfView / 180f));
                }
                return _stitchMaterial;
            }
        }

        CameraEvent stitchCameraEvent => CameraEvent.AfterImageEffects;

        CommandBuffer _stitchCommandBuffer = null;
        CommandBuffer stitchCommandBuffer
        {
            get
            {
                if (_stitchCommandBuffer == null)
                {
                    _stitchCommandBuffer = new CommandBuffer();
                    _stitchCommandBuffer.name = nameof(PanoramicCamera) + " Stitch";                    
                }

                return _stitchCommandBuffer;
            }
        }
        void RegisterCommandBuffers()
        {
            UnregisterCommandBuffers();
 
            stitchCommandBuffer.Clear();
            stitchCommandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, stitchMaterial);
            linkedCamera.AddCommandBuffer(stitchCameraEvent, stitchCommandBuffer);

            subcameraModifiers.ForEach(scm => scm.RegisterCommandBuffersMainCamera(linkedCamera));
        }
        void UnregisterCommandBuffers()
        {
            subcameraModifiers.ForEach(scm => scm.UnregisterCommandBuffersMainCamera(linkedCamera));
            if (_stitchCommandBuffer != null)
            {
                linkedCamera.RemoveCommandBuffer(stitchCameraEvent, stitchCommandBuffer);
            }
        }

        readonly List<ISubCameraModifier> subcameraModifiers = new List<ISubCameraModifier>();
        /// <summary>
        /// Registers a new component that needs to modify every subcamera, it must implement the
        /// ISubcameraModifier interface.
        /// 
        /// The methods in the interface will be called when needed 
        /// </summary>
        /// <param name="sender">The new component to be registered</param>
        public void RegisterSubcameraModifier(ISubCameraModifier sender)
        {
            subcameraModifiers.Add(sender);
            if (_subCameras != null)
            {
                if (_stitchCommandBuffer != null)
                {
                    sender.RegisterCommandBuffersMainCamera(linkedCamera);
                }

                foreach (var camera in subCameras)
                {
                    sender.RegisterCommandBuffersSubCamera(camera);
                }
            }
        }
        /// <summary>
        /// Unregisters a previously registered component
        /// </summary>
        /// <param name="sender">The new component to be unregistered</param>
        public void UnregisterSubcameraModifier(ISubCameraModifier sender)
        {
            subcameraModifiers.Remove(sender);
            if (_subCameras != null)
            {
                foreach (var camera in subCameras)
                {
                    sender.UnregisterCommandBuffersSubCamera(camera);
                }
                sender.UnregisterCommandBuffersMainCamera(linkedCamera);
            }
        }
    }
}