using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDKBase.Network;

namespace nadena.dev.ndmf.localization
{
    public class Localizer
    {
        private static Action _reloadLocalizations;
        
        public string DefaultLanguage { get; }

        private ImmutableSortedDictionary<string, Func<string, string>> languages;

        private string _lastLanguage = null;
        private Func<string, string> _lookupCache;
        private Func<List<(string, Func<string, string>)>> _localizationLoader;

        public Localizer(string defaultLanguage, Func<List<(string, Func<string, string>)>> lookup)
        {
            DefaultLanguage = defaultLanguage;
            LanguagePrefs.RegisterLanguage(defaultLanguage);

            _localizationLoader = lookup;
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty;
            LoadLocalizations();
            _reloadLocalizations += LoadLocalizations;
        }
        
        public Localizer(string defaultLanguage, Func<List<LocalizationAsset>> lookup)
        {
            DefaultLanguage = defaultLanguage;
            LanguagePrefs.RegisterLanguage(defaultLanguage);

            _localizationLoader = () =>
            {
                return lookup().Select<
                       LocalizationAsset,
                       (string, Func<string, string>)
                >(asset => (asset.localeIsoCode, asset.GetLocalizedString)).ToList();
            };
            
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty;
            LoadLocalizations();
            _reloadLocalizations += LoadLocalizations;
        }

        public Localizer(string defaultLanguage, Func<string, string> lookup)
        {
            DefaultLanguage = defaultLanguage;
            _localizationLoader = null;

            LanguagePrefs.RegisterLanguage(defaultLanguage);
            
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty
                .Add(defaultLanguage, lookup);
            _reloadLocalizations += LoadLocalizations;
        }

        public Localizer(LocalizationAsset asset)
        {
            DefaultLanguage = asset.localeIsoCode;
            _localizationLoader = null;

            LanguagePrefs.RegisterLanguage(asset.localeIsoCode);
            
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty
                .Add(asset.localeIsoCode, asset.GetLocalizedString);
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
            
            var newLanguages = ImmutableSortedDictionary<string, Func<string, string>>.Empty;
            foreach (var (lang, lookup) in _localizationLoader())
            {
                LanguagePrefs.RegisterLanguage(lang);
                newLanguages = newLanguages.Add(lang, lookup);
            }

            languages = newLanguages;
        }

        public static void ReloadLocalizations()
        {
            AssetDatabase.Refresh();
            _reloadLocalizations?.Invoke();
        }
        
        public Localizer WithLanguage(LocalizationAsset asset)
        {
            return WithLanguage(asset.localeIsoCode, asset.GetLocalizedString);
        }
        
        public Localizer WithLanguage(string lang, Func<string, string> lookup)
        {
            if (languages.ContainsKey(lang))
            {
                throw new ArgumentException($"Language {lang} already exists");
            }
            
            LanguagePrefs.RegisterLanguage(lang);
            
            return new Localizer(DefaultLanguage, languages.Add(lang, lookup));
        }
        
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

                if (languages.ContainsKey(LanguagePrefs.Language))
                {
                    // Always try the exact match first
                    lookups.Insert(0, languages[LanguagePrefs.Language]);
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

        public string GetLocalizedString(string key)
        {
            if (!TryGetLocalizedString(key, out var value))
            {
                value = $"<{key}>";
            }

            return value;
        }
        
        public void LocalizeUIElements(VisualElement root)
        {
            new UIElementLocalizer(this).Localize(root);
        }
    }
}