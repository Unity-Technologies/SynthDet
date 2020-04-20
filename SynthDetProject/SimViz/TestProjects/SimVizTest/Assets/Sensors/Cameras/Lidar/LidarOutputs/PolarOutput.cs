using UnityEngine;

namespace Syncity.Cameras.LidarOutputs
{
	/// <summary>
	/// Polar output of the point cloud as (azimuth, elevation, depth, [0..1] laserid)
	/// </summary>
    [DisallowMultipleComponent]
    public class PolarOutput : Output
    {
	    public override string computeShader => "PolarOutput";
	}
}