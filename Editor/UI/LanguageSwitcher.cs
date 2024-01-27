using System;
using System.Globalization;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.ui
{
    /// <summary>
    /// VisualElement to display a language selector.
    /// </summary>
    public sealed class LanguageSwitcher : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<LanguageSwitcher, UxmlTraits>
        {
        }

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
        }

        public LanguageSwitcher()
        {
            // DropdownField is not supported in 2019...
            var imgui = new IMGUIContainer(() => { DrawImmediate(); });
            Add(imgui);
        }

        /// <summary>
        /// Draws a language selector using IMGUI.
        /// </summary>
        public static void DrawImmediate()
        {
            var curLang = LanguagePrefs.Language;

            var curIndex = LanguagePrefs.RegisteredLanguages.IndexOf(curLang);
            var DisplayNames = LanguagePrefs.RegisteredLanguages.Select(
                    lang =>
                    {
                        try
                        {
                            return CultureInfo.GetCultureInfo(lang).NativeName;
                        }
                        catch (Exception)
                        {
                            return lang;
                        }
                    })
                .ToArray();

            var newIndex = EditorGUILayout.Popup("Editor Language", curIndex, DisplayNames);

            if (newIndex != curIndex)
            {
                LanguagePrefs.Language = LanguagePrefs.RegisteredLanguages[newIndex];
            }
        }
    }
}