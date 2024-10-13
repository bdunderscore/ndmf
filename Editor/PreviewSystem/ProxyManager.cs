#region

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Tracks the proxy meshes created by the preview system.
    /// </summary>
    internal static class ProxyManager
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += () =>
            {
                Camera.onPreCull += OnPreCull;
                Camera.onPostRender += OnPostRender;
                EditorSceneManager.sceneSaving += (_, _) => ResetStates();
                AssemblyReloadEvents.beforeAssemblyReload += ResetStates;
            };
        }

        private static List<(Renderer, bool)> _resetActions = new();

        private static bool IsSceneCamera(Camera cam)
        {
            return cam != null && SceneView.currentDrawingSceneView?.camera == cam;
        }
        
        private static bool ShouldHookCamera(Camera cam)
        {
            if (cam.name == "TempCamera" && cam.targetTexture?.name == "ThumbnailCapture") return true;
            if (cam.transform.parent == null) return true;
            if (cam.targetTexture == null) return true;

            return false;
        }

        private static void OnPostRender(Camera cam)
        {
            ResetStates();
        }

        private static void OnPreCull(Camera cam)
        {
            ResetStates();

            bool sceneCam = IsSceneCamera(cam);
            if (!sceneCam && !ShouldHookCamera(cam)) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // TODO: fully support prefab isolation view
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;

            var sess = PreviewSession.Current;
            if (sess == null) return;

            foreach (var (original, replacement) in sess.OnPreCull(sceneCam))
            {
                // TODO: Optimize to cull meshes that don't have an active-state override registered
                if (original == null || replacement == null || !original.enabled)
                {
                    if (replacement != null) replacement.forceRenderingOff = true;
                    continue;
                }

                _resetActions.Add((original, false));
                _resetActions.Add((replacement, true));
                
                // Note: don't set replacement.forceRenderingOff to false, as we might have culled it in sess.OnPreCull
                replacement.forceRenderingOff = false;
                original.forceRenderingOff = true;
            }
        }

        private static void ResetStates()
        {
            foreach (var (renderer, state) in _resetActions)
            {
                if (renderer != null) renderer.forceRenderingOff = state;
            }

            _resetActions.Clear();
        }
    }
}