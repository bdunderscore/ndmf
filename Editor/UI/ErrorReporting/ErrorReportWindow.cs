using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


public class ErrorReportWindow : EditorWindow
{
    [MenuItem("Window/UIElements/ErrorReportWindow")]
    public static void ShowExample()
    {
        ErrorReportWindow wnd = GetWindow<ErrorReportWindow>();
        wnd.titleContent = new GUIContent("ErrorReportWindow");
        wnd.Show();
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;
        root.AddToClassList("WindowRoot");

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/nadena.dev.ndmf/Editor/UI/ErrorReporting/ErrorReportWindow.uxml");
        VisualElement labelFromUXML = visualTree.CloneTree();
        root.Add(labelFromUXML);

        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/nadena.dev.ndmf/Editor/UI/ErrorReporting/ErrorReportWindow.uss");
        root.styleSheets.Add(styleSheet);
        
        NDMFLocales.L.LocalizeUIElements(root);
        
    }
}