using System;
using System.IO;
using System.Collections.Generic;

namespace Unity.AI.Simulation
{
    public static class ContentType
    {
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

        public static string ForPath(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            if (_contentTypeMapping.ContainsKey(ext))
                return _contentTypeMapping[ext];
            return kDefaultContentType;
        }
    }
}