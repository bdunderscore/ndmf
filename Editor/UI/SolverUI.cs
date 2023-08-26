using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace nadena.dev.build_framework.ui
{
    public class SolverWindow : EditorWindow
    {
        [MenuItem("Window/[ABPF] Plugin sequence display")]
        public static void ShowWindow()
        {
            GetWindow<SolverWindow>("Plugin sequence display");
        }
        
        private SolverUI _solverUI;
        
        private void OnEnable()
        {
            _solverUI = new SolverUI();
        }
        
        void OnGUI()
        {
            _solverUI.OnGUI(new UnityEngine.Rect(0, 0, position.width, position.height));
        }
    }
    
    public class SolverUI : TreeView
    {
        private static PluginResolver Resolver = new PluginResolver();
        
        public SolverUI() : this(new TreeViewState())
        {
        }
        
        public SolverUI(TreeViewState state) : base(state)
        {
            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem() {id = 0, depth = -1, displayName = "Avatar Build"};
            var allItems = new List<TreeViewItem>();
            
            int id = 1;

            foreach (var phaseKVP in Resolver.Passes)
            {
                var phase = phaseKVP.Key;
                var passes = phaseKVP.Value;
                string priorPluginName = null;

                allItems.Add(new TreeViewItem() {id = id++, depth = 1, displayName = phase.ToString()});

                foreach (var pass in passes)
                {
                    string pluginName = pass.InstantiatedPass.QualifiedName.Split('/')[0];
                    if (pluginName != priorPluginName)
                    {
                        allItems.Add(new TreeViewItem() {id = id++, depth = 2, displayName = pluginName});
                        priorPluginName = pluginName;
                    }
                    
                    allItems.Add(new TreeViewItem() {id = id++, depth = 3, displayName = pass.Description});
                }
            }
            
            SetupParentsAndChildrenFromDepths(root, allItems);

            return root;
        }
    }
}