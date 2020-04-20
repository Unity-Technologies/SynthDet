using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using Syncity.Sensors;
using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class Radar : MonoBehaviour
    {
        #if DEBUG_RADAR
        [ContextMenu("Save output")]
        void SaveOutput()
        {
            RenderTexture.active = output;

            var tex = new Texture2D(output.width, output.height);
            RenderTexture.active = output;
            tex.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(nameof(output) + ".png", tex.EncodeToPNG());
            RenderTexture.active = null;
        }
        #endif
        Camera _linkedCamera;
        public Camera linkedCamera
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

        /// <summary>
        /// This is used to render parts of the scene selectively.
        /// </summary>
        public int cullingMask
        {
            get { return linkedCamera.cullingMask; }
            set { linkedCamera.cullingMask = value; }
        }
        /// <summary>
        /// The far clipping plane distance.
        /// </summary>
        public float range
        {
            get { return linkedCamera.farClipPlane; }
            set { linkedCamera.farClipPlane = value; }
        }
        /// <summary>
        /// Camera's depth in the camera rendering order.
        /// </summary>
        public float depth
        {
            get { return linkedCamera.depth; }
            set { linkedCamera.depth = value; }
        }

        /// <summary>
        /// Vertical angle for the radar (the horizontal angle depends on the output resolution aspect ratio)
        /// </summary>
        public float verticalfieldOfView
        {
            get { return linkedCamera.fieldOfView; }
            set { linkedCamera.fieldOfView = value; }
        }
        [SerializeField]
        float _minIntensity = 0.1f;
        /// <summary>
        /// Any intensity value bellow minIntensity will be ignored
        /// </summary>
        public float minIntensity
        {
            get { return _minIntensity; }
            set
            {
                _minIntensity = Math.Max(0.001f, Math.Min(1, value));
            }
        }

        public RenderTexture output;
        /// <summary>
        /// Event raised when the output RenderTexture is created
        /// </summary>
        public UnityEventRenderTexture onOutput;

        RenderTexture internalRenderTexture;
        RenderTexture targetTexture;

        void OnEnable()
        {
            linkedCamera.nearClipPlane = 0.01f;
            linkedCamera.clearFlags = CameraClearFlags.Color;
            linkedCamera.backgroundColor = Color.black;
            linkedCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            linkedCamera.renderingPath = RenderingPath.DeferredShading;
            RegisterCommandBuffers();
        }

        const string shaderName = "Hidden/SynCity/Cameras/Radar";
        CommandBuffer commandBuffer;
        Material material;
        const CameraEvent cameraEvent = CameraEvent.AfterImageEffects;

        [SerializeField]
        Vector2Int _outputResolution = new Vector2Int(1000, 1000);
        /// <summary>
        /// Resolution for the output texture
        /// </summary>
        public Vector2Int outputResolution
        {
            get { return _outputResolution; }
            set
            {
                _outputResolution.x = Math.Max(1, value.x);
                _outputResolution.y = Math.Max(1, value.y);

                RefreshOutput();
            }
        }

        [SerializeField]
        Color _startColor = Color.yellow;
        /// <summary>
        /// Color for minimum intensity
        /// </summary>
        public Color startColor
        {
            get
            {
                return _startColor;
            }
            set
            {
                if (_startColor != value)
                {
                    _startColor = value;
                    UpdateMaterial();
                }
            }
        }
        [SerializeField]
        Color _endColor = Color.blue;
        /// <summary>
        /// Color for maximum intensity
        /// </summary>
        public Color endColor
        {
            get
            {
                return _endColor;
            }
            set
            {
                if (_endColor != value)
                {
                    _endColor = value;
                    UpdateMaterial();
                }
            }
        }
        void UpdateMaterial()
        {
            if (material != null)
            {
                material.SetColor("_StartColor", startColor);
                material.SetColor("_EndColor", endColor);
            }
        }
        
        [SerializeField]
        /// <summary>
        /// Random noise to simulate measuring errors on the devices
        /// </summary>
        ScriptableObject _noiseGenerator;
        /// <summary>
        /// Random noise to simulate measuring errors on the devices
        /// </summary>
        public INoiseGenerator<Vector3> noiseGenerator
        {
            get { return _noiseGenerator as INoiseGenerator<Vector3>; }
            set
            {
                if (_noiseGenerator != (ScriptableObject) value)
                {
                    _noiseGenerator = value as ScriptableObject; 
                }
            }
        }

        void RegisterCommandBuffers()
        {
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
            }

            if (commandBuffer == null)
            {
                commandBuffer = new CommandBuffer {name = this.GetType().Name};

                RefreshOutput();

                commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, internalRenderTexture);
                commandBuffer.Blit(internalRenderTexture, targetTexture, material);
                UpdateMaterial();
                commandBuffer.SetRandomWriteTarget(1, output);
            }

            linkedCamera.AddCommandBuffer(cameraEvent, commandBuffer);
        }

        void RefreshOutput()
        {
            if (internalRenderTexture != null &&
                (internalRenderTexture.width != outputResolution.x ||
                 internalRenderTexture.height != outputResolution.y))
            {
                internalRenderTexture.Release();
                DestroyImmediate(internalRenderTexture);
                internalRenderTexture = null;
            }
            if (internalRenderTexture == null)
            {
                internalRenderTexture = new RenderTexture(
                    outputResolution.x,
                    outputResolution.y,
                    24,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear);
                commandBuffer?.Blit(BuiltinRenderTextureType.CameraTarget, internalRenderTexture);
            }

            if (targetTexture != null &&
                (targetTexture.width != outputResolution.x ||
                 targetTexture.height != outputResolution.y))
            {
                linkedCamera.targetTexture = null;
                targetTexture.Release();
                DestroyImmediate(targetTexture);
                targetTexture = null;
            }
            if (targetTexture == null)
            {
                targetTexture = new RenderTexture(
                    outputResolution.x,
                    outputResolution.y,
                    24,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear)
                {
                    useMipMap = false, 
                    filterMode = FilterMode.Point, 
                    name = "Internal",
                };
                linkedCamera.targetTexture = targetTexture;

                commandBuffer?.Blit(internalRenderTexture, targetTexture, material);
            }

            if (output != null && (output.width != outputResolution.x || output.height != outputResolution.y))
            {
                output.Release();
                DestroyImmediate(output);
                output = null;
            }
            if (output == null)
            {
                output = new RenderTexture(
                    outputResolution.x,
                    outputResolution.y,
                    24,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear)
                {
                    useMipMap = false, 
                    filterMode = FilterMode.Point, 
                    name = "Cross range",
                    enableRandomWrite = true,
                };
                output.Create();
                onOutput?.Invoke(output);

                commandBuffer?.SetRandomWriteTarget(1, output);
            }
        }

        ComputeShader _cleanShader = null;
        ComputeShader cleanShader
        {
            get
            {
                if (_cleanShader == null)
                {
                    _cleanShader = Resources.Load<ComputeShader>("ClearRenderTexture");
                }
                return _cleanShader;
            }
        }
        /// <summary>
        /// Horizontal angle of the radar (depends on the vertical field of view and the output
        /// resolution aspect ratio)
        /// </summary>
        public float horizontalFieldOfView
        {
            get
            {
                float vFOVrad = linkedCamera.fieldOfView * Mathf.Deg2Rad;
                float cameraHeightAt1 = Mathf.Tan(vFOVrad * .5f);
                return Mathf.Atan(cameraHeightAt1 * linkedCamera.aspect) * 2f * Mathf.Rad2Deg;
            }
        }
        void Update()
        {
            if (material != null)
            {
                material.SetFloat("_MinIntensity", minIntensity);
                material.SetFloat("_HorizontalFOV", horizontalFieldOfView);
            }
            if (cleanShader != null && output != null)
            {
                cleanShader.SetTexture(0, "_Texture", output);
                cleanShader.Dispatch(0, output.width, output.height, 1);
            }
        }

        void OnDisable()
        {
            UnregisterCommandBuffers();
        }

        void UnregisterCommandBuffers()
        {
            if (commandBuffer != null)
            {
                linkedCamera.RemoveCommandBuffer(cameraEvent, commandBuffer);
            }
        }

        private void OnPreCull()
        {
            material.SetVector("_Noise", noiseGenerator?.Generate(Vector3.zero) ?? Vector3.zero);
            material.SetFloat("_Random", UnityEngine.Random.value);
        }

        void OnDestroy()
        {
            if (internalRenderTexture != null)
            {
                internalRenderTexture.Release();
                DestroyImmediate(internalRenderTexture);
            }
            if (targetTexture != null)
            {
                linkedCamera.targetTexture = null;
                targetTexture.Release();
                DestroyImmediate(targetTexture);
            }
            if (output != null)
            {
                linkedCamera.targetTexture = null;
                output.Release();
                DestroyImmediate(output);
            }

            commandBuffer?.Release();
        }
    }
}
