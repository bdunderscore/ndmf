using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.preview.UI
{
    public class PreviewPrefsUI : EditorWindow
    {
        internal static bool DISABLE_WINDOW = false;

        private readonly List<TreeViewItemData<ItemData>> _treeViewData = new();
        private TreeView _treeView;

        private class ItemData
        {
            public bool IsPass;
            public string QualifiedName;
            public Func<string> DisplayName;
            public string PluginQualifiedName;
            public InternalNode Node;
        }

        [MenuItem("Tools/NDM Framework/Configure Previews")]
        public static void ShowPreviewConfigWindow()
        {
            if (Application.isBatchMode || DISABLE_WINDOW) return; // headless unit tests

            GetWindow<PreviewPrefsUI>();
        }

        private void OnEnable()
        {
            BuildTreeViewData();
            PluginDisablePrefs.OnPluginDisableChanged += OnPluginDisableChanged;
        }

        private void OnPluginDisableChanged(string arg1, bool arg2) => _treeView.Rebuild();

        private void OnDisable()
        {
            PluginDisablePrefs.OnPluginDisableChanged -= OnPluginDisableChanged;
        }

        private class InternalNode
        {
            public Func<string> DisplayName;
            public string QualifiedName;

            public Action<bool> SetEnabled;

            // Action will be invoked on invalidation
            public Func<Action<bool>, bool> IsEnabled;
        }

        private List<InternalNode> NodesForPlugin(IGrouping<IPluginInternal, ConcretePass> group)
        {
            return group.SelectMany(
                    concretePass => concretePass.RenderFilters
                ).SelectMany(rf => rf.GetPreviewControlNodes())
                .Select(CreateCustomNode)
                .ToList();
        }

        private InternalNode CreateCustomNode(TogglablePreviewNode node)
        {
            return new InternalNode
            {
                DisplayName = node.DisplayName,
                QualifiedName = "",
                SetEnabled = b => { node.IsEnabled.Value = b; },
                IsEnabled = action =>
                {
                    node.IsEnabled.OnChange += action;

                    return node.IsEnabled.Value;
                }
            };
        }

        private InternalNode CreatePluginNode(PluginBase plugin)
        {
            return new InternalNode
            {
                DisplayName = () =>
                {
                    var name = "";
                    if (PluginDisablePrefs.IsPluginDisabled(plugin.QualifiedName))
                        name += "(Disabled) ";
                    name += plugin.DisplayName;
                    return name;
                },
                QualifiedName = plugin.QualifiedName,
                SetEnabled = b => { PreviewPrefs.instance.SetPreviewPluginEnabled(plugin.QualifiedName, b); },
                IsEnabled = _ => PreviewPrefs.instance.IsPreviewPluginEnabled(plugin.QualifiedName)
            };
        }

        private void BuildTreeViewData()
        {
            var nodesByPlugin =
                new PluginResolver(includeDisabled: true).Passes
                    .SelectMany(kv => kv.Item2)
                    .Where(pass => pass.HasPreviews)
                    .OrderBy(pass => pass.Description)
                    .GroupBy(cp => cp.Plugin)
                    .OrderBy(g => g.Key.DisplayName)
                    .Select(g => (g.Key, NodesForPlugin(g)))
                    .ToList();

            _treeViewData.Clear();

            var id = 0;
            foreach (var (plugin, nodes) in nodesByPlugin)
            {
                var pluginNode = CreatePluginNode((PluginBase)plugin);
                var pluginItemData = new ItemData
                {
                    IsPass = false,
                    QualifiedName = plugin.QualifiedName,
                    DisplayName = pluginNode.DisplayName,
                    Node = pluginNode
                };

                var items = new List<TreeViewItemData<ItemData>>();

                foreach (var node in nodes)
                {
                    var passItemData = new ItemData
                    {
                        IsPass = true,
                        Node = node,
                        QualifiedName = node.QualifiedName,
                        DisplayName = node.DisplayName,
                        PluginQualifiedName = plugin.QualifiedName
                    };

                    items.Add(new TreeViewItemData<ItemData>(id++, passItemData));
                }

                _treeViewData.Add(new TreeViewItemData<ItemData>(id++, pluginItemData, items));
            }
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent(NDMFLocales.L.GetLocalizedString("PreviewEnable:Title"));
            minSize = new Vector2(300, 400);

            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;
            root.AddToClassList("WindowRoot");

            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/nadena.dev.ndmf/Editor/PreviewSystem/UI/Resources/PreviewPrefsWindow.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            root.Add(labelFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/PreviewSystem/UI/Resources/PreviewPrefsWindow.uss");
            root.styleSheets.Add(styleSheet);

            NDMFLocales.L.LocalizeUIElements(root);

            _treeView = root.Q<TreeView>("PreviewEnableTree");
            _treeView.SetRootItems(_treeViewData);
            _treeView.makeItem = MakeItem;
            _treeView.bindItem = BindItem;
            _treeView.selectionType = SelectionType.None;
            _treeView.Rebuild();
        }

        private void BindItem(VisualElement elem, int id)
        {
            var itemData = _treeView.GetItemDataForIndex<ItemData>(id);
            var itemElem = elem as ItemElement;

            itemElem?.BindItem(itemData);
        }

        private VisualElement MakeItem()
        {
            return new ItemElement();
        }

        private class ItemElement : VisualElement
        {
            private readonly Toggle _toggle = new();
            private bool _isPass;
            private string _qualifiedName = "";
            private ItemData _current;

            private readonly Action<bool> RebindDelegate;

            internal ItemElement()
            {
                RebindDelegate = _ => Rebind();
                
                Add(_toggle);

                _toggle.RegisterValueChangedCallback(OnValueChanged);
                LanguagePrefs.RegisterLanguageChangeCallback(this, OnLanguageChange);
            }

            private static void OnLanguageChange(ItemElement elem)
            {
                if (elem._current != null) elem._toggle.text = elem._current.DisplayName();
            }

            private void OnValueChanged(ChangeEvent<bool> evt)
            {
                _current.Node.SetEnabled(evt.newValue);

                // Redraw all scene views after a delay to ensure the change takes effect
                EditorApplication.delayCall += SceneView.RepaintAll;
            }

            internal void BindItem(ItemData itemData)
            {
                _current = itemData;
                _toggle.text = itemData.DisplayName();

                _isPass = itemData.IsPass;
                _qualifiedName = itemData.QualifiedName;

                if (itemData.IsPass)
                    _toggle.AddToClassList("ndmf-pass-item");
                else
                    _toggle.RemoveFromClassList("ndmf-pass-item");

                Rebind();
            }

            private void Rebind()
            {
                var isChecked = _current.Node.IsEnabled(RebindDelegate);

                _toggle.SetValueWithoutNotify(isChecked);
            }
        }
    }
}