using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlasticPipe.PlasticProtocol.Messages;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.localization
{
    public class Localizer
    {
        public string DefaultLanguage { get; }

        private readonly ImmutableSortedDictionary<string, Func<string, string>> languages;

        private string _lastLanguage = null;
        private Func<string, string> _lookupCache;

        public Localizer(string defaultLanguage, Func<string, string> lookup)
        {
            DefaultLanguage = defaultLanguage;

            LanguagePrefs.RegisterLanguage(defaultLanguage);
            
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty
                .Add(defaultLanguage, lookup);
        }

        public Localizer(LocalizationAsset asset)
        {
            DefaultLanguage = asset.localeIsoCode;

            LanguagePrefs.RegisterLanguage(asset.localeIsoCode);
            
            languages = ImmutableSortedDictionary<string, Func<string, string>>.Empty
                .Add(asset.localeIsoCode, asset.GetLocalizedString);
        }

        private Localizer(string defaultLanguage, ImmutableSortedDictionary<string, Func<string, string>> languages)
        {
            DefaultLanguage = defaultLanguage;
            this.languages = languages;
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
                var prefix = LanguagePrefs.Language.Split('-')[0] + "-";
                candidates.AddRange(languages.Keys.Where(k => k.StartsWith(prefix)));
                candidates.Add(DefaultLanguage);

                List<Func<string, string>> lookups = candidates.Where(languages.ContainsKey)
                    .Select(l => languages[l])
                    .ToList();

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
            return value != null;
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