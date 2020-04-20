using System;
using System.Collections;
using System.Collections.Generic;
using Syncity.Cameras.LidarOutputs;
using Syncity.Sensors;
using UnityEngine;

namespace Syncity.Cameras
{
	/// <summary>
	/// Component to simulate a lidar device
	/// </summary>
    [DisallowMultipleComponent]
    public class Lidar : MonoBehaviour
    {
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

	    [SerializeField] float _nearClipPlane = 0.1f;
	    /// <summary>
	    /// The near clipping plane distance.
	    /// </summary>
        public float nearClipPlane
        {
            get { return _nearClipPlane; }
            set
            {
                if (_nearClipPlane != value)
                {
                    _nearClipPlane = value;
                    RefreshSubCameras();
                }
            }
        }
		[SerializeField] float _farClipPlane = 100;
	    /// <summary>
	    /// The far clipping plane distance.
	    /// </summary>
	    public float farClipPlane
        {
            get { return _farClipPlane; }
            set
            {
                if (_farClipPlane != value)
                {
                    _farClipPlane = value;
                    RefreshSubCameras();
                }
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
				    RefreshSubCameras();
			    }
		    }
	    }
	    [SerializeField]
	    Vector2 _subCameraResolution = new Vector2(4096, 4096);
	    /// <summary>
	    /// Resolution for each of the subcameras used to raytrace the lidar
	    /// </summary>
	    public Vector2 subCameraResolution
	    {
		    get { return _subCameraResolution; }
		    set
		    {
			    if (_subCameraResolution != value)
			    {
				    _subCameraResolution = value;
				    RefreshSubCameras();
			    }
		    }
	    }

	    [SerializeField]
	    ScriptableObject _noiseGenerator;
	    /// <summary>
	    /// Random noise to simulate measuring errors on the devices
	    /// </summary>
	    public INoiseGenerator<float> noiseGenerator
	    {
		    get { return _noiseGenerator as INoiseGenerator<float>; }
		    set
		    {
			    if (_noiseGenerator != (ScriptableObject) value)
			    {
				    _noiseGenerator = value as ScriptableObject; 
				    RefreshSubCameras();
			    }
		    }
	    }

	    /// <summary>
	    /// Type of distribution for the lidar angles
	    /// </summary>
	    public enum TBeamAnglesDistribution
	    {
		    /// <summary>
		    /// Angles are evenly separated on the vertical field of view
		    /// </summary>
		    Uniform,
		    /// <summary>
		    /// Define each angle position
		    /// </summary>
		    Custom
	    }
	    /// <summary>
	    /// Angles distribution along the vertical field of view
	    /// </summary>
	    public TBeamAnglesDistribution beamAnglesDistribution = TBeamAnglesDistribution.Uniform;
	    /// <summary>
	    /// Max number of beams
	    /// </summary>
	    public const int arrayLength = 128; // if this number is changed, output shaders should be modified
	    /// <summary>
	    /// Array of beam angles, for internal usage, to modify the beam angles use SetAngle method
	    /// </summary>
	    public float[] beamAngles = new float[arrayLength];
	    /// <summary>
	    /// Get the angle in degrees for a beam
	    /// </summary>
	    /// <param name="index">Beam's index</param>
	    /// <returns>Beam angle in degrees</returns>
	    public float GetAngle(int index)
	    {
		    if (index >= nbOfBeams)
		    {
			    throw new InvalidOperationException($"Number of angles is {nbOfBeams}");
		    }

		    return beamAngles[index];
	    }
	    /// <summary>
	    /// Sets the angle for a beam
	    /// </summary>
	    /// <param name="index">Beam indes</param>
	    /// <param name="value">Beam angle value</param>
	    public void SetAngle(int index, float value)
	    {
		    if (index >= nbOfBeams)
		    {
			    throw new InvalidOperationException($"Number of angles is {nbOfBeams}");
		    }

		    if (beamAngles == null || beamAngles.Length != arrayLength)
		    {
			    beamAngles = new float[arrayLength];
		    }

		    var newNbOfBeams = Math.Min(arrayLength, index);
		    if (newNbOfBeams > nbOfBeams)
		    {
			    nbOfBeams = newNbOfBeams;
		    }

		    if (beamAngles[index] != value)
		    {
			    beamAngles[index] = value;
		    }
	    }
	    
	    [SerializeField]
	    int _nbOfBeams = 16;
	    /// <summary>
	    /// Number of beams
	    /// </summary>
	    public int nbOfBeams
	    {
		    get { return _nbOfBeams; }
		    set
		    {
			    if (_nbOfBeams != value)
			    {
				    _nbOfBeams = Mathf.Max(0, Mathf.Min(arrayLength, value));
			    }
		    }
	    }
	    
