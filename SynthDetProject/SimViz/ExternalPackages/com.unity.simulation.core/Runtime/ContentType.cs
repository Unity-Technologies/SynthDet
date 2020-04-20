using System;
using System.IO;
using System.Collections.Generic;

namespace Unity.Simulation
{
    /// <summary>
    /// Utility class for converting a path to content-type.
    /// </summary>
    public static class ContentType
    {
        /// <summary>
        /// Default content-type for uploads.
        /// </summary>
        public const string kDefaultContentType = "application/octet-stream";

        static Dictionary<string, string> _contentTypeMapping = new Dictionary<string, string>()
        {
            {".jpg",  "image/jpeg"},
            {".jpeg", "image/jpeg"},
            {".log",  "text/plain"},
            {".txt",  "text/plain"},
            {".raw",  kDefaultContentType},
            {".tga",  kDefaultContentType},
        };

        /// <summary>
        /// Returns the content type mapping for the provided input file name.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <returns>string of content type. Defaults to application/octet-stream.</returns>
        public static string ForPath(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (_contentTypeMapping.ContainsKey(ext))
                return _contentTypeMapping[ext];
            return kDefaultContentType;
        }
    }
}