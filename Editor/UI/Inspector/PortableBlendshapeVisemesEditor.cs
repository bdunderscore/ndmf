#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.ui.inspector
{
    [CustomEditor(typeof(PortableBlendshapeVisemes))]
    internal class PortableBlendshapeVisemesEditor : Editor
    {
        private SerializedProperty prop_targetRenderer;
        private SerializedProperty prop_shapeList;
        
        private void OnEnable()
        {
            prop_targetRenderer = serializedObject.FindProperty(nameof(PortableBlendshapeVisemes.m_targetRenderer));
            prop_shapeList = serializedObject.FindProperty(nameof(PortableBlendshapeVisemes.m_shapes));
        }
        
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This is an experimental component. It may change or be removed without notice.",
                MessageType.Warning
            );
            
            serializedObject.Update();
            
            EditorGUILayout.PropertyField(prop_targetRenderer);

            List<string>? meshShapeList = null;
            
            if (prop_targetRenderer.objectReferenceValue is SkinnedMeshRenderer smr && smr != null && smr.sharedMesh != null)
            {
                int blendShapeCount = smr.sharedMesh.blendShapeCount;
                
                meshShapeList = new List<string>(blendShapeCount);
                for (int i = 0; i < blendShapeCount; i++)
                {
                    meshShapeList.Add(smr.sharedMesh.GetBlendShapeName(i));
                }
            }
            
            GUIContent[]? meshShapeGuiContents = meshShapeList?.Select(s => new GUIContent(s)).ToArray();
            
            EditorGUILayout.Separator();
            
            Dictionary<string, int> visemeList = new Dictionary<string, int>();

            for (int i = 0; i < prop_shapeList.arraySize; i++)
            {
                var entry = prop_shapeList.GetArrayElementAtIndex(i)
                    .FindPropertyRelative(nameof(PortableBlendshapeVisemes.Shape.VisemeName))
                    .stringValue;

                if (entry == null) continue;
                
                visemeList.TryAdd(entry, i);
            }

            int maxShape = prop_shapeList.arraySize;
            foreach (var knownShape in CommonAvatarInfo.KnownVisemes)
            {
                if (!visemeList.ContainsKey(knownShape)) visemeList.Add(knownShape, maxShape++);
            } 

            if (maxShape > prop_shapeList.arraySize)
            {
                int cutoff = prop_shapeList.arraySize;
                prop_shapeList.arraySize = maxShape;
                foreach ((var shape, var index) in visemeList)
                {
                    if (index >= cutoff)
                    {
                        var entry = prop_shapeList.GetArrayElementAtIndex(index);
                        var visemeName = entry.FindPropertyRelative(nameof(PortableBlendshapeVisemes.Shape.VisemeName));
                        var blendshape = entry.FindPropertyRelative(nameof(PortableBlendshapeVisemes.Shape.Blendshape));
                        
                        visemeName.stringValue = shape;
                        blendshape.stringValue = null;
                    }
                }
            }
            
            for (int i = 0; i < prop_shapeList.arraySize; i++)
            {
                var entry = prop_shapeList.GetArrayElementAtIndex(i);
                var visemeName = entry.FindPropertyRelative(nameof(PortableBlendshapeVisemes.Shape.VisemeName));
                var blendshape = entry.FindPropertyRelative(nameof(PortableBlendshapeVisemes.Shape.Blendshape));
                
                EditorGUILayout.BeginHorizontal();

                var label = visemeName.stringValue;
                if (string.IsNullOrEmpty(label))
                {
                    label = "???";
                }

                int currentSelectedIndex = meshShapeList?.FindIndex(s => s == blendshape.stringValue) ?? -1; 
                if (meshShapeList == null || (currentSelectedIndex < 0 && !string.IsNullOrWhiteSpace(blendshape.stringValue)))
                {
                    EditorGUILayout.PropertyField(blendshape, new GUIContent(label));
                }
                else
                {
                    // Create a dropdown using the meshShapeList
                    var rect = EditorGUILayout.GetControlRect();

                    var labelContent = EditorGUI.BeginProperty(rect, new GUIContent(label), blendshape);
                    
                    currentSelectedIndex = EditorGUI.Popup(rect, labelContent, currentSelectedIndex, meshShapeGuiContents!);
                    if (currentSelectedIndex >= 0)
                    {
                        blendshape.stringValue = meshShapeList[currentSelectedIndex];
                    }
                }
                
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    if (CommonAvatarInfo.KnownVisemes.Contains(visemeName.stringValue))
                    {
                        blendshape.stringValue = null;
                    }
                    else
                    {
                        prop_shapeList.DeleteArrayElementAtIndex(i);
                    }

                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}