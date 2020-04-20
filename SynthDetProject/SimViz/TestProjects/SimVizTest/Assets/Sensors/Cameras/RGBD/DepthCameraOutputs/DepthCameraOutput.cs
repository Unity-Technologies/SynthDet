using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras.DepthCameraOutputs
{
	/// <summary>
	/// Base class for eye depth camera outputs
	/// </summary>
	[RequireComponent(typeof(DepthCamera))]
	[ExecuteInEditMode, DisallowMultipleComponent]
	public abstract class DepthCameraOutput : MonoBehaviour 
	{
		DepthCamera _linkedDepthCamera;
		DepthCamera linkedDepthCamera
		{
		    get
		    {
		        if (_linkedDepthCamera == null)
		        {
		            _linkedDepthCamera = GetComponent<DepthCamera>();
		        }
		        return _linkedDepthCamera;
		    }
		}
		
		protected void OnDestroy()
		{
			if (internalRenderTexture != null)
			{
				internalRenderTexture.Release();
				DestroyImmediate(internalRenderTexture);
				internalRenderTexture = null;
			}
			if (material != null)
			{
				DestroyImmediate(material);
				material = null;
			}
		}

		RenderTexture internalRenderTexture = null;
		protected Material material = null;
		protected abstract string shaderName { get; }
		CommandBuffer commandBuffer = null;
		CameraEvent cameraEvent => CameraEvent.AfterImageEffects;
		public void RegisterCommandBuffersMainCamera(Camera c)
		{
			var targetDimensions = new Vector2Int(c.pixelWidth, c.pixelHeight);
			var targetFormat = RenderTextureFormat.ARGB32;

			if (linkedDepthCamera.linkedPanoramicCamera.targetTexture != null)
			{
				targetFormat = linkedDepthCamera.linkedPanoramicCamera.targetTexture.format;
			}

			if (internalRenderTexture != null && 
			    (internalRenderTexture.width != targetDimensions.x ||
			     internalRenderTexture.height != targetDimensions.y ||
			     internalRenderTexture.format != targetFormat))
			{
				internalRenderTexture.Release();
				DestroyImmediate(internalRenderTexture);
				internalRenderTexture = null;
			}
			
			if(internalRenderTexture == null)
			{
				internalRenderTexture = new RenderTexture(
					targetDimensions.x, 
					targetDimensions.y,
					24, 
					targetFormat, 
					RenderTextureReadWrite.Linear);
				commandBuffer?.Dispose();
				commandBuffer = null;
			}

			if (material == null)
			{
				material = new Material(Shader.Find(shaderName));
			}
			UpdateMaterial();
			if (commandBuffer == null)
			{
				commandBuffer = new CommandBuffer();
				commandBuffer.name = this.GetType().Name;                    
				commandBuffer.Blit(BuiltinRenderTextureType.CameraTarget, internalRenderTexture);
				commandBuffer.Blit(internalRenderTexture, BuiltinRenderTextureType.CameraTarget, material);
			}

			c.AddCommandBuffer(cameraEvent, commandBuffer);				
		}
		protected virtual void UpdateMaterial()
		{
			if (material != null)
			{
				material.SetInt("_InvertY", linkedDepthCamera.linkedPanoramicCamera.targetTexture != null ? 0 : 1);
			}
		}
		public virtual void UnregisterCommandBuffersMainCamera(Camera c)
		{
			if (commandBuffer != null)
			{
				c.RemoveCommandBuffer(cameraEvent, commandBuffer);				
			}
		}
	}
}
