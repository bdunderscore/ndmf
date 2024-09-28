using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace nadena.dev.ndmf.ui
{
    internal static class MiscDebugTools
    {
        #if NDMF_DEBUG
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
            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Debug.Log($"Scene {scene.name}:");
                foreach (var gameObject in scene.GetRootGameObjects())
                {
                    Debug.Log($"  {gameObject.name}");
                }
            }
        }
        
        [MenuItem("Tools/NDM Framework/Debug Tools/Dump Prefab Stage Objects (recursive)")]
        public static void DumpPrefabStageObjects()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                Debug.Log("No prefab stage active.");
                return;
            }
 
            Debug.Log($"Prefab stage {prefabStage.name}:");
            DumpPrefabStageObjectsRecursive(prefabStage.prefabContentsRoot, indent: 0);

            void DumpPrefabStageObjectsRecursive(GameObject obj, int indent)
            {
                Debug.Log(new string(' ', indent * 2) + obj.name);
                foreach (Transform child in obj.transform)
                {
                    DumpPrefabStageObjectsRecursive(child.gameObject, indent + 1);
                }
            }
        }
        
        [MenuItem("Tools/NDM Framework/Debug Tools/Create GameObject at root")]
        public static void CreateGameObjectAtRoot()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var scene = prefabStage?.scene ?? SceneManager.GetActiveScene();
            
            var go = new GameObject("New GameObject");
            SceneManager.MoveGameObjectToScene(go, scene);

            go.transform.SetParent(null, worldPositionStays: true);
        }
        
        [MenuItem("Tools/NDM Framework/Debug Tools/Reload Domain")]
        public static void ReloadDomain()
        {
            Debug.Log("Reloading domain...");
            EditorUtility.RequestScriptReload();
        }
        #endif
    }
}