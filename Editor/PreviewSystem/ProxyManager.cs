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
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            EditorSceneManager.sceneSaving += (_, _) => ResetStates();
            AssemblyReloadEvents.beforeAssemblyReload += ResetStates;
        }

        private static List<(Renderer, bool)> _resetActions = new();

        private static void OnPostRender(Camera cam)
        {
            ResetStates();
        }

        private static void OnPreCull(Camera cam)
        {
            ResetStates();

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var sess = PreviewSession.Current;
            if (sess == null) return;

            foreach (var (original, replacement) in sess.GetReplacements())
            {
                if (original == null || replacement == null || !original.enabled ||
                    !original.gameObject.activeInHierarchy)
                {
                    if (replacement != null) replacement.forceRenderingOff = true;
                    continue;
                }

                _resetActions.Add((original, false));
                _resetActions.Add((replacement, true));

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