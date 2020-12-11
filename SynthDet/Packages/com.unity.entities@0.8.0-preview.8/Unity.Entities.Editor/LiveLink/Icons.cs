using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    static class Icons
    {
        const string k_IconsDirectory = "Packages/com.unity.entities/Editor/LiveLink/Icons";

        public static GUIContent LiveLink { get; } = new GUIContent(LoadIcon("LiveLink"));
        public static GUIContent LiveLinkOn { get; } = new GUIContent(LoadIcon("LiveLinkActive"));

        /// <summary>
        /// Workaround for `EditorGUIUtility.LoadIcon` not working with packages. This can be removed once it does
        /// </summary>
        /// <param name="relativePathInIconDirectory">Relative path the icon in the default icons directory</param>
        /// <returns>The loaded icon as a <see cref="Texture2D"/></returns>
        static Texture2D LoadIcon(string relativePathInIconDirectory)
        {
            if (string.IsNullOrEmpty(relativePathInIconDirectory))
                return null;

            if (EditorGUIUtility.isProSkin)
            {
                var idx = relativePathInIconDirectory.LastIndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                if (idx != -1)
                    relativePathInIconDirectory = relativePathInIconDirectory.Insert(idx + 1, "d_");
                else
                    relativePathInIconDirectory = "d_" + relativePathInIconDirectory;
            }

            // Try to use high DPI if possible
            if (EditorGUIUtility.pixelsPerPoint > 1.0)
            {
                var texture = LoadIconTexture($"{k_IconsDirectory}/{relativePathInIconDirectory}@2x.png");

                if (null != texture)
                    return texture;
            }

            // Fallback to low DPI if we couldn't find the high res or we are on a low res screen
            return LoadIconTexture($"{k_IconsDirectory}/{relativePathInIconDirectory}.png");
        }

        static Texture2D LoadIconTexture(string path)
        {
            var texture = (Texture2D) AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));

            if (texture != null
                && !Mathf.Approximately(texture.GetPixelsPerPoint(), EditorGUIUtility.pixelsPerPoint)
                && !Mathf.Approximately(EditorGUIUtility.pixelsPerPoint % 1f, 0.0f))
                texture.filterMode = FilterMode.Bilinear;

            return texture;
        }
    }

    // This helper is here to encapsulate the access to `pixelsPerPoint` private property on Texture2D.
    // We need to access this property to enable bilinear filter mode on the texture when its pixel
    // per point is different from the editor ppp.
    // TODO: @antoineb remove this when ppp is public or thanks to a cleaner solution
    static class InternalsHelpers
    {
        static PropertyInfo s_TexturePixelsPerPoint;

        static InternalsHelpers()
        {
            s_TexturePixelsPerPoint = typeof(Texture2D).GetProperty("pixelsPerPoint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static float GetPixelsPerPoint(this Texture2D @this)
        {
            var v = s_TexturePixelsPerPoint?.GetValue(@this);
            if (v == null)
                return 1.0f;

            return (float)v;
        }
    }
}
