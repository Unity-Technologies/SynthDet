using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{
    public class CameraGrab : MonoBehaviour
    {

#pragma warning disable CS0649
        public Camera[]            _cameraSources;
#pragma warning restore CS0649

        public CaptureImageEncoder.ImageFormat _imageFormat = CaptureImageEncoder.ImageFormat.Jpg;
        public string              _customFilePath = "";
        public float               _screenCaptureInterval = 1.0f;
        public GraphicsFormat      _format = GraphicsFormat.R8G8B8A8_UNorm;

        private float              _elapsedTime;
        private string             _baseDirectory;
        private int                _sequence = 0;

        void Start()
        {
            _baseDirectory = Manager.Instance.GetDirectoryFor(DataCapturePaths.ScreenCapture, _customFilePath);
        }

        void Update()
        {
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime > _screenCaptureInterval)
            {
                _elapsedTime -= _screenCaptureInterval;

                for (var i = 0; i < _cameraSources.Length; i++)
                {
                    CaptureCamera.CaptureColorToFile(_cameraSources[i], _format, Path.Combine(_baseDirectory, _cameraSources[i].name+ "_" + _sequence + "." + _imageFormat.ToString().ToLower()), _imageFormat);
                }

                ++_sequence;
            }
        }

        void OnValidate()
        {
            // Automatically add the camera component if there is one on this game object.
            if (_cameraSources == null || _cameraSources.Length == 0)
            {
                var camera = GetComponent<Camera>();
                if (camera != null)
                {
                    if (_cameraSources == null)
                        _cameraSources = new Camera[1];
                    _cameraSources[0] = camera;
                }
            }

            // Ensure that the same camera hasn't been added twice.
            var map = new Dictionary<string, int>();
            var cameraWithNoRtCount = 0;
            if (_cameraSources != null)
            {
                for (var i = 0; i < _cameraSources.Length; ++i)
                {
                    var c = _cameraSources[i];
                    if (map.ContainsKey(c.name))
                        Debug.LogWarning($"Warning: camera at index {i} has the same name as a previous camera at index {map[c.name]}, this will cause capture files to be overwritten. Please specify a unique name for this camera.");
                    else
                        map.Add(c.name, i);
                    
                    if (_cameraSources[i].targetTexture == null)
                        cameraWithNoRtCount++;
                }
                
                if (cameraWithNoRtCount > 1)
                {
                    Debug.LogWarning("Target Texture is set to None for cameras other than the main camera.");
                }
            }
        }
    }
}
