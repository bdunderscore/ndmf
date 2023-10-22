using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace nadena.dev.ndmf.localization
{
    public static class LanguagePrefs
    {
        private static string _curLanguage = "en-US";

        public static string Language
        {
            get => _curLanguage;
            set
            {
                if (value == _curLanguage) return;
                _curLanguage = value;
                OnLanguageChanged?.Invoke();
            }
        }

        public static event Action OnLanguageChanged;

        public static ImmutableSortedSet<string> RegisteredLanguages
        {
            get;
            private set;
        }

        static LanguagePrefs()
        {
            RegisteredLanguages = ImmutableSortedSet<string>.Empty;
        }
        
        public static void RegisterLanguage(string defaultLanguage)
        {
            RegisteredLanguages = RegisteredLanguages.Add(defaultLanguage);
        }
    }
}