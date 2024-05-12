#region

using System.Collections.Generic;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal class SimpleErrorUI : VisualElement
    {
        private readonly ErrorReport _report;
        private readonly SimpleError _error;

        public SimpleErrorUI(ErrorReport report, SimpleError error)
        {
            this._report = report;
            this._error = error;

            LanguagePrefs.RegisterLanguageChangeCallback(this, ui => ui.RenderContent());

            RenderContent();
        }

        internal void AddStackTrace(string trace)
        {
            var traceFoldout = this.Q<Foldout>("stack-trace-foldout");
            var traceElem = this.Q<TextField>("stack-trace");
            traceFoldout.style.display = DisplayStyle.Flex;
            traceElem.value = trace;
        }

        private void RenderContent()
        {
            Clear();

            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/SimpleErrorUI.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            Add(labelFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/SimpleErrorUI.uss");
            styleSheets.Add(styleSheet);
            
            NDMFLocales.L.LocalizeUIElements(this);

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
            icon.Severity = _error.Severity;
            
            var objRefs = this.Q<VisualElement>("object-references");
            if (_error.References.Length == 0)
            {
                objRefs.style.display = DisplayStyle.None;
            }
            else
            {
                HashSet<ObjectReference> _refs = new HashSet<ObjectReference>();
                foreach (var objRef in _error.References)
                {
                    // dedup refs
                    if (!_refs.Add(objRef)) continue;
                    
                    if (ObjectSelector.TryCreate(_report, objRef, out var selector))
                    {
                        objRefs.Add(selector);
                    }
                    else
                    {
                        objRefs.Add(new Label(objRef.ToString()));
                    }
                }
            }
        }
    }
}