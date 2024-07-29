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

        private ImmutableList<(BuildPhase, IList<ConcretePass>)> _passes = new PluginResolver().Passes;
        private readonly List<TreeViewItemData<ItemData>> _treeViewData = new();
        private readonly List<ItemData> _flatItems = new();
        private TreeView _treeView;

        private class ItemData
        {
            public bool isPass;
            public string QualifiedName;
            public string DisplayName;
            public string PluginQualifiedName;
        }

        [MenuItem("Tools/NDM Framework/Configure Previews")]
        public static void ShowPreviewConfigWindow()
        {
            if (Application.isBatchMode || DISABLE_WINDOW) return; // headless unit tests

            GetWindow<PreviewPrefsUI>();
        }

        private PreviewPrefsUI()
        {
            BuildTreeViewData();
        }

        private void BuildTreeViewData()
        {
            var passesByPlugin =
                new PluginResolver().Passes
                    .SelectMany(kv => kv.Item2)
                    .Where(pass => pass.HasPreviews)
                    .OrderBy(pass => pass.Description)
                    .GroupBy(cp => cp.Plugin)
                    .OrderBy(g => g.Key.DisplayName)
                    .Select(g => (g.Key, g.ToList()))
                    .ToList();

            _treeViewData.Clear();
            _flatItems.Clear();

            var id = 0;
            foreach (var (plugin, passes) in passesByPlugin)
            {
                var pluginItemData = new ItemData
                {
                    isPass = false,
                    QualifiedName = plugin.QualifiedName,
                    DisplayName = plugin.DisplayName
                };
                _flatItems.Add(pluginItemData);

                var items = new List<TreeViewItemData<ItemData>>();

                foreach (var pass in passes)
                {
                    var passItemData = new ItemData
                    {
                        isPass = true,
                        QualifiedName = pass.InstantiatedPass.QualifiedName,
                        DisplayName = pass.Description,
                        PluginQualifiedName = plugin.QualifiedName
                    };

                    items.Add(new TreeViewItemData<ItemData>(id++, passItemData));
                    _flatItems.Add(passItemData);
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

            internal ItemElement()
            {
                Add(_toggle);

                _toggle.RegisterValueChangedCallback(OnValueChanged);
            }

            private void OnValueChanged(ChangeEvent<bool> evt)
            {
                if (_isPass)
                    PreviewPrefs.instance.SetPreviewPassEnabled(_qualifiedName, evt.newValue);
                else
                    PreviewPrefs.instance.SetPreviewPluginEnabled(_qualifiedName, evt.newValue);
            }

            internal void BindItem(ItemData itemData)
            {
                _toggle.text = itemData.DisplayName;

                var isChecked = itemData.isPass
                    ? PreviewPrefs.instance.IsPreviewPassEnabled(itemData.QualifiedName)
                    : PreviewPrefs.instance.IsPreviewPluginEnabled(itemData.QualifiedName);

                _toggle.SetValueWithoutNotify(isChecked);
                _isPass = itemData.isPass;
                _qualifiedName = itemData.QualifiedName;

                var isEnabled = !itemData.isPass ||
                                PreviewPrefs.instance.IsPreviewPluginEnabled(itemData.PluginQualifiedName);
                _toggle.SetEnabled(isEnabled);

                if (itemData.isPass)
                    _toggle.AddToClassList("ndmf-pass-item");
                else
                    _toggle.RemoveFromClassList("ndmf-pass-item");
            }
        }
    }
}