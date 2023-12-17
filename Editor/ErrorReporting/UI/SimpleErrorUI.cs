#region

using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal class SimpleErrorUI : VisualElement
    {
        private readonly SimpleError _error;

        public SimpleErrorUI(SimpleError error)
        {
            this._error = error;

            LanguagePrefs.RegisterLanguageChangeCallback(this, ui => ui.RenderContent());

            RenderContent();
        }

        private void RenderContent()
        {
            Clear();

            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/SimpleErrorUI.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            Add(labelFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/SimpleErrorUI.uss");
            styleSheets.Add(styleSheet);

            var titleElem = this.Q<Label>("title");
            titleElem.text = _error.FormatTitle();

            var descText = _error.FormatDetails();
            var descElem = this.Q<Label>("description");

            if (descText != null)
            {
                descElem.text = descText;
            }
            else
            {
                descElem.style.display = DisplayStyle.None;
            }

            var hintFoldout = this.Q<Foldout>("hint-foldout");
            hintFoldout.text = NDMFLocales.L.GetLocalizedString("ErrorReport:HintFoldout");

            var hintText = _error.FormatHint();
            var hintElem = this.Q<Label>("hint");

            if (hintText != null)
            {
                hintElem.text = hintText;
            }
            else
            {
                hintFoldout.style.display = DisplayStyle.None;
            }

            var icon = this.Q<ErrorIcon>("icon");
            icon.Category = _error.Category;
        }
    }
}