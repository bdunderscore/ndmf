using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.localization
{
    /// <summary>
    /// Provides a way to translate language lookup keys into localized strings.
    /// </summary>
    public sealed class Localizer
    {
        private static Action _reloadLocalizations;

        /// <summary>
        /// The default (fallback) language to use to look up keys when they are missing in the currently selected
        /// UI language.
        /// </summary>
        public string DefaultLanguage { get; }

        private ImmutableSortedDictionary<string, Func<string, string>> languages;

        private string _lastLanguage = null;
        private Func<string, string> _lookupCache;
        private Func<List<(string, Func<string, string>)>> _localizationLoader;

        /// <summary>
        /// Constructs a Localizer based on a callback which loads from some external source of localizations.
        /// The function is expected to return a list of (language, lookup) pairs, where lookup is a function which
        /// attempts to look up a single string by its key, returning null if not found.
        ///
        /// This function may be called multiple times if localizations are reloaded.
        /// </summary>
        /// <param name="defaultLanguage">The default language code to use as a fallback when strings are missing</param>
        /// <param name="loader"></param>
        public Localizer(string defaultLanguage, Func<List<(string, Func<string, string>)>> loader)
        {
            DefaultLanguage = CultureInfo.GetCultureInfo(defaultLanguage).Name;
            LanguagePrefs.RegisterLanguage(defaultLanguage);

            _localizationLoader = loader;
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty;
            LoadLocalizations();
            _reloadLocalizations += LoadLocalizations;
        }

        /// <summary>
        /// Constructs a localizer based on a list of LocalizationAssets.
        /// </summary>
        /// <param name="defaultLanguage">The default language code to use as a fallback when strings are missing</param>
        /// <param name="assetLoader">A function which loads LocalizationAssets</param>
        public Localizer(string defaultLanguage, Func<List<LocalizationAsset>> assetLoader)
        {
            DefaultLanguage = defaultLanguage;
            LanguagePrefs.RegisterLanguage(defaultLanguage);

            _localizationLoader = () =>
            {
                return assetLoader().Select<
                    LocalizationAsset,
                    (string, Func<string, string>)
                >(asset => (asset.localeIsoCode, k =>
                {
                    var s = asset.GetLocalizedString(k);
                    if (s == k) s = null;
                    return s;
                })).ToList();
            };

            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
            LoadLocalizations();
            _reloadLocalizations += LoadLocalizations;
        }

        private Localizer(string defaultLanguage, ImmutableSortedDictionary<string, Func<string, string>> languages)
        {
            DefaultLanguage = defaultLanguage;
            this.languages = languages;
        }

        void LoadLocalizations()
        {
            _lookupCache = null;

            if (_localizationLoader == null) return;

            var newLanguages = ImmutableSortedDictionary<string, Func<string, string>>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
            foreach (var (lang, lookup) in _localizationLoader())
            {
                var normalizedLang = CultureInfo.GetCultureInfo(lang).Name;
                LanguagePrefs.RegisterLanguage(normalizedLang);
                newLanguages = newLanguages.Add(normalizedLang, lookup);
            }

            languages = newLanguages;
        }

        /// <summary>
        /// Reloads all localizations from their loader functions.
        /// </summary>
        public static void ReloadLocalizations()
        {
            AssetDatabase.Refresh();
            _reloadLocalizations?.Invoke();
        }

        /// <summary>
        /// Attempts to look up a localized string. Returns true if the string was found, false otherwise.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetLocalizedString(string key, out string value)
        {
            if (_lookupCache == null || _lastLanguage != LanguagePrefs.Language)
            {
                List<string> candidates = new List<string>();
                candidates.Add(LanguagePrefs.Language);
                var baseLang = LanguagePrefs.Language.Split('-')[0];
                var prefix = baseLang + "-";
                candidates.AddRange(languages.Keys.Where(k => k == baseLang || k.StartsWith(prefix)));
                candidates.Add(DefaultLanguage);

                List<Func<string, string>> lookups = candidates.Where(languages.ContainsKey)
                    .Select(l => languages[l])
                    .ToList();

                if (languages.TryGetValue(LanguagePrefs.Language, out var currentLookup))
                {
                    // Always try the exact match first
                    lookups.Insert(0, currentLookup);
                }

                _lookupCache = k =>
                {
                    foreach (var lookup in lookups)
                    {
                        var s = lookup(k);
                        if (s != null) return s;
                    }

                    return null;
                };
                _lastLanguage = LanguagePrefs.Language;
            }

            value = _lookupCache(key);
            return value != null && value != key;
        }

        /// <summary>
        /// Obtains a localized string, or a placeholder if it cannot be found.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetLocalizedString(string key)
        {
            if (!TryGetLocalizedString(key, out var value))
            {
                value = $"<{key}>";
            }

            return value;
        }

        /// <summary>
        /// Localizes UI elements under the given root element. Any elements with the class "ndmf-tr" will be
        /// localized automatically, with localization keys under their `text` or `label` properties being converted
        /// into localized strings. These elements will automatically update when the currently selected language
        /// changes.
        /// </summary>
        /// <param name="root"></param>
        public void LocalizeUIElements(VisualElement root)
        {
            new UIElementLocalizer(this).Localize(root);
        }
    }
}