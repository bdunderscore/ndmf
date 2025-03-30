using System.Text;
using nadena.dev.ndmf.cs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nadena.dev.ndmf.preview.trace
{
    internal class TraceWindow : EditorWindow
    {
        [MenuItem("Tools/NDM Framework/Debug Tools/Preview trace")]
        public static void ShowWindow()
        {
            GetWindow<TraceWindow>("Preview trace");
        }

        [SerializeField] StyleSheet uss;
        [SerializeField] VisualTreeAsset uxml;

        private VisualElement _events;
        private string _eventText;
        
        private void OnEnable()
        {
            EditorApplication.delayCall += LoadUI;
        }

        private void OnDisable()
        {
            ObjectWatcher.Instance.PropertyMonitor.IsEnabled = true;
        }

        private void LoadUI()
        {
            ObjectWatcher.Instance.PropertyMonitor.IsEnabled = false;
            
            var root = rootVisualElement;
            root.Clear();
            
            root.styleSheets.Add(uss);
            uxml.CloneTree(root);
            
            _events = root.Q<VisualElement>("events");

            root.Q<Button>("btn_refresh").clickable.clicked += () =>
            {
                var events = TraceBuffer.FormatTraceBuffer(256);
                
                _events.Clear();

                var copyPasteText = new StringBuilder();
                foreach (var (label, eventText) in events)
                {
                    copyPasteText.AppendLine("\n=== " + label + " ===");
                    copyPasteText.AppendLine(eventText);

                    var foldout = new Foldout();
                    foldout.text = label;
                    
                    var labelCtl = new Label(eventText);
                    foldout.Add(labelCtl);
                    
                    _events.Add(foldout);
                }
                
                _eventText = copyPasteText.ToString();
            };
            
            root.Q<Button>("btn_copy").clickable.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = _eventText;
            };
            
            root.Q<Button>("btn_clear").clickable.clicked += () =>
            {
                TraceBuffer.Clear();
                _events.Clear();
            };
            
            root.Q<Toggle>("tgl_propmon").RegisterValueChangedCallback(evt =>
            {
                ObjectWatcher.Instance.PropertyMonitor.IsEnabled = evt.newValue;
            });
        }
    }
}