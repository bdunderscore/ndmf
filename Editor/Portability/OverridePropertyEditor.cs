using nadena.dev.ndmf.multiplatform.components;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.editor
{
    [CustomPropertyDrawer(typeof(IOverrideProperty), true)]
    internal class OverridePropertyEditor : PropertyDrawer
    {
        // TODO - UI Elements implementation

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var m_override = property.FindPropertyRelative("m_override");
            var m_value = property.FindPropertyRelative("m_value");

            if (!m_override.boolValue) return EditorGUIUtility.singleLineHeight;
            
            return EditorGUI.GetPropertyHeight(m_value, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            var m_override = property.FindPropertyRelative("m_override");
            var m_value = property.FindPropertyRelative("m_value");
            
            // Align checkbox (no label) on the left
            var checkBoxRect = new Rect(position.x, position.y, EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight);

            m_override.boolValue = EditorGUI.ToggleLeft(checkBoxRect, new GUIContent(), m_override.boolValue);

            using (var _ = new EditorGUI.DisabledScope(m_override.boolValue == false))
            {
                var remainingRect = new Rect(position.x + checkBoxRect.width * 2, position.y, position.width - checkBoxRect.width, position.height);

                if (m_override.boolValue)
                {
                    EditorGUI.PropertyField(remainingRect, m_value, label, true);
                }
                else
                {
                    var leftHalf = new Rect(remainingRect.x, remainingRect.y, remainingRect.width / 2, remainingRect.height);
                    var rightHalf = new Rect(remainingRect.x + remainingRect.width / 2, remainingRect.y, remainingRect.width / 2, remainingRect.height);
                    
                    EditorGUI.LabelField(leftHalf, label);
                    EditorGUI.LabelField(rightHalf, "(inherited)");
                }
            }
            
            EditorGUI.EndProperty();
        }
    }
}