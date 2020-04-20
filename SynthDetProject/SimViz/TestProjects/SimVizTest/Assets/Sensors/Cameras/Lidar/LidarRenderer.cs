using Syncity.Cameras.LidarOutputs;
using UnityEngine;

namespace Syncity.Cameras
{
	/// <summary>
	/// Renders the cartisian ouput of a lidar on the screen as a point cloud
	/// </summary>
	[RequireComponent(typeof(CartesianOutput))]
	[DisallowMultipleComponent]
	public sealed class LidarRenderer : MonoBehaviour
	{
		CartesianOutput _linkedCartesianOutput;
		CartesianOutput linkedCartesianOutput
		{
		    get
		    {
		        if (_linkedCartesianOutput == null)
		        {
		            _linkedCartesianOutput = GetComponent<CartesianOutput>();
		        }
		        return _linkedCartesianOutput;
		    }
		}
		[SerializeField]
		float _pointSize = .1f;
		/// <summary>
		/// Size for the rendered point
		/// </summary>
		public float pointSize
		{
			get { return _pointSize; }
			set
			{
				if (value != _pointSize)
				{
					_pointSize = Mathf.Max(0, value);
				}
			}
		}
		/// <summary>
		/// Start color for the laser id gradient
		/// </summary>
		public Color startColor = Color.red;
		/// <summary>
		/// End color for the laser id gradient
		/// </summary>
		public Color endColor = Color.blue;

		void OnDestroy()
		{
			if (material != null)
			{
				DestroyImmediate(material);
			}
		}

		void FixedUpdate()
		{
			if (linkedCartesianOutput == null || !linkedCartesianOutput.enabled) return;
			if (linkedCartesianOutput.pointCloud == null) return;

			if (material == null)
			{
				material = new Material(Shader.Find("Hidden/Syncity/Cameras/LidarRenderer"));
				material.hideFlags = HideFlags.DontSave;
			}

			material.SetMatrix("_Transform", linkedCartesianOutput.transform.localToWorldMatrix);
			material.SetBuffer("_PointBuffer", linkedCartesianOutput.pointCloud);
			material.SetFloat("_PointSize", pointSize / 2f);

			material.SetColor("_StartColor", startColor);
			material.SetColor("_EndColor", endColor);
		}

		Material material;
		/// <summary>
		/// Camera layer the lidar will be rendered to
		/// </summary>
		public SingleUnityLayer layer;
		private void OnRenderObject()
		{
			if (linkedCartesianOutput == null || !linkedCartesianOutput.enabled) return;
			if (linkedCartesianOutput.pointCloud == null) return;
			if (material == null) return;
			
			//Check the camera condition.
			var camera = Camera.current;
			if ((camera.cullingMask & layer.mask) == 0) return;

			material.SetPass(0);
#if UNITY_2019_1_OR_NEWER
			Graphics.DrawProceduralNow(MeshTopology.Points, linkedCartesianOutput.pointCloud.count, 1);
#else
			Graphics.DrawProcedural(MeshTopology.Points, linkedCartesianOutput.pointCloud.count, 1);
#endif
		}
	}
}