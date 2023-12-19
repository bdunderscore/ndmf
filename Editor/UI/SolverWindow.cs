#region

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
    }

    internal class SolverUI : TreeView
    {
        private static PluginResolver Resolver = new PluginResolver();

        public SolverUI() : this(new TreeViewState())
        {
        }

        public SolverUI(TreeViewState state) : base(state)
        {
            Reload();
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

                foreach (var pass in passes)
                {
                    if (pass.InstantiatedPass.IsPhantom) continue;

                    var plugin = pass.Plugin;
                    if (plugin != priorPlugin)
                    {
                        allItems.Add(new SolverUIItem() {id = id++, depth = 2, displayName = plugin.DisplayName});
                        priorPlugin = plugin;
                        pluginItem = allItems[allItems.Count - 1];
                    }

                    allItems.Add(new SolverUIItem() {id = id++, depth = 3, displayName = pass.Description});
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

            foreach (var pass in allItems)
            {
                if (pass.ExecutionTimeMS.HasValue)
                {
                    pass.displayName = $"({pass.ExecutionTimeMS:F}ms) {pass.displayName}";
                }
            }

            SetupParentsAndChildrenFromDepths(root, allItems.Select(i => (TreeViewItem) i).ToList());

            return root;
        }
    }
}