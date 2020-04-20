using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.AI.Simulation
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
            _baseDirectory = DXManager.Instance.GetDirectoryFor(DataCapturePaths.ScreenCapture, _customFilePath);
        }

        void Update()
        {   
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime > _screenCaptureInterval)
            {
                _elapsedTime -= _screenCaptureInterval;

                for (var i = 0; i < _cameraSources.Length; i++)
                {
                    var camera = _cameraSources[i];
                    
                    if (Application.isBatchMode && camera.targetTexture == null)
                    {
                        camera.targetTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight,0, _format);
                    }

                    CaptureCamera.CaptureColorToFile(camera, _format, Path.Combine(_baseDirectory, _cameraSources[i].name+ "_" + _sequence + "." + _imageFormat.ToString().ToLower()), _imageFormat);
                    if (!camera.enabled)
                        camera.Render();
                }
                
                ++_sequence;
            }
        }

        void OnValidate()
        {
            var map = new Dictionary<string, int>();
            if (_cameraSources != null)
            {
                for (var i = 0; i < _cameraSources.Length; ++i)
                {
                    var c = _cameraSources[i];
                    if (map.ContainsKey(c.name))
                        Debug.LogWarning(string.Format("Warning: camera at index {0} has the same name as a previous camera at index {1}, this will cause capture files to be overwritten. Please specify a unique name for this camera.", i, map[c.name]));
                    else
                        map.Add(c.name, i);
                }
            }
        }
    }
}