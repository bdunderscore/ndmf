using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.preview.UI
{
    internal class PreviewPrefs : ScriptableSingleton<PreviewPrefs>
    {
        [SerializeField] private List<string> _disabledPreviewPasses = new();
        private ImmutableHashSet<string> _disabledPreviewPassesSet;

        [SerializeField] private List<string> _disabledPreviewPlugins = new();
        private ImmutableHashSet<string> _disabledPreviewPluginsSet;

        public event Action OnPreviewConfigChanged;

        private void OnValidate()
        {
            _disabledPreviewPassesSet = _disabledPreviewPasses.ToImmutableHashSet();
            _disabledPreviewPluginsSet = _disabledPreviewPlugins.ToImmutableHashSet();
        }

        public bool IsPreviewPassEnabled(string qualifiedName)
        {
            if (_disabledPreviewPassesSet == null) OnValidate();

            return !_disabledPreviewPassesSet.Contains(qualifiedName);
        }

        public bool IsPreviewPluginEnabled(string qualifiedName)
        {
            if (_disabledPreviewPluginsSet == null) OnValidate();

            return !_disabledPreviewPluginsSet.Contains(qualifiedName);
        }

        public void SetPreviewPassEnabled(string qualifiedName, bool enabled)
        {
            if (enabled)
                _disabledPreviewPasses.Remove(qualifiedName);
            else
                _disabledPreviewPasses.Add(qualifiedName);

            OnValidate();

            OnPreviewConfigChanged?.Invoke();
        }

        public void SetPreviewPluginEnabled(string qualifiedName, bool enabled)
        {
            if (enabled)
                _disabledPreviewPlugins.Remove(qualifiedName);
            else
                _disabledPreviewPlugins.Add(qualifiedName);

            OnValidate();

            OnPreviewConfigChanged?.Invoke();
        }
    }
}