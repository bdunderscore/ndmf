using nadena.dev.ndmf.runtime.components;
using UnityEditor;

namespace nadena.dev.ndmf.ui.inspector
{
    [CustomEditor(typeof(NDMFViewpoint))]
    internal class NDMFViewpointEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This is an experimental component. It may change or be removed without notice.",
                MessageType.Warning
            );
        }
    }
}