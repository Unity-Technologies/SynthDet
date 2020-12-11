using System;
using System.IO;

namespace Unity.Entities.Hybrid
{
    /// <summary>
    /// Information for resources to be loaded at runtime.
    /// </summary>
    public struct ResourceMetaData
    {
        /// <summary>
        /// For scenes, if AutoLoad is true, the scene will be loaded when the player starts
        /// </summary>
        [Flags]
        public enum Flags
        {
            None = 0,
            AutoLoad = 1
        }

        /// <summary>
        /// Currently Scene types are supported, assetbundles will need to be supported when dependencies are implemented
        /// </summary>
        public enum Type
        {
            Unknown,
            Scene,
        }

        /// <summary>
        /// The guid of the asset
        /// </summary>
        public Hash128 ResourceId; 

        /// <summary>
        /// Flags to control the behavior of the asset
        /// </summary>
        public Flags ResourceFlags;         

        /// <summary>
        /// The type of resource.
        /// </summary>
        public Type ResourceType;
    }

    /// <summary>
    /// Container for resource data.
    /// </summary>
    public struct ResourceCatalogData
    {
        /// <summary>
        /// File format needs to change anytime the data layout for this class changes.
        /// </summary>
        public static readonly int CurrentFileFormatVersion = 1;
        /// <summary>
        /// The resource data.
        /// </summary>
        public BlobArray<ResourceMetaData> resources;

        /// <summary>
        /// Path information for resources.  This is separate to keep the resources data streamlined as using paths is slow.
        /// </summary>
        public BlobArray<BlobString> paths;
         
        /// <summary>
        /// Slow path to lookup guid from a path.  This first checks the passed in path then just the filename, then the lowercase version of the filename.
        /// </summary>
        /// <param name="path">The resource path.</param>
        /// <returns>The guid for the resource.</returns>
        public Hash128 GetGUIDFromPath(string path)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var origPath = paths[i].ToString();
                if (path == origPath)
                    return resources[i].ResourceId;
                var fpath = Path.GetFileNameWithoutExtension(origPath);
                if (path == fpath)
                    return resources[i].ResourceId;
                var lpath = fpath.ToLower();
                if (path == lpath)
                    return resources[i].ResourceId;
            }
            return default;
        }
    }
}