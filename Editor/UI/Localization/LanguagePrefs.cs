using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
                foreach (Action op in _onLanguageChangeCallbacks)
                {
                    op();
                }
            }
        }

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