using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.preview.UI
{
    internal class PreviewPrefs : ScriptableSingleton<PreviewPrefs>
    {
        [SerializeField] private List<string> _disabledPreviewPlugins = new();
        private ImmutableHashSet<string> _disabledPreviewPluginsSet;

        [Serializable]
        private struct KeyValue
        {
            public string key;
            public bool value;
        }

        [SerializeField] private List<KeyValue> _savedNodeStates = new();
        private Dictionary<string, bool> _nodeStates = new();
        
        public event Action OnPreviewConfigChanged;

        private void OnValidate()
        {
            if (_disabledPreviewPlugins == null)
                _disabledPreviewPlugins = new List<string>();
            if (_savedNodeStates == null)
                _savedNodeStates = new List<KeyValue>();
            
            _disabledPreviewPluginsSet = _disabledPreviewPlugins.ToImmutableHashSet();
            _nodeStates = _savedNodeStates.ToDictionary(kv => kv.key, kv => kv.value);
        }

        private void OnEnable()
        {
            PluginDisablePrefs.OnPluginDisableChanged += (_, _) => OnPreviewConfigChanged?.Invoke();
        }

        public bool GetNodeState(string qualifiedName, bool defaultValue)
        {
            if (_nodeStates == null) OnValidate();

            return _nodeStates.TryGetValue(qualifiedName, out var value) ? value : defaultValue;
        }

        public void SetNodeState(string qualifiedName, bool value)
        {
            if (_nodeStates == null) OnValidate();

            _nodeStates[qualifiedName] = value;
            _savedNodeStates = _nodeStates.Select(kv => new KeyValue { key = kv.Key, value = kv.Value }).ToList();

            EditorUtility.SetDirty(this);
        }

        public bool IsPreviewPluginEnabled(string qualifiedName)
        {
            if (_disabledPreviewPluginsSet == null) OnValidate();

            return !_disabledPreviewPluginsSet.Contains(qualifiedName);
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