	    [SerializeField]
	    float _rpm = 600f;
	    /// <summary>
	    /// Lidar's number of rotations per second
	    /// </summary>
	    public float rps => rpm / 60f;
	    /// <summary>
	    /// Lidar's number of rotations per minute
	    /// </summary>
	    public float rpm
	    {
		    get { return _rpm; }
		    set
		    {
			    if (_rpm != value)
			    {
				    _rpm = Mathf.Max(1, value);
			    }
		    }
	    }
	    [SerializeField]
	    int _pointsPerSecond = 1200000;
	    int pointsPerSecondPerBeam => Mathf.FloorToInt(pointsPerSecond / (float)nbOfBeams);
	    int pointsPerRotation
	    {
		    get { return Mathf.FloorToInt(pointsPerSecond / rps); }
		    set { pointsPerSecond = Mathf.RoundToInt(value * rps); }
	    }
	    /// <summary>
	    /// Points per rotation of a single beam
	    /// </summary>
	    public int pointsPerRotationPerBeam
	    {
		    get { return pointsPerRotation / nbOfBeams; }
		    set { pointsPerRotation = value * nbOfBeams; }
	    }
	    /// <summary>
	    /// Number of total points per second
	    /// </summary>
	    public int pointsPerSecond
	    {
		    get { return _pointsPerSecond; }
		    set
		    {
			    if (_pointsPerSecond != value)
			    {
				    _pointsPerSecond = Mathf.Max(1, value);
			    }
		    }
	    }
	    
	    const string subCameraPrefix = "SubCamera ";
	    SingleTrueDepthCamera[] _subCameras = null;
	    /// <summary>
	    /// Lidar's subcameras, for internal usage
	    /// </summary>
	    public SingleTrueDepthCamera[] subCameras
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

