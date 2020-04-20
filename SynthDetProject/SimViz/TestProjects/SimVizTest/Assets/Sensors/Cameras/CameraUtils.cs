using UnityEngine;
using UnityEngine.Rendering;

namespace Syncity.Cameras
{
    public static class CameraUtils
    {
        static CameraUtils()
        {
            blitMaterial = new Material(Shader.Find("Hidden/SynCity/Blit"));
        }
        
        public static Material blitMaterial { get; private set; }

        public static void Blit(CommandBuffer commandBuffer, RenderTargetIdentifier src, RenderTargetIdentifier dest)
        {
            commandBuffer.SetGlobalTexture("_MainTex", src);
            CoreUtils.DrawFullScreen(commandBuffer, CameraUtils.blitMaterial, dest);
        }
    }
}