using UnityEngine;

namespace Syncity.Cameras.DepthCameraOutputs
{
	/// <summary>
	/// Eye depth camera output as a gradient between two colors
	/// </summary>
	public class ColorCodedOutput : DepthCameraOutput 
	{
		protected override string shaderName => "Hidden/Syncity/Cameras/ColorCoded";

		[SerializeField]
		Color _startColor = Color.red;
		/// <summary>
		/// Color for minimum distance (near clip plane)
		/// </summary>
		public Color startColor
		{
			get
			{
				return _startColor;
			}
			set
			{
				if (_startColor != value)
				{
					_startColor = value;
					UpdateMaterial();
				}
			}
		}
		[SerializeField]
		Color _endColor = Color.yellow;
		/// <summary>
		/// Color for maximum distance (far clip plane)
		/// </summary>
		public Color endColor
		{
			get
			{
				return _endColor;
			}
			set
			{
				if (_endColor != value)
				{
					_endColor = value;
					UpdateMaterial();
				}
			}
		}
		
		
		protected override void UpdateMaterial()
		{
			if (material != null)
			{
				material.SetColor("_StartColor", startColor);
				material.SetColor("_EndColor", endColor);
				base.UpdateMaterial();
			}
		}
	}
}
