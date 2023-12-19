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

            if (plugin.LogoTexture != null)
            {
                imageElem.image = plugin.LogoTexture;
                titleElem.style.display = DisplayStyle.None;
            }
            else
            {
                imageElem.style.display = DisplayStyle.None;
                titleElem.text = plugin.DisplayName;
            }
        }
    }
}