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

            var FilteredLanguages = LanguagePrefs.RegisteredLanguages
                .Where(lang => lang.Contains("-") ||
                               LanguagePrefs.RegisteredLanguages.All(l2 => !l2.StartsWith(lang + "-")))
                .ToArray();
            var curIndex = FilteredLanguages.ToList().IndexOf(curLang);
            
            var DisplayNames = FilteredLanguages.Select(LanguagePrefs.GetLocaleNativeName).ToArray();

            var newIndex = EditorGUILayout.Popup("Editor Language", curIndex, DisplayNames);

            if (newIndex != curIndex)
            {
                LanguagePrefs.Language = FilteredLanguages[newIndex];
            }
        }
    }
}