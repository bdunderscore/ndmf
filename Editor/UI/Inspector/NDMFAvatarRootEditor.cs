using nadena.dev.ndmf.runtime.components;
using UnityEditor;

namespace nadena.dev.ndmf.ui.Inspector
{
    [CustomEditor(typeof(NDMFAvatarRoot))]
    public class NDMFAvatarRootEditor : Editor
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