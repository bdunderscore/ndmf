using System;
using System.Linq;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.ui
{
    internal class ExperimentalFeaturesControlWindow : EditorWindow
    {
        private const string ResourcesRoot = "packages/nadena.dev.ndmf/Editor/UI/ExperimentalFeaturesWindow/Resources";
        private const string UXMLPath = ResourcesRoot + "/ExperimentalFeaturesWindow.uxml";
        private const string USSPath = ResourcesRoot + "/ExperimentalFeaturesWindow.uss";
        
        [MenuItem(Menus.EXPERIMENTAL_FEATURES, priority = Menus.EXPERIMENTAL_FEATURES_PRIO)]
        private static void ShowWindow()
        {
            var window = GetWindow<ExperimentalFeaturesControlWindow>();
            window.titleContent = new GUIContent("NDMF Experimental Features");
            window.Show();
        }

        private const string ExperimentalDefine = "NDMF_EXPERIMENTAL";
        private Toggle m_toggle;
        private VisualElement m_interactable;

        private void OnEnable()
        {
            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var isEnabled = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup).Split(';').Contains(ExperimentalDefine);
            
            // Load UXML and USS
            var xml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXMLPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(USSPath);
            
            if (xml == null || style == null)
            {
                Debug.LogError($"Failed to load UXML or USS from {UXMLPath} or {USSPath}");
                return;
            }
            
            var root = xml.CloneTree();
            root.styleSheets.Add(style);
            
            NDMFLocales.L.LocalizeUIElements(root);
            
            rootVisualElement.Add(root);
            m_toggle = root.Q<Toggle>("enable-experimental-features");
            m_toggle.value = isEnabled;
            m_interactable = root.Q("interactable");
            
            var btn_apply = root.Q<Button>("apply-button");
            btn_apply.clicked += OnClicked;
            
            minSize = new Vector2(400, 125);
        }

        void OnClicked()
        {
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            var defines = currentSymbols.Split(';').ToList();

            if (m_toggle.value)
            {
                if (!defines.Contains(ExperimentalDefine))
                {
                    defines.Add(ExperimentalDefine);
                }
            }
            else
            {
                defines.Remove(ExperimentalDefine);
            }

            var newSymbols = string.Join(";", defines);
            if (newSymbols != currentSymbols)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, newSymbols);
                m_interactable.SetEnabled(false);
            }
        }
    }
}