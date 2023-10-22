using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.localization
{
    internal class UIElementLocalizer
    {
        private static Dictionary<Type, Func<VisualElement, Action>> _localizers =
            new Dictionary<Type, Func<VisualElement, Action>>();

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
        
        private static ConditionalWeakTable<VisualElement, ElementFinalizer> _visualElementRefs
            = new ConditionalWeakTable<VisualElement, ElementFinalizer>();

        static UIElementLocalizer()
        {
            LanguagePrefs.OnLanguageChanged += OnLanguageChanged;
        }

        private static void OnLanguageChanged()
        {
            lock (_onLanguageChangeCallbacks)
            {
                foreach (var cb in _onLanguageChangeCallbacks)
                {
                    cb();
                }
            }
        }

        private readonly Localizer _localizer;

        public UIElementLocalizer(Localizer localizer)
        {
            _localizer = localizer;
        }
        
        private static void RegisterCallback(VisualElement elem, Action updater)
        {
            lock (_onLanguageChangeCallbacks)
            {
                if (_visualElementRefs.TryGetValue(elem, out var oldUpdater))
                {
                    _onLanguageChangeCallbacks.Remove(oldUpdater.theAction);
                }
                
                _onLanguageChangeCallbacks.Add(updater);
                ElementFinalizer ef = new ElementFinalizer(updater);
                _visualElementRefs.Add(elem, ef);
            }
        }

        internal void Localize(VisualElement elem)
        {
            WalkTree(elem);
        }

        private void WalkTree(VisualElement elem)
        {
            var ty = elem.GetType();
            
            if (elem.ClassListContains("ndmf-tr"))
            {
                var op = GetLocalizationOperation(ty);
                if (op != null)
                {
                    var cb = op(elem);
                    RegisterCallback(elem, cb);
                    cb();
                }
            }

            foreach (var child in elem.Children())
            {
                WalkTree(child);
            }
        }
        
        private Func<VisualElement, Action> GetLocalizationOperation(Type ty)
        {
            if (!_localizers.TryGetValue(ty, out var action))
            {
                PropertyInfo m_label;
                if (ty == typeof(Label))
                {
                    m_label = ty.GetProperty("text");
                }
                else
                {
                    m_label = ty.GetProperty("label");
                }

                if (m_label == null)
                {
                    action = null;
                }
                else
                {
                    action = elem =>
                    {
                        var key = m_label.GetValue(elem) as string;
                        
                        if (key != null)
                        {
                            return () =>
                            {
                                var new_label = _localizer.GetLocalizedString(key);
                                if (!_localizer.TryGetLocalizedString(key + ":tooltip", out var tooltip))
                                {
                                    tooltip = null;
                                }

                                m_label.SetValue(elem, new_label);
                                elem.tooltip = tooltip;
                            };
                        }
                        else
                        {
                            return () => { };
                        }
                    };
                }

                _localizers[ty] = action;
            }

            return action;
        }
    }
}