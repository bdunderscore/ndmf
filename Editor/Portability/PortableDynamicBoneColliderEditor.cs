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
        
        private void OnEnable()
        {
            p_root = serializedObject.FindProperty("root");
            p_colliderType = serializedObject.FindProperty("colliderType");
            p_radius = serializedObject.FindProperty("radius");
            p_height = serializedObject.FindProperty("height");
            p_positionOffset = serializedObject.FindProperty("positionOffset");
            p_rotationOffset = serializedObject.FindProperty("rotationOffset");
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

            serializedObject.ApplyModifiedProperties();
        }
    }
}