				    _subCameras = new SingleTrueDepthCamera[rots.Length];
				    for (var i = 0; i < _subCameras.Length; i++)
				    {
					    var go = new GameObject(subCameraPrefix + ((PanoramicCamera.TSubCamera)i).ToString());
					    go.transform.SetParent(this.transform, false);
					    go.transform.localPosition = Vector3.zero;
					    go.transform.localScale = Vector3.one;
					    go.transform.localEulerAngles = rots[i];
					    go.hideFlags |= HideFlags.DontSave;

					    var depthCamera = go.AddComponent<SingleTrueDepthCamera>();                        
					    _subCameras[i] = depthCamera;
					    _subCameras[i].linkedCamera.enabled = false;
				    }
				    RefreshSubCameras();
			    }
			    return _subCameras;
		    }
	    }

	    void RefreshSubCameras()
	    {
		    if (!Application.isPlaying) return;
		    
		    for (int i = 0; i < subCameras.Length; i++)
		    {
			    RefreshSubCamera((PanoramicCamera.TSubCamera)i);                    
		    }
	    }
	    
	    void RefreshSubCamera(PanoramicCamera.TSubCamera subCamera)
        {
	        SingleTrueDepthCamera depthCamera = subCameras[(int) subCamera];
	        Camera camera = depthCamera.linkedCamera;
	        GameObject cameraGameObject = camera.gameObject;

#if DEBUG_LIDAR
            camera.gameObject.hideFlags &= ~HideFlags.HideInHierarchy;
#else
	        camera.gameObject.hideFlags |= HideFlags.HideInHierarchy;
#endif
	        
	        if (!enabled)
	        {
		        cameraGameObject.SetActive(false);
		        return;
	        }

	        switch (subCamera)
            {
                case PanoramicCamera.TSubCamera.Left:
	                cameraGameObject.SetActive(_horizontalFieldOfView >= 90f);
                    break;
                case PanoramicCamera.TSubCamera.Right:
	                cameraGameObject.SetActive(_horizontalFieldOfView >= 90f);
                    break;
                case PanoramicCamera.TSubCamera.Back:
	                cameraGameObject.SetActive(_horizontalFieldOfView >= 270f);
                    break;
                case PanoramicCamera.TSubCamera.Top:
	                cameraGameObject.SetActive(_verticalFieldOfView >= 68f);
                    break;
                case PanoramicCamera.TSubCamera.Bottom:
	                cameraGameObject.SetActive(_verticalFieldOfView >= 68f);
                    break;
                case PanoramicCamera.TSubCamera.Front:
                default:
	                cameraGameObject.SetActive(true);
                    break;
            }

            if (camera.gameObject.activeInHierarchy)
            {
				var targetDimensions = new Vector2Int(
					Mathf.CeilToInt(subCameraResolution.x),
					Mathf.CeilToInt(subCameraResolution.y));

                if (camera.targetTexture)
                {
                    if (camera.targetTexture.width != targetDimensions.x ||
                        camera.targetTexture.height != targetDimensions.y)
                    {
                        var rt = camera.targetTexture;
                        camera.targetTexture = null;
                        rt.Release();
                        DestroyImmediate(rt);
                    }
                }

                if (camera.targetTexture == null)
                {
                    RenderTexture texture = new RenderTexture(
                        targetDimensions.x,
                        targetDimensions.y,
                        0, RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Linear) 
                    {
                        filterMode = FilterMode.Point,
	                    wrapMode = TextureWrapMode.Clamp,
	                    anisoLevel = 0
                    };
                    texture.Create();
                    camera.targetTexture = texture;
                }
            }
            else
            {
                if (camera.targetTexture != null)
                {
                    var rt = camera.targetTexture;
                    camera.targetTexture = null;
                    rt.Release();
                    DestroyImmediate(rt);
                }
            }

            camera.cullingMask = cullingMask;

            camera.nearClipPlane = nearClipPlane;
            camera.farClipPlane = farClipPlane;
                
            camera.depth = 0;
            camera.renderingPath = RenderingPath.Forward;
                
            camera.useOcclusionCulling = false;
            camera.allowHDR = true;
            camera.allowMSAA = false;

            camera.fieldOfView = 90f;
            camera.aspect = 1;
            camera.layerCullSpherical = true;

	        depthCamera.noiseGenerator = noiseGenerator;
        }

        void OnDestroy()
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
		        DestroyImmediate(camera.gameObject);
	        }
	        _subCameras = null;
        }

	    void OnEnable()
	    {
		    renderCoroutine = StartCoroutine(Render());
	    }

	    void OnDisable()
	    {
		    if (renderCoroutine != null)
		    {
			    StopCoroutine(renderCoroutine);
		    }
	    }

	    Coroutine renderCoroutine = null;
	    IEnumerator Render()
	    {
		    while (this.enabled)
		    {
			    if (this.enabled)
			    {
				    for (int i = 0; i < subCameras.Length; i++)
				    {
					    if (_subCameras[i].linkedCamera.gameObject.activeInHierarchy)
					    {
						    _subCameras[i].linkedCamera.Render();
					    }
				    }

				    for (int i = 0; i < outputs.Count; i++)
				    {
					    var output = outputs[i];
					    output.Render();
				    }

				    yield return new WaitForEndOfFrame();
			    }
		    }
	    }

	    readonly List<LidarOutputs.Output> outputs = new List<Output>();
	    /// <summary>
	    /// Register a new lidar output component
	    /// </summary>
	    /// <param name="output"></param>
	    public void RegisterNewOutput(LidarOutputs.Output output)
	    {
		    outputs.Add(output);
	    }
	    /// <summary>
	    /// Unregister a registered lidar ouput
	    /// </summary>
	    /// <param name="output"></param>
	    public void UnregisterOutput(LidarOutputs.Output output)
	    {
		    outputs.Remove(output);
	    }

	    /// <summary>
	    /// Gets a subcamera texture
	    /// </summary>
	    /// <param name="camera">Subcamera direction</param>
	    /// <returns>Subcamera's render texture</returns>
	    public RenderTexture GetTexture(PanoramicCamera.TSubCamera camera)
	    {
		    return subCameras[(int) camera].linkedCamera.targetTexture;
	    }
	    
	    /// <summary>
	    /// Helper function to get the vertical angle in degrees from a [0, 1] value
	    /// </summary>
	    /// <param name="v">Normalized value</param>
	    /// <returns>Vertical angle in degrees</returns>
	    public float GetVerticlAngleInDegreesForNormalizadValue(float v)
	    {
		    return (v * verticalFieldOfView) - verticalFieldOfView / 2f;
	    }
	    /// <summary>
	    /// Helper function to get the [0, 1] value from the vertical angle in degrees 
	    /// </summary>
	    /// <param name="v">Vertical angle in degrees</param>
	    /// <returns>Normalized value</returns>
	    public float GetVerticalNormalizedValueForAngleInDegrees(float v)
	    {
		    return (v + verticalFieldOfView / 2f) / verticalFieldOfView;
	    }


    }
}