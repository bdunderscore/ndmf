using System;
using nadena.dev.ndmf.multiplatform.components;
using UnityEditor;

namespace nadena.dev.ndmf.multiplatform.editor
{
    [CustomEditor(typeof(PortableDynamicBone))]
    internal class PortableDynamicBoneEditor : Editor
    {
        private SerializedProperty p_root;
        private SerializedProperty p_templateName;
        private SerializedProperty p_baseRadius;
        private SerializedProperty p_ignoreTransforms;
        private SerializedProperty p_isGrabbable;
        private SerializedProperty p_colliders;
        
        private void OnEnable()
        {
            p_root = serializedObject.FindProperty("m_root");
            p_templateName = serializedObject.FindProperty("m_templateName");
            p_baseRadius = serializedObject.FindProperty("m_baseRadius");
            p_ignoreTransforms = serializedObject.FindProperty("m_ignoreTransforms");
            p_isGrabbable = serializedObject.FindProperty("m_isGrabbable");
            p_colliders = serializedObject.FindProperty("m_colliders");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(p_root);
            EditorGUILayout.PropertyField(p_templateName);
            EditorGUILayout.PropertyField(p_baseRadius);
            EditorGUILayout.PropertyField(p_ignoreTransforms);
            EditorGUILayout.PropertyField(p_isGrabbable);
            EditorGUILayout.PropertyField(p_colliders);

            serializedObject.ApplyModifiedProperties();
        }
    }
}