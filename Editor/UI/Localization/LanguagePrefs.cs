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
            Language = EditorPrefs.GetString(EditorPrefKey, "en-US");
        }

        /// <summary>
        /// The currently selected language ID, e.g. "en-us".
        /// </summary>
        public static string Language
        {
            get => _curLanguage;
            set
            {
                if (value == _curLanguage) return;
                _curLanguage = value;
                EditorPrefs.SetString(EditorPrefKey, value);
                TriggerLanguageChangeCallbacks();
            }
        }

        // TODO: Move to a single ConditionalWeakTable once we can use .NET 7 (which allows us to iterate this)
        private static HashSet<Action> _onLanguageChangeCallbacks = new HashSet<Action>();

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
    }
}