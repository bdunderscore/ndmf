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
    internal class SolverWindow : EditorWindow
    {
        [MenuItem("Tools/NDM Framework/Debug Tools/Plugin sequence display", false, 100)]
        public static void ShowWindow()
        {
            GetWindow<SolverWindow>("Plugin sequence display");
        }

        private SolverUI _solverUI;

        private void OnEnable()
        {
            _solverUI = new SolverUI();
            BuildEvent.OnBuildEvent += OnBuildEvent;
        }

        private void OnDisable()
        {
            _solverUI?.Dispose();
            BuildEvent.OnBuildEvent -= OnBuildEvent;
        }

        private void OnBuildEvent(BuildEvent ev)
        {
            if (ev is BuildEvent.BuildEnded)
            {
                _solverUI.Reload();
            }
        }

        void OnGUI()
        {
            if (_solverUI != null)
            {
                _solverUI.OnGUI(new Rect(0, 0, position.width, position.height));
            }
        }
    }

    internal class SolverUIItem : TreeViewItem
    {
        public double? ExecutionTimeMS;
        public bool IsDisabled;
        public bool IsPlugin;
    }

    internal class SolverUI : TreeView, IDisposable
    {
        private static PluginResolver Resolver = new PluginResolver(includeDisabled: true);
        private Dictionary<string, List<SolverUIItem>> _pluginItems = new();

        public SolverUI() : this(new TreeViewState())
        {
        }

        public SolverUI(TreeViewState state) : base(state)
        {
            Reload();

            PluginDisablePrefs.OnPluginDisableChanged += OnPluginDisableChanged;
        }

        private void OnPluginDisableChanged(string pluginId, bool disabled)
        {
            if (_pluginItems.TryGetValue(pluginId, out var items))
            {
                foreach (var item in items)
                {
                    item.IsDisabled = disabled;
                    foreach (var childItem in item.children.OfType<SolverUIItem>())
                    {
                        childItem.IsDisabled = disabled;
                    }
                }
            }

            Repaint();
        }

        public void Dispose()
        {
            PluginDisablePrefs.OnPluginDisableChanged -= OnPluginDisableChanged;
        }

        BuildEvent.PassExecuted NextPassExecuted(IEnumerator<BuildEvent> events)
        {
            while (events.MoveNext())
            {
                if (events.Current is BuildEvent.PassExecuted pe) return pe;
            }

            return null;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new SolverUIItem() {id = 0, depth = -1, displayName = "Avatar Build"};
            var allItems = new List<SolverUIItem>();
            _pluginItems.Clear();

            int id = 1;

            IEnumerator<BuildEvent> events = BuildEvent.LastBuildEvents.GetEnumerator();

            foreach (var phaseKVP in Resolver.Passes)
            {
                var phase = phaseKVP.Item1;
                var passes = phaseKVP.Item2;
                IPluginInternal priorPlugin = null;
                SolverUIItem pluginItem = null;

                allItems.Add(new SolverUIItem() {id = id++, depth = 1, displayName = phase.ToString()});
                var phaseItem = allItems[allItems.Count - 1];

                var isDisabled = false;

                foreach (var pass in passes)
                {
                    if (pass.InstantiatedPass.IsPhantom) continue;

                    var plugin = pass.Plugin;
                    if (plugin != priorPlugin)
                    {
                        isDisabled = PluginDisablePrefs.IsPluginDisabled(plugin.QualifiedName);
                        pluginItem = new SolverUIItem()
                        {
                            id = id++, 
                            depth = 2, 
                            displayName = $"{plugin.DisplayName} ({plugin.QualifiedName})",
                            IsDisabled = isDisabled,
                            IsPlugin = true,
                        };
                        allItems.Add(pluginItem);
                        priorPlugin = plugin;

                        if (!_pluginItems.TryGetValue(plugin.QualifiedName, out var pluginItems)) _pluginItems.Add(plugin.QualifiedName, pluginItems = new List<SolverUIItem>());
                        pluginItems.Add(pluginItem);
                    }

                    allItems.Add(new SolverUIItem() {id = id++, depth = 3, displayName = pass.Description, IsDisabled = isDisabled});

                    if (isDisabled) continue;

                    BuildEvent.PassExecuted passEvent;

                    do
                    {
                        passEvent = NextPassExecuted(events);
                    } while (passEvent != null && passEvent.QualifiedName != pass.InstantiatedPass.QualifiedName);

                    var passItem = allItems[allItems.Count - 1];
                    if (passEvent == null) continue;

                    passItem.ExecutionTimeMS = passEvent.PassExecutionTime;

                    if (passEvent.PassActivationTimes.Count > 0 || passEvent.PassDeactivationTimes.Count > 0)
                    {
                        passItem.ExecutionTimeMS = passEvent.PassExecutionTime;

                        foreach (var kvp in passEvent.PassDeactivationTimes)
                        {
                            var ty = kvp.Key;
                            var time = kvp.Value;

                            allItems.Add(new SolverUIItem()
                                {id = id++, depth = 4, displayName = $"Deactivate {ty.Name}", ExecutionTimeMS = time});
                            passItem.ExecutionTimeMS += time;
                        }

                        foreach (var kvp in passEvent.PassActivationTimes)
                        {
                            var ty = kvp.Key;
                            var time = kvp.Value;

                            allItems.Add(new SolverUIItem()
                                {id = id++, depth = 4, displayName = $"Activate {ty.Name}", ExecutionTimeMS = time});
                            passItem.ExecutionTimeMS += time;
                        }

                        allItems.Add(new SolverUIItem()
                        {
                            id = id++, depth = 4, displayName = "Pass execution",
                            ExecutionTimeMS = passEvent.PassExecutionTime
                        });
                    }

                    if (pluginItem.ExecutionTimeMS == null) pluginItem.ExecutionTimeMS = 0;
                    if (phaseItem.ExecutionTimeMS == null) phaseItem.ExecutionTimeMS = 0;
                    pluginItem.ExecutionTimeMS += passItem.ExecutionTimeMS;
                    phaseItem.ExecutionTimeMS += passItem.ExecutionTimeMS;
                }
            }

            SetupParentsAndChildrenFromDepths(root, allItems.Select(i => (TreeViewItem) i).ToList());

            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is not SolverUIItem pass)
            {
                base.RowGUI(args);
                return;
            }

            EditorGUI.BeginDisabledGroup(pass.IsDisabled);
            args.label = "";
            if (pass.ExecutionTimeMS is {} executionTimeMS) args.label += $"({executionTimeMS:F}ms) ";
            if (pass.IsDisabled && pass.IsPlugin) args.label += "(Disabled) ";
            args.label += pass.displayName;
            base.RowGUI(args);
            EditorGUI.EndDisabledGroup();
        }
    }
}