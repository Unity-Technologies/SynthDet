using Syncity.Sensors;
using UnityEngine;

namespace Syncity.Cameras
{
    /// <summary>
    /// Eye depth camera, that instead of CommandBuffers will use an unlit shader to output the scene's depth
    /// information as a [0..1) float value packed in the 4 channels.
    ///
    /// It is used by the lidar component.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class SingleTrueDepthCamera : MonoBehaviour
    {
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
        /// Depth bracketing, the depth bands will be rounded to this value
        /// </summary>
        public float bands = 0;
        /// <summary>
        /// Random noise to simulate measuring errors on the devices
        /// </summary>
        public INoiseGenerator<float> noiseGenerator = null;

        Shader _unlitShader;
        Shader unlitShader => _unlitShader ? _unlitShader : (_unlitShader = Shader.Find("Hidden/Syncity/Cameras/TrueDepth"));
        
        private void OnEnable()
        {
            linkedCamera.SetReplacementShader(unlitShader, null);
            linkedCamera.clearFlags = CameraClearFlags.Color;
            linkedCamera.backgroundColor = new Color(0,0,0,0);
        }

        private void OnDisable()
        {
            linkedCamera.ResetReplacementShader();
        }
        
        private void OnPreCull()
        {
            Shader.SetGlobalFloat("_Bands", bands);
            Shader.SetGlobalFloat("_Noise", noiseGenerator?.Generate(0) ?? 0);
            Shader.SetGlobalFloat("_Random", UnityEngine.Random.value);
        }
    }
}