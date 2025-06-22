using System;
using nadena.dev.ndmf.multiplatform.components;
using UnityEditor;

namespace nadena.dev.ndmf.multiplatform.editor
{
    [CustomEditor(typeof(PortableDynamicBoneCollider))]
    internal class PortableDynamicBoneColliderEditor : Editor
    {
        private SerializedProperty p_root;
        private SerializedProperty p_colliderType;
        private SerializedProperty p_radius;
        private SerializedProperty p_height;
        private SerializedProperty p_positionOffset;
        private SerializedProperty p_rotationOffset;
        private SerializedProperty p_insideCollider;
        
        private void OnEnable()
        {
            p_root = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_root));
            p_colliderType = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_colliderType));
            p_radius = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_radius));
            p_height = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_height));
            p_positionOffset = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_positionOffset));
            p_rotationOffset = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_rotationOffset));
            p_insideCollider = serializedObject.FindProperty(nameof(PortableDynamicBoneCollider.m_insideBounds));
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(p_root);
            EditorGUILayout.PropertyField(p_colliderType);
            EditorGUILayout.PropertyField(p_radius);
            EditorGUILayout.PropertyField(p_height);
            EditorGUILayout.PropertyField(p_positionOffset);
            EditorGUILayout.PropertyField(p_rotationOffset);
            EditorGUILayout.PropertyField(p_insideCollider);

            serializedObject.ApplyModifiedProperties();
        }
    }
}