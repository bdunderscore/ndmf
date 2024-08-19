#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_2022_1_OR_NEWER
using UnityEngine.TextCore.Text;
#endif

#endregion

namespace nadena.dev.ndmf.localization
{
    /// <summary>
    /// Tracks the currently selected UI language
    /// </summary>
    public static class LanguagePrefs
    {
        private const string LocaleNameDatasetPath =
            "Packages/nadena.dev.ndmf/Editor/UI/Localization/language_names.txt";
        private const string EditorPrefKey = "nadena.dev.ndmf.language-selection";
        private static string _curLanguage = "en-us";

        private static ImmutableDictionary<String, String> LocaleNames;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Exiting playmode destroys the dynamic font assets we create, so we need to recreate and reapply them.
            Language = EditorPrefs.GetString(EditorPrefKey, GetSystemLanguage()).ToLowerInvariant();
            EditorApplication.playModeStateChanged += evt =>
            {
                foreach (var fontCallback in _fontUpdateCallbacks.Values)
                {
                    fontCallback();
                }
            };

            EditorApplication.update += MaybeRefreshActiveFont;
        }

        private static string GetSystemLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Japanese:
                    return "ja-jp";
                case SystemLanguage.ChineseSimplified:
                    return "zh-hans";
                case SystemLanguage.ChineseTraditional:
                    return "zh-hant";
                case SystemLanguage.Korean:
                    return "ko-kr";
                case SystemLanguage.Chinese:
                    // fall through - this case is ambiguous and culturally sensitive, so we'll treat this case as an
                    // unknown language.
                default:
                    return "en-us";
            }
        }

        /// <summary>
        /// The currently selected language ID, e.g. "en-us".
        /// </summary>
        public static string Language
        {
            get => _curLanguage;
            set
            {
                value = value.ToLowerInvariant();
                if (value == _curLanguage) return;
                _curLanguage = value;
                EditorPrefs.SetString(EditorPrefKey, value);
                TriggerLanguageChangeCallbacks();
            }
        }

        // TODO: Move to a single ConditionalWeakTable once we can use .NET 7 (which allows us to iterate this)
        private static HashSet<Action> _onLanguageChangeCallbacks = new HashSet<Action>();

        private static Dictionary<VisualElement, Action> _fontUpdateCallbacks = new Dictionary<VisualElement, Action>();

        private sealed class ElementFinalizer
        {
            internal readonly Action theAction;

            public ElementFinalizer(Action theAction)
            {
                this.theAction = theAction;
            }

            ~ElementFinalizer()
            {
                lock (_onLanguageChangeCallbacks)
                {
                    _onLanguageChangeCallbacks.Remove(theAction);
                }
            }
        }

        private static ConditionalWeakTable<object, ElementFinalizer> _targetRefs =
            new ConditionalWeakTable<object, ElementFinalizer>();

        /// <summary>
        /// Registers a callback to be invoked when the currently selected language changes.
        /// This callback will be retained as long as the `handle` object is not garbage collected.
        /// </summary>
        /// <param name="handle">An object which controls the lifetime of callback.</param>
        /// <param name="callback">A callback to be invoked, passing the value of handle</param>
        /// <typeparam name="T"></typeparam>
        public static void RegisterLanguageChangeCallback<T>(
            T handle,
            Action<T> callback
        ) where T : class
        {
            var weakRef = new WeakReference<T>(handle);
            Action op = () =>
            {
                if (weakRef.TryGetTarget(out var liveHandle))
                {
                    callback(liveHandle);
                }
            };
            var finalizer = new ElementFinalizer(op);
            lock (_onLanguageChangeCallbacks)
            {
                _onLanguageChangeCallbacks.Add(op);
                // _targetRefs.AddOrUpdate(handle, finalizer); - not supported on 2019
                if (_targetRefs.TryGetValue(handle, out var _))
                {
                    _targetRefs.Remove(handle);
                }
                _targetRefs.Add(handle, finalizer);
            }
        }

        private static void TriggerLanguageChangeCallbacks()
        {
            lock (_onLanguageChangeCallbacks)
            {
                foreach (Action op in new List<Action>(_onLanguageChangeCallbacks))
                {
                    try
                    {
                        op();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                
                foreach (Action op in _fontUpdateCallbacks.Values)
                {
                    try
                    {
                        op();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a list of all languages which have been registered at some point with the localization system.
        /// </summary>
        public static ImmutableSortedSet<string> RegisteredLanguages { get; private set; }

        internal static string GetLocaleNativeName(string locale)
        {
            if (LocaleNames.TryGetValue(locale.ToLowerInvariant().Replace("-", "_"), out var name))
            {
                return name;
            }
            else
            {
                try
                {
                    return CultureInfo.CreateSpecificCulture(locale).NativeName;
                }
                catch (Exception)
                {
                    return locale;
                }
            }
        } 
        
        static LanguagePrefs()
        {
            try
            {
                var localeNameJson = File.ReadAllText(LocaleNameDatasetPath, Encoding.UTF8);
                var lines = localeNameJson.Split('\n');

                var builder = ImmutableDictionary.CreateBuilder<string, string>();
                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;

                    builder.Add(parts[0].ToLowerInvariant(), parts[1]);
                }

                LocaleNames = builder.ToImmutableDictionary();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LocaleNames = ImmutableDictionary<string, string>.Empty;
            }

            RegisteredLanguages = ImmutableSortedSet<string>.Empty;
        }

        /// <summary>
        /// Registers an additional language code to display in the language selectors.
        /// </summary>
        /// <param name="languageCode"></param>
        public static void RegisterLanguage(string languageCode)
        {
            RegisteredLanguages = RegisteredLanguages.Add(languageCode.ToLowerInvariant());
        }

        private static IDictionary<string, StyleFontDefinition> FontCache =
            new Dictionary<string, StyleFontDefinition>();

        private static string _activeFontLanguage;
        private static StyleFontDefinition _activeFont;

        private static bool ActiveFontValid => _activeFont.value.fontAsset != null;

        private static StyleFontDefinition ActiveFont
        {
            get
            {
                if (ActiveFontValid) return _activeFont;

                _activeFontLanguage = Language;
                return TryLoadFontForLanguage(Language);
            }
        }

        private static void MaybeRefreshActiveFont()
        {
            // the active font can be invalidated on scene change. unfortunately unity callbacks are unreliable at
            // detecting this, so we need to check every frame.
            if (ActiveFontValid && _activeFontLanguage == Language) return;

            _activeFont = TryLoadFontForLanguage(Language);

            foreach (var fontCallback in _fontUpdateCallbacks.Values) fontCallback();
        }

        private static StyleFontDefinition TryLoadFontForLanguage(string lang)
        {
            if (FontCache.TryGetValue(lang, out var font)
                && (font.keyword != StyleKeyword.Undefined || font.value.fontAsset != null)) return font;

            var definitions = File.ReadAllLines("Packages/nadena.dev.ndmf/Editor/UI/Localization/font_preferences.txt");

            FontAsset currentFont = null;
            foreach (var line in definitions)
            {
                if (line.StartsWith("#")) continue;
                var parts = line.Split('=');
                
                if (!lang.StartsWith(parts[0])) continue;
                
                var loadedFont = FontAsset.CreateFontAsset(parts[1], "");
                if (loadedFont == null) continue;

                if (currentFont == null)
                {
                    currentFont = loadedFont;
                    currentFont.fallbackFontAssetTable = new List<FontAsset>();
                }
                else
                {
                    currentFont.fallbackFontAssetTable.Add(loadedFont);
                }
            }
             
            if (currentFont == null)
            {
                font = new StyleFontDefinition(StyleKeyword.Null);
            }
            else
            {
                font = new StyleFontDefinition(currentFont);
            }
            
            FontCache[lang] = font;

            return font;
        }

        /// <summary>
        /// Arranges to set the inheritable font style for a given visual element to the NDMF-bundled font for this
        /// language, if any.
        /// </summary>
        /// <param name="elem"></param>
        public static void ApplyFontPreferences(VisualElement elem)
        {
            elem.UnregisterCallback<AttachToPanelEvent>(AttachToPanelForFont);
            elem.UnregisterCallback<DetachFromPanelEvent>(DetachFromPanelForFont);
            
            elem.RegisterCallback<AttachToPanelEvent>(AttachToPanelForFont);
            elem.RegisterCallback<DetachFromPanelEvent>(DetachFromPanelForFont);

            if (elem.parent != null)
            {
                _fontUpdateCallbacks[elem] = () => UpdateElementFont(elem);
            }
            
            UpdateElementFont(elem);
        }

        private static void AttachToPanelForFont(AttachToPanelEvent evt)
        {
            var elem = evt.target as VisualElement;
            if (elem == null) return;
            
            _fontUpdateCallbacks[elem] = () => UpdateElementFont(elem);
            UpdateElementFont(elem);
        }

        private static void DetachFromPanelForFont(DetachFromPanelEvent evt)
        {
            var elem = evt.target as VisualElement;
            if (elem == null) return;
            
            _fontUpdateCallbacks.Remove(elem);
        }

        private static void UpdateElementFont(VisualElement elem)
        {
            elem.style.unityFontDefinition = ActiveFont;
        }
    }
}
