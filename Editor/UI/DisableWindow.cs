#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.reporting;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal class DisableWindow : EditorWindow
    {
        [MenuItem("Tools/NDM Framework/Debug Tools/Temporally Disable Plugins", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<DisableWindow>("Temporally Disable Plugins");
        }

        private List<IPluginInternal> _plugins = null!;

        private void OnEnable()
        {
            _plugins = PluginResolver.FindAllPlugins().ToList();
            TemporalPluginDisable.OnPluginDisableChanged += OnDisableChanged;
        }

        private void OnDisable()
        {
            TemporalPluginDisable.OnPluginDisableChanged -= OnDisableChanged;
        }

        private void OnDisableChanged(string _, bool _1) => Repaint();

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "This window allows you to temporally disable plugins for the current session.\n" +
                "Configuration you made will be rolled back after restarting the Unity.",
                MessageType.None);

            foreach (var plugin in _plugins)
            {
                var disabled = TemporalPluginDisable.IsPluginDisabled(plugin.QualifiedName);
                var newDisabled = EditorGUILayout.ToggleLeft($"{plugin.DisplayName} ({plugin.QualifiedName})", disabled);
                if (newDisabled != disabled)
                {
                    TemporalPluginDisable.SetPluginDisabled(plugin.QualifiedName, newDisabled);
                }
            }
        }
    }
}