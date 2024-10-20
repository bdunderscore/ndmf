using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.ui
{
    internal class GroupHeader : VisualElement
    {
        public GroupHeader(PluginBase plugin)
        {
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/GroupHeader.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            Add(labelFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/GroupHeader.uss");
            styleSheets.Add(styleSheet);

            var titleElem = this.Q<Label>("group-title");
            var imageElem = this.Q<Image>("group-logo");

            if (plugin?.LogoTexture != null)
            {
                imageElem.image = plugin.LogoTexture;
                titleElem.style.display = DisplayStyle.None;

#if !UNITY_2020_1_OR_NEWER
                // Older versions of unity don't scale the image the way we want. Manually compute the size...
                // Note that 2020.1 is a bit of a guess - I've only tried 2019.4.31f1 and 2022.3.6f1.
                var aspectRatio = plugin.LogoTexture.width / (float)plugin.LogoTexture.height;
                var height = 64 - 12;
                var width = height * aspectRatio;
                imageElem.style.width = width;
                imageElem.style.height = height;
#endif
            }
            else if (plugin != null)
            {
                imageElem.style.display = DisplayStyle.None;
                titleElem.text = plugin.DisplayName;
            } else {
                imageElem.style.display = DisplayStyle.None;
                titleElem.text = "???";
            }
        }
    }
}