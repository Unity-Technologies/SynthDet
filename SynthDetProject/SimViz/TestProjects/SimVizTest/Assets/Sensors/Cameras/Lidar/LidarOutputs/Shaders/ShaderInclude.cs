using System.Linq;
using UnityEngine;
using System.IO;

namespace Syncity.Cameras.LidarOutputs
{
    static class ShaderInclude
    {
//#if UNITY_2018_1_OR_NEWER// && !UNITY_2018_4_OR_NEWER
//        [ShaderIncludePath]
//#endif
        public static string[] GetPaths()
        {
            return new[] { Path.GetFullPath("Packages/syncity/Cameras/Lidar/LidarOutputs/Shaders/Resources/Output.cginc") };
        }
    }
}
