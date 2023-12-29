using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace nadena.dev.ndmf.localization
{
    /// <summary>
    /// Tracks the currently selected UI language
    /// </summary>
    public static class LanguagePrefs
    {
        private static string _curLanguage = "en-us";

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
                    op();
                }
            }
        }

        /// <summary>
        /// Returns a list of all languages which have been registered at some point with the localization system.
        /// </summary>
        public static ImmutableSortedSet<string> RegisteredLanguages { get; private set; }

        static LanguagePrefs()
        {
            RegisteredLanguages = ImmutableSortedSet<string>.Empty;
        }

        /// <summary>
        /// Registers an additional language code to display in the language selectors.
        /// </summary>
        /// <param name="languageCode"></param>
        public static void RegisterLanguage(string languageCode)
        {
            RegisteredLanguages = RegisteredLanguages.Add(languageCode);
        }
    }
}