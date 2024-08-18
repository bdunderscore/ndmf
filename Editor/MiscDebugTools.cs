using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.ndmf.ui
{
    internal static class MiscDebugTools
    {
        [MenuItem("Tools/NDM Framework/Debug Tools/Dump Scenes")]
        public static void DumpScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Debug.Log($"Scene {i}: {scene.name}");
            }
        }
    }
}