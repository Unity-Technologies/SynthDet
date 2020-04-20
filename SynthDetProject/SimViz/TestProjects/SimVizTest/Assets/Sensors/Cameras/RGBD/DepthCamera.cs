using System.Collections.Generic;
using Syncity.Cameras.DepthCameraOutputs;
using UnityEngine;

namespace Syncity.Cameras
{
    /// <summary>
    /// Panoramic camera that will render as an eye depth camera
    /// </summary>
    [RequireComponent(typeof(PanoramicCamera))]
    [DisallowMultipleComponent]
    public class DepthCamera : MonoBehaviour, ISubCameraModifier
    {
        PanoramicCamera _linkedPanoramicCamera;
        public PanoramicCamera linkedPanoramicCamera
        {
            get
            {
                if (_linkedPanoramicCamera == null)
                {
                    _linkedPanoramicCamera = GetComponent<PanoramicCamera>();
                }
                return _linkedPanoramicCamera;
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
                    foreach (var rgbdCamera in rgbdCameras.Values)
                    {
                        rgbdCamera.bands = _bands;
                    }
                }
            }
        }

        /// <summary>
        /// Type to define the type of depth output:
        /// </summary>
        public enum TOutput
        {
            /// <summary>
            /// (Red, Green, Blue, Depth)
            /// </summary>
            Raw,
            /// <summary>
            /// (Depth, Depth, Depth, 1)
            /// </summary>
            Greyscale,
            /// <summary>
            /// Gradient between two custom colors
            /// </summary>
            ColorCoded
        }

        [SerializeField]
        TOutput _output = TOutput.Raw;
        /// <summary>
        /// Type of depth camera output
        /// </summary>
        public TOutput output
        {
            get
            {
                return _output;                
            }
            set
            {
                if (value != _output)
                {
                    this.enabled = false;
                    _output = value;
                    this.enabled = true;
                }
            }
        }

        void RefreshOutput()
        {
            var prevOutputs = GetComponents<DepthCameraOutput>();
            foreach (var ouput in prevOutputs)
            {
                DestroyImmediate(ouput);
            }

            DepthCameraOutput newBehaviour = null;
            switch (_output)
            {
                case TOutput.Greyscale:
                    newBehaviour = gameObject.AddComponent<GreyscaleOutput>();
                    break;
                case TOutput.ColorCoded:
                    newBehaviour = gameObject.AddComponent<ColorCodedOutput>();
                    break;
            }

            if (newBehaviour != null)
            {
#if !DEBUG_DEPTH_CAMERA
                newBehaviour.hideFlags = HideFlags.HideInInspector;
#endif
            }
        }

        DepthCameraOutput[] _outputs = null;
        DepthCameraOutput[] outputs
        {
            get
            {
                if (_outputs == null)
                {
                    _outputs = GetComponents<DepthCameraOutput>();
                }
                return _outputs;                 
            }
        }
        protected void OnEnable()
        {
            RefreshOutput();
            linkedPanoramicCamera.RegisterSubcameraModifier(this);
        }

        protected void OnDisable()
        {
            linkedPanoramicCamera.UnregisterSubcameraModifier(this);
            _outputs = null;
        }

        protected void OnDestroy()
        {
            foreach (var rgbdCamera in rgbdCameras.Values)
            {
                DestroyImmediate(rgbdCamera);
            }
            rgbdCameras.Clear();
        }

        readonly Dictionary<Camera, SingleRGBDCamera> rgbdCameras = new Dictionary<Camera, SingleRGBDCamera>();
        /// <summary>
        ///  ISubCameraModifier implementation, will be called by PanoramicCamera, do not use it otherwise
        /// </summary>
        public void RegisterCommandBuffersMainCamera(Camera c)
        {
            foreach (var output in outputs)
            {
                output.RegisterCommandBuffersMainCamera(c);
            }            
        }
        
        /// <summary>
        ///  ISubCameraModifier implementation, will be called by PanoramicCamera, do not use it otherwise
        /// </summary>
        public void RegisterCommandBuffersSubCamera(Camera c)
        {
            var rgbCamera = c.gameObject.AddComponent<SingleRGBDCamera>();
            rgbdCameras.Add(c, rgbCamera);
            rgbCamera.bands = bands;
        }        
        /// <summary>
        ///  ISubCameraModifier implementation, will be called by PanoramicCamera, do not use it otherwise
        /// </summary>
        public void UnregisterCommandBuffersSubCamera(Camera c)
        {
            DestroyImmediate(rgbdCameras[c]);
            rgbdCameras.Remove(c);
        }

        /// <summary>
        ///  ISubCameraModifier implementation, will be called by PanoramicCamera, do not use it otherwise
        /// </summary>
        public void UnregisterCommandBuffersMainCamera(Camera c)
        {
            foreach (var output in outputs)
            {
                output.UnregisterCommandBuffersMainCamera(c);
            }            
        }
    }
}