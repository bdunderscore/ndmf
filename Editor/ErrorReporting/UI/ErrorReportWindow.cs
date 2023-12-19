#region

using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf.ui
{
    #region

    using UnityObject = Object;

    #endregion

    public class ErrorReportWindow : EditorWindow
    {
        private Label _avatarHeader;

        private VisualElement _errorList, _noErrorLabel;
        private ErrorReport _report;
        private Button _testBuild;

        public ErrorReport CurrentReport
        {
            get => _report;
            set
            {
                _report = value;
                if (_errorList != null)
                {
                    UpdateErrorList();
                }
            }
        }

        public void CreateGUI()
        {
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

            var errorList = root.Q<VisualElement>("error-list");

            _errorList = errorList;
            _avatarHeader = root.Q<Label>("avatar-header-placeholder-label");
            _testBuild = root.Q<Button>("test-build-button");
            _testBuild.clicked += TestBuild;
            _testBuild.SetEnabled(_report != null && _report.TryResolveAvatar(out _));
            _noErrorLabel = root.Q<VisualElement>("no-errors-label");
        }

#if NDMF_DEBUG
        [MenuItem("Window/UIElements/ErrorReportWindow")]
        public static void ShowExample()
        {
            ShowReport(null);
        }
#endif

        private void TestBuild()
        {
            Debug.Log("TestBuild");
            if (_report == null) return;

            if (!_report.TryResolveAvatar(out var originalRoot)) return;
            Debug.Log("Report OK, root=" + originalRoot);

            var clone = Instantiate(originalRoot);

            try
            {
                ErrorReport.Reports.Clear();

                AvatarProcessor.ProcessAvatar(clone);

                CurrentReport = ErrorReport.Reports.FirstOrDefault();
            }
            finally
            {
                DestroyImmediate(clone);
                AvatarProcessor.CleanTemporaryAssets();
            }
        }

        void UpdateErrorList()
        {
            _errorList.Clear();

            if (_report != null)
            {
                foreach (var error in _report.Errors)
                {
                    var elem = new VisualElement();
                    elem.AddToClassList("error-list-element");
                    elem.Add(error.TheError.CreateVisualElement(_report));
                    _errorList.Add(elem);
                }

                _avatarHeader.text = "Avatar: " + _report.AvatarName;

                _testBuild.SetEnabled(FindAvatarRoot(_report) != null);
                
                _noErrorLabel.style.display = _report.Errors.Count == 0 ?
                    DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private GameObject FindAvatarRoot(ErrorReport report)
        {
            var path = report.AvatarRootPath;
            var firstSegmentIndex = path.IndexOf('/');
            var firstSegment = firstSegmentIndex > 0 ? path.Substring(0, firstSegmentIndex) : path;

            foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (sceneRoot.name == firstSegment)
                {
                    if (firstSegmentIndex > 0)
                    {
                        return sceneRoot.transform.Find(path.Substring(firstSegmentIndex + 1))?.gameObject;
                    }
                    else
                    {
                        return sceneRoot;
                    }
                }
            }

            return null;
        }

        public static void ShowReport(ErrorReport report)
        {
            ErrorReportWindow wnd = GetWindow<ErrorReportWindow>();
            wnd.titleContent = new GUIContent("NDMF Error Report");
            wnd.CurrentReport = report;
            wnd.Show();
        }
    }

    internal class TestError : SimpleError
    {
        public override ErrorCategory Category => ErrorCategory.NonFatal;
        protected override Localizer Localizer => NDMFLocales.L;
        protected override string TitleKey => "ndmf.test_error";
    }
}