using UnityEngine;

namespace Syncity.Cameras.LidarOutputs
{
	/// <summary>
	/// Cartesian output of the point cloud as (x, y, z, [0..1] laserid)
	/// </summary>
    [DisallowMultipleComponent]
    public class CartesianOutput : Output
    {
	    public override string computeShader => "CartesianOutput";
	}
}