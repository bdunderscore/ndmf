#region

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    #region

    #endregion

    // Note: Due to historical reason, "NDMF Console" is internally called "Error Report".
    public sealed class ErrorReportWindow : EditorWindow
    {
        // Disables displaying the error report window (for tests)
        public static bool DISABLE_WINDOW = false;

        private Label _avatarHeader;

        private VisualElement _errorList, _noErrorLabel, _unbuiltContainer, _noAvatarLabel;
        private ErrorReport _report;

        [SerializeField] // retain over domain reloads
        private GameObject _avatarRoot;

        private List<Button> _testBuild;

#if UNITY_2021_3_OR_NEWER
        private ToolbarMenu _selector;
#endif

        /// <summary>
        /// Gets or sets the error report currently being displayed. May be null if no error report has been generated
        /// yet. Setting this will update CurrentAvatar, pointing to the avatar that originated the error report (or
        /// null if it can't be found).
        /// </summary>
        public ErrorReport CurrentReport
        {
            get => _report;
            set
            {
                if (_report == value) return;

                _report = value;
                if (_report?.TryResolveAvatar(out _avatarRoot) != true)
                {
                    _avatarRoot = null;
                }

                if (_errorList != null)
                {
                    UpdateContents();
                }
            }
        }

        /// <summary>
        /// Gets or sets the avatar corresponding to the error report being displayed. On set, the window will search
        /// for a corresponding error report; if not found, a UI offering to run a test build will be shown instead of
        /// the error contents. 
        /// </summary>
        public GameObject CurrentAvatar
        {
            get => _avatarRoot;
            set
            {
                if (_avatarRoot == value) return;

                var avatarPath = RuntimeUtil.RelativePath(null, value);
                var report = ErrorReport.Reports.FirstOrDefault(r => r.AvatarRootPath == avatarPath);

                if (report == null)
                {
                    _report = null;
                    _avatarRoot = value;
                    UpdateContents();
                }
                else
                {
                    CurrentReport = report;
                    _avatarRoot = value;
                }
            }
        }

        [ExcludeFromDocs]
        public void CreateGUI()
        {
            minSize = new Vector2(300, 400);
            
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            root.AddToClassList("WindowRoot");

            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/ErrorReportWindow.uxml");
            VisualElement labelFromUXML = visualTree.CloneTree();
            root.Add(labelFromUXML);

            var styleSheet =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    "Packages/nadena.dev.ndmf/Editor/ErrorReporting/UI/Resources/ErrorReportWindow.uss");
            root.styleSheets.Add(styleSheet);

            NDMFLocales.L.LocalizeUIElements(root);

            var errorList = root.Q<VisualElement>("error-list-container");

            _errorList = errorList;
            _avatarHeader = root.Q<Label>("avatar-header-placeholder-label");
            _noErrorLabel = root.Q<VisualElement>("no-errors-label");
            _unbuiltContainer = root.Q<VisualElement>("unbuilt-container");
            _noAvatarLabel = root.Q<VisualElement>("no-avatar-label");

            _testBuild = root.Query<Button>(className: "test-build-button").ToList();
            foreach (var button in _testBuild)
            {
                button.clicked += TestBuild;
                button.SetEnabled(false);
            }

            SetupSelector();
            EditorApplication.hierarchyChanged += SetupSelector;

            UpdateContents();
        }

        private void OnEnable()
        {
            if (_testBuild != null)
            {
                // GUI setup done
                EditorApplication.hierarchyChanged += SetupSelector;
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= SetupSelector;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (_testBuild != null && state == PlayModeStateChange.EnteredEditMode)
            {
                // Rerender error UI in case we can now find objects that had moved during processing...
                UpdateContents();
            }
        }

        private void OnSelectionChange()
        {
            if (_testBuild == null) return;

            // On newer versions of unity, we expose an explicit selection UI that we don't want to override.
            // On Unity 2019, however, there's no way to explicitly select an avatar, so rely on editor selection UI
            // instead.
#if UNITY_2020_1_OR_NEWER
            if (_avatarRoot != null || _report != null) return;
#endif
            if (Selection.gameObjects.Length != 1) return;

            var go = Selection.activeGameObject;
            var av = RuntimeUtil.FindAvatarInParents(go.transform);
            if (av != null)
            {
                CurrentAvatar = av.gameObject;
            }
        }

        private void SetupSelector()
        {
            var container = rootVisualElement.Q<VisualElement>("avatar-selector-container");

#if UNITY_2021_3_OR_NEWER
            if (_selector != null)
            {
                container.Remove(_selector);
            }

            _selector = new ToolbarMenu();
            container.Add(_selector);
            
            _selector.text = _avatarRoot != null ? _avatarRoot.name : (_report != null ? _report.AvatarName : "<???>");

            foreach (var root in RuntimeUtil.FindAvatarRoots())
            {
                _selector.menu.AppendAction(root.name, _ =>
                {
                    CurrentAvatar = root;
                });
            }
#else
            container.style.display = DisplayStyle.None;

            var placeholder = rootVisualElement.Q<Label>("avatar-header-placeholder-label");
            placeholder.style.display = DisplayStyle.Flex;
            placeholder.text = "Avatar: " + (_report?.AvatarName ?? (_avatarRoot != null ? _avatarRoot.name : "<???>"));
#endif
        }

        /// <summary>
        /// Shows the error report window, displaying the last error report generated.
        /// </summary>
        [MenuItem("Tools/NDM Framework/Show NDMF Console")]
        public static void ShowErrorReportWindow()
        {
            if (Application.isBatchMode || DISABLE_WINDOW) return; // headless unit tests

            ShowReport(ErrorReport.Reports.LastOrDefault());
        }

        private void TestBuild()
        {
            if (_avatarRoot == null) return;

            var currentAvatar = _avatarRoot;
            var clone = Instantiate(_avatarRoot);

            try
            {
                ErrorReport.Reports.Clear();

                AvatarProcessor.ProcessAvatar(clone);

                CurrentReport = ErrorReport.Reports.FirstOrDefault();

                // HACK: If the avatar is not at the root of the scene, we end up being unable to find it (because the
                // clone is at the root). For now, we force reset the avatar root here, but we should find a more
                // reliable way to find the original avatar. This probably needs to be plugged into the platform API
                // system, so for now we'll do this workaround to ensure that the test build button remains enabled.
                // See https://github.com/bdunderscore/ndmf/issues/517

                // Note: We avoid the property accessor as it would clear the CurrentReport property.
                _avatarRoot = currentAvatar;
                UpdateContents();
            }
            finally
            {
                DestroyImmediate(clone);
                AvatarProcessor.CleanTemporaryAssets();
            }
        }

        private void UpdateContents()
        {
            _errorList.Clear();

            foreach (var button in _testBuild)
            {
                button.SetEnabled(_avatarRoot != null);
            }

            _unbuiltContainer.style.display = DisplayStyle.None;
            _errorList.style.display = DisplayStyle.None;
            _noErrorLabel.style.display = DisplayStyle.None;
            _noAvatarLabel.style.display = DisplayStyle.None;

            if (_report != null)
            {
                _errorList.style.display = DisplayStyle.Flex;
                _errorList.Clear();

                var errors = _report.Errors.OrderBy(e => e.Plugin?.DisplayName).ToList();
                PluginBase lastPlugin = null;

                foreach (var error in errors)
                {
                    if (error.Plugin != lastPlugin)
                    {
                        _errorList.Add(new GroupHeader(error.Plugin));
                        lastPlugin = error.Plugin;
                    }

                    var elem = new VisualElement();
                    elem.AddToClassList("error-list-element");
                    elem.Add(error.TheError.CreateVisualElement(_report));
                    _errorList.Add(elem);
                }

                _avatarHeader.text = "Avatar: " + _report.AvatarName;

                _noErrorLabel.style.display = _report.Errors.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else if (_avatarRoot != null)
            {
                _unbuiltContainer.style.display = DisplayStyle.Flex;
                _avatarHeader.text = "Avatar: " + _avatarRoot.name;
            }
            else
            {
                _noAvatarLabel.style.display = DisplayStyle.Flex;
            }

            SetupSelector();
        }

        /// <summary>
        /// Shows the error report window, displaying a specific error report.
        /// </summary>
        /// <param name="report"></param>
        public static void ShowReport(ErrorReport report)
        {
            if (Application.isBatchMode || DISABLE_WINDOW) return; // headless unit tests

            ErrorReportWindow wnd = GetWindow<ErrorReportWindow>();
            wnd.titleContent = new GUIContent("NDMF Console");
            wnd.CurrentReport = report;
            wnd.Show();
        }

        /// <summary>
        /// Shows the error report window, displaying a specific avatar and its error report (if any).
        /// </summary>
        /// <param name="avatarRoot"></param>
        public static void ShowReport(GameObject avatarRoot)
        {
            if (Application.isBatchMode || avatarRoot == null || DISABLE_WINDOW) return;

            ErrorReportWindow wnd = GetWindow<ErrorReportWindow>();
            wnd.titleContent = new GUIContent("NDMF Console");
            wnd.CurrentAvatar = avatarRoot;
            wnd.Show();
        }

        [MenuItem("GameObject/NDM Framework/Show NDMF Console", false)]
        private static void ShowCurrentAvatarErrorReport()
        {
            if (Selection.activeGameObject == null) return;

            ShowReport(Selection.activeGameObject);
        }

        [MenuItem("GameObject/NDM Framework/Show NDMF Console", true)]
        private static bool ShowCurrentAvatarErrorReportValidation()
        {
            return Selection.activeGameObject != null && RuntimeUtil.IsAvatarRoot(Selection.activeGameObject.transform);
        }
    }
}
