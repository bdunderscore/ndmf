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
        
        [MenuItem("Tools/NDM Framework/Debug Tools/Dump Scene Objects")]
        public static void DumpSceneObjects()
        {
            foreach (var scene in SceneManager.GetAllScenes())
            {
                Debug.Log($"Scene {scene.name}:");
                foreach (var gameObject in scene.GetRootGameObjects())
                {
                    Debug.Log($"  {gameObject.name}");
                }
            }
        }
        
        [MenuItem("Tools/NDM Framework/Debug Tools/Reload Domain")]
        public static void ReloadDomain()
        {
            Debug.Log("Reloading domain...");
            UnityEditor.EditorUtility.RequestScriptReload();
        }
    }
}