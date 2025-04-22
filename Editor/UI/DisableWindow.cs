#region

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal class DisableWindow : EditorWindow
    {
        [MenuItem("Tools/NDM Framework/Debug Tools/Enable-Disable Plugins", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<DisableWindow>("[NDMF] Enable-Disable Plugins");
        }

        private List<IPluginInternal> _plugins = null!;

        private void OnEnable()
        {
            _plugins = PluginResolver.FindAllPlugins().ToList();
            PluginDisablePrefs.OnPluginDisableChanged += OnDisableChanged;
        }

        private void OnDisable()
        {
            PluginDisablePrefs.OnPluginDisableChanged -= OnDisableChanged;
        }

        private void OnDisableChanged(string _, bool _1) => Repaint();

        private Vector2 _scrollPos = Vector2.zero;
        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "This window allows you to temporarily disable plugins for the current session.\n" +
                "Changes you make here will be reverted after restarting Unity.",
                MessageType.None);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable All Plugins"))
                {
                    foreach (var plugin in _plugins)
                    {
                        PluginDisablePrefs.SetPluginDisabled(plugin.QualifiedName, false);
                    }
                }
                if (GUILayout.Button("Disable All Plugins"))
                {
                    foreach (var plugin in _plugins)
                    {
                        PluginDisablePrefs.SetPluginDisabled(plugin.QualifiedName, true);
                    }
                }
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var plugin in _plugins)
            {
                var disabled = PluginDisablePrefs.IsPluginDisabled(plugin.QualifiedName);
                var newDisabled = !EditorGUILayout.ToggleLeft($"{plugin.DisplayName} ({plugin.QualifiedName})", !disabled);
                if (newDisabled != disabled)
                {
                    PluginDisablePrefs.SetPluginDisabled(plugin.QualifiedName, newDisabled);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}