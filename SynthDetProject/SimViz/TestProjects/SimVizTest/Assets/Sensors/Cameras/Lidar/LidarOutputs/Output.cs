using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras.LidarOutputs
{
	/// <summary>
	/// Base clase for lidar output
	/// </summary>
    [RequireComponent(typeof(Lidar))]
    public abstract class Output : MonoBehaviour
    {
        Lidar _linkedLidar;
        Lidar linkedLidar
        {
            get
            {
                if (_linkedLidar == null)
                {
                    _linkedLidar = GetComponent<Lidar>();
                }
                return _linkedLidar;
            }
        }

	    void OnEnable()
	    {
		    gettingDataFromGPU = false;
		    linkedLidar.RegisterNewOutput(this);
	    }
	    void OnDisable()
	    {
		    linkedLidar.UnregisterOutput(this);
	    }

	    void OnDestroy()
	    {
		    pointCloud?.Dispose();
		    pointCloud = null;
		    beamAngles?.Dispose();
		    beamAngles = null;
	    }

	    public abstract string computeShader { get; }
	    ComputeShader _exportShader = null;
	    ComputeShader exportShader
	    {
		    get
		    {
			    if (_exportShader == null)
			    {
				    _exportShader = Resources.Load<ComputeShader>(computeShader);
			    }
			    return _exportShader;
		    }
	    }
	    
	    /// <summary>
	    /// Point cloud as a ComputeBuffer
	    /// </summary>
	    public ComputeBuffer pointCloud { get; private set; }
	    bool gettingDataFromGPU = false;
	    /// <summary>
	    /// Event raised when a compute buffer containing the point cloud is created
	    /// It will containg a Vector4 with the position of the point and its intensity
	    /// </summary>
	    public UnityEventComputeBuffer onPointCloud;

	    /// <summary>
	    /// If true the data from the compute buffer will be read to a native array
	    /// </summary>
	    public bool generatePointCloudData = false;
	    /// <summary>
	    /// Event raised when the point could data is read
	    /// </summary>
	    public UnityEventVector4Array onPointCloudData;

	    ComputeBuffer beamAngles = null;
	    /// <summary>
	    /// Internal render cycle, do not call it directly
	    /// </summary>
	    public void Render()
	    {
		    if (exportShader != null && linkedLidar.pointsPerRotationPerBeam > 0)
		    {
			    var cloudDataLength = linkedLidar.pointsPerRotationPerBeam * linkedLidar.nbOfBeams;
			    if (pointCloud != null && pointCloud.count != cloudDataLength)
			    {
					pointCloud.Dispose();
					pointCloud = null;
			    }

			    if (pointCloud == null)
			    {
				    pointCloud  = new ComputeBuffer(cloudDataLength, Marshal.SizeOf(typeof(Vector4)));
				    onPointCloud?.Invoke(pointCloud);
			    }

			    if (beamAngles == null)
			    {
				    beamAngles = new ComputeBuffer(Lidar.arrayLength, Marshal.SizeOf(typeof(int)));
			    }
			    beamAngles.SetData(linkedLidar.beamAngles);
			    
			    exportShader.SetBuffer(0, "Result", pointCloud);

			    exportShader.SetBuffer(0, "_Beams", beamAngles);
			    exportShader.SetVector("_FOV", new Vector2(linkedLidar.horizontalFieldOfView, linkedLidar.verticalFieldOfView));
			    SetTexture(PanoramicCamera.TSubCamera.Front);
			    SetTexture(PanoramicCamera.TSubCamera.Left);
			    SetTexture(PanoramicCamera.TSubCamera.Right);
			    SetTexture(PanoramicCamera.TSubCamera.Back);
			    SetTexture(PanoramicCamera.TSubCamera.Top);
			    SetTexture(PanoramicCamera.TSubCamera.Bottom);
			    exportShader.SetInt("_NbOfBeams", linkedLidar.nbOfBeams);
			    float azOffset = Time.time * linkedLidar.rps;
			    azOffset %= 1;
			    exportShader.SetFloat("_AzOffset", azOffset);
			    exportShader.SetInt("_PointsPerBeam", linkedLidar.pointsPerRotationPerBeam);
			    exportShader.SetVector("_ClipPlanes", new Vector2(linkedLidar.nearClipPlane, linkedLidar.farClipPlane));

			    exportShader.Dispatch(0, linkedLidar.pointsPerRotationPerBeam, linkedLidar.nbOfBeams, 1);
			    if (generatePointCloudData)
			    {
				    if (!gettingDataFromGPU)
				    {
					    AsyncGPUReadback.Request(
						    pointCloud, (rArgs) =>
						    {
							    try
							    {
								    if (rArgs.done)
								    {
									    if (rArgs.hasError)
									    {
										    Debug.LogError("Error getting buffer from gpu", this);
									    }
									    else
									    {
										    var pointCloudData = rArgs.GetData<Vector4>();
										    onPointCloudData?.Invoke(pointCloudData);
									    }
								    }
							    }
							    finally
							    {
								    gettingDataFromGPU = false;
							    }
						    }
					    );
				    }
			    }
		    }
	    }

	    void SetTexture(PanoramicCamera.TSubCamera camera)
	    {
		    var renderTexture = linkedLidar.GetTexture(camera);
		    if (renderTexture == null)
		    {	// front is always active and compute shader will fail if any of them is null (even if not used)
			    renderTexture = linkedLidar.GetTexture(PanoramicCamera.TSubCamera.Front);
		    }
		    exportShader.SetTexture(0, "_" + camera, renderTexture);
	    }
    }
}