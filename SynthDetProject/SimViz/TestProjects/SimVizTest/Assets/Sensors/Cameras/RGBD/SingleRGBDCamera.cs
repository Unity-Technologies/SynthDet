using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras
{
    /// <summary>
    /// Eye depth camera, it will be used by the main panoramic DepthCamera,
    /// it outputs the scene's depth information as (Red, Green, Blue, Depth)
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class SingleRGBDCamera : MonoBehaviour
    {
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

        [SerializeField]
        float _bands = 1;
        /// <summary>
        /// Depth bracketing, the depth bands will be rounded to this value
        /// </summary>
        public float bands
        {
            get { return _bands; }
            set
            {
                if (_bands != value)
                {
                    _bands = value;
                    UpdateMaterial();
                }
            }
        }

        void UpdateMaterial()
        {
            if (renderDepthCommandBufferMaterial != null)
            {
                renderDepthCommandBufferMaterial.SetFloat("_Bands", _bands);
            }
        }

        private void OnEnable()
        {
            RegisterCommandBuffer();
        }

        private void OnDisable()
        {
            UnregisterCommandBuffer();
        }
        
        private void OnPreCull()
        {
            Matrix4x4 p = GL.GetGPUProjectionMatrix(linkedCamera.projectionMatrix, false);// Unity flips its 'Y' vector depending on if its in VR, Editor view or game view etc... (facepalm)
            p[2, 3] = p[3, 2] = 0.0f;
            p[3, 3] = 1.0f;

            Matrix4x4 clipToWorld = Matrix4x4.Inverse(p * linkedCamera.worldToCameraMatrix) * Matrix4x4.TRS(new Vector3(0, 0, -p[2, 2]), Quaternion.identity, Vector3.one);
            renderDepthCommandBufferMaterial.SetMatrix("_ClipToWorld", clipToWorld);
        }

        protected void OnDestroy()
        {
            if (internalRenderTexture != null)
            {
                internalRenderTexture.Release();
                DestroyImmediate(internalRenderTexture);
            }

            if (renderDepthCommandBufferMaterial != null)
            {
                DestroyImmediate(renderDepthCommandBufferMaterial);
                renderDepthCommandBufferMaterial = null;
            }
        }

        RenderTexture internalRenderTexture = null;
        CommandBuffer renderDepthCommandBuffer;
        Material renderDepthCommandBufferMaterial = null;
        const CameraEvent renderDepthCommandBufferEvent = CameraEvent.AfterImageEffects;
        void RegisterCommandBuffer()
        {
            var targetDimensions = new Vector2Int(linkedCamera.pixelWidth, linkedCamera.pixelHeight);
            var targetFormat = RenderTextureFormat.ARGB32;
            if (linkedCamera.targetTexture != null)
            {
                targetFormat = linkedCamera.targetTexture.format;
            }

            if (internalRenderTexture!= null &&
                (internalRenderTexture.width != targetDimensions.x || internalRenderTexture.height != targetDimensions.y || 
                 internalRenderTexture.format == targetFormat))
            {
                internalRenderTexture.Release();
                DestroyImmediate(internalRenderTexture);
                internalRenderTexture = null;
            }

            if (internalRenderTexture == null)
            {
                internalRenderTexture = new RenderTexture(
                    targetDimensions.x,
                    targetDimensions.y,
                    24,
                    targetFormat,
                    RenderTextureReadWrite.Linear);
                renderDepthCommandBuffer?.Dispose();
                renderDepthCommandBuffer = null;
            }

            if (renderDepthCommandBufferMaterial == null)
            {
                renderDepthCommandBufferMaterial = new Material(Shader.Find("Hidden/Syncity/Cameras/RGBD"));
            }
            UpdateMaterial();
            if (renderDepthCommandBuffer == null)
            {
                renderDepthCommandBuffer = new CommandBuffer()
                {
                    name = this.GetType().Name + " " + nameof(renderDepthCommandBuffer)
                };
                renderDepthCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, internalRenderTexture);
                renderDepthCommandBuffer.Blit(internalRenderTexture, BuiltinRenderTextureType.CameraTarget, renderDepthCommandBufferMaterial);
            }

            linkedCamera.AddCommandBuffer(renderDepthCommandBufferEvent, renderDepthCommandBuffer);
        }        
        void UnregisterCommandBuffer()
        {
            linkedCamera.RemoveCommandBuffer(renderDepthCommandBufferEvent, renderDepthCommandBuffer);
        }
    }
}