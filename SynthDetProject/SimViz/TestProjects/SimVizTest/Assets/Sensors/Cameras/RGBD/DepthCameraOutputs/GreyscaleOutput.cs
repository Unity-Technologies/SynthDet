namespace Syncity.Cameras.DepthCameraOutputs
{
	/// <summary>
	/// Eye depth camera output as a greyscale gradient
	/// </summary>
	public class GreyscaleOutput : DepthCameraOutput
	{
		protected override string shaderName => "Hidden/Syncity/Cameras/Greyscale";
	}
}
