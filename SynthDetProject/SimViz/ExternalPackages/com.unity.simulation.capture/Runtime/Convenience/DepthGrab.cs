using System.IO;

using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Simulation
{   
    public class DepthGrab : MonoBehaviour
    {
        public CaptureImageEncoder.ImageFormat _imageFormat = CaptureImageEncoder.ImageFormat.Raw;
        public float               _screenCaptureInterval = 1.0f;
        public GraphicsFormat      _format = GraphicsFormat.R16_UNorm;

        float              _elapsedTime;
        string             _baseDirectory;
        int                _sequence = 0;
        Camera             _camera;

        void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                enabled = false;
            _baseDirectory = Manager.Instance.GetDirectoryFor(DataCapturePaths.ScreenCapture);
        }

        void Update()
        {   
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime > _screenCaptureInterval)
            {
                _elapsedTime -= _screenCaptureInterval;

                if (Application.isBatchMode && _camera.targetTexture == null)
                {
                    _camera.targetTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight,0, _format);
                }

                CaptureCamera.CaptureDepthToFile
                (
                    _camera, 
                    _format, 
                    Path.Combine(_baseDirectory, _camera.name + "_depth_" + _sequence + "." + _imageFormat.ToString().ToLower()),
                    _imageFormat
                );

                if (!_camera.enabled)
                    _camera.Render();

                ++_sequence;
            }
        }
    }
}