using System;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    static class UIElementHelpers
    {
        public static VisualElement LoadTemplate(string basePath, string uxmlFileName, string ussFileName = null, string uxmlSubDirectoryName = "uxml", string ussSubDirectoryName = "uss", string lightSkinUssSuffix = "light", string darkSkinUssSuffix = "dark")
            => LoadClonableTemplate(basePath, uxmlFileName, ussFileName, uxmlSubDirectoryName, ussSubDirectoryName, lightSkinUssSuffix, darkSkinUssSuffix).GetNewInstance();

        public static VisualElementTemplate LoadClonableTemplate(string basePath, string uxmlFileName, string ussFileName = null, string uxmlSubDirectoryName = "uxml", string ussSubDirectoryName = "uss", string lightSkinUssSuffix = "light", string darkSkinUssSuffix = "dark")
            => new VisualElementTemplate(basePath, uxmlFileName, ussFileName, uxmlSubDirectoryName, ussSubDirectoryName, lightSkinUssSuffix, darkSkinUssSuffix);

        public static void Show(this VisualElement v) => ToggleVisibility(v, true);
        public static void Hide(this VisualElement v) => ToggleVisibility(v, false);

        public static void ToggleVisibility(this VisualElement v, bool isVisible) => v.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

        public struct VisualElementTemplate
        {
            readonly VisualTreeAsset m_Template;
            readonly StyleSheet m_BaseStyle;
            readonly StyleSheet m_SkinStyle;

            public VisualElementTemplate(string basePath, string uxmlFileName, string ussFileName, string uxmlSubDirectoryName, string ussSubDirectoryName, string lightSkinUssSuffix, string darkSkinUssSuffix)
            {
                if (string.IsNullOrEmpty(basePath))
                    throw new ArgumentNullException(basePath);
                if (string.IsNullOrEmpty(uxmlFileName))
                    throw new ArgumentNullException(uxmlFileName);
                if (string.IsNullOrEmpty(lightSkinUssSuffix))
                    throw new ArgumentNullException(lightSkinUssSuffix);
                if (string.IsNullOrEmpty(darkSkinUssSuffix))
                    throw new ArgumentNullException(darkSkinUssSuffix);

                if (Path.HasExtension(uxmlFileName) && Path.GetExtension(uxmlFileName) == "uxml")
                    uxmlFileName = Path.GetFileNameWithoutExtension(uxmlFileName);

                var templatePath = Path.Combine(basePath, uxmlSubDirectoryName, uxmlFileName + ".uxml");
                m_Template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
                if (m_Template == null)
                    throw new ArgumentException("No UXML template found at location " + templatePath);

                var ussName = ussFileName ?? uxmlFileName;
                m_BaseStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(basePath, ussSubDirectoryName, $"{ussName}.uss"));
                var skinStylePath = EditorGUIUtility.isProSkin ? Path.Combine(basePath, ussSubDirectoryName, $"{ussName}_{darkSkinUssSuffix}.uss") : Path.Combine(basePath, ussSubDirectoryName, $"{ussName}_{lightSkinUssSuffix}.uss");
                m_SkinStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(skinStylePath);
            }

            public VisualElement GetNewInstance()
            {
                var visualElement = new VisualElement();
                m_Template.CloneTree(visualElement);
                if (m_BaseStyle != null)
                    visualElement.styleSheets.Add(m_BaseStyle);
                if (m_SkinStyle != null)
                    visualElement.styleSheets.Add(m_SkinStyle);

                return visualElement;
            }
        }
    }
}
