using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.preview
{
    internal class PreviewSceneSaveHook : AssetModificationProcessor
    {
        internal static string PreviewScenePath;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (PreviewScenePath == null || !paths.Any(p => p == PreviewScenePath)) return paths;

            return paths.Where(p => p != PreviewScenePath).ToArray();
        }
    }
    
    public static class NDMFPreviewSceneManager
    {
        internal static string PreviewSceneName = "___NDMF Preview___";
        private const string PreviewSceneGuid = "8cbd3f19cef3477439841053ced0661b";

        private static Scene _previewScene;

        private static bool _showPreviewScene;

        private static bool ShowPreviewScene
        {
            get => _showPreviewScene;
            set
            {
                _showPreviewScene = value;
                if (_previewScene.IsValid()) _previewScene.isSubScene = !_showPreviewScene;

                Menu.SetChecked("Tools/NDM Framework/Debug Tools/Show Preview Scene", _showPreviewScene);
            }
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            var clearSceneDirtiness = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                BindingFlags.NonPublic | BindingFlags.Static);

            EditorApplication.update += () =>
            {
                if (_previewScene.IsValid() && _previewScene.isDirty)
                    // We never want to save anything in the preview scene, and we definitely don't want to end up with
                    // UI popups prompting to save it, so aggressively clear its dirty flag.
                    clearSceneDirtiness?.Invoke(null, new object[] { _previewScene });

                if (_previewScene.IsValid() && SceneManager.GetActiveScene() == _previewScene)
                {
                    // Oops, make sure the preview scene isn't selected
                    var found = false;

                    var sceneCount = SceneManager.sceneCount;
                    for (var i = 0; i < sceneCount; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene == _previewScene || !scene.isLoaded || !scene.IsValid()) continue;

                        SceneManager.SetActiveScene(scene);
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        // Unload the preview scene if it's the only valid/loaded scene left
                        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);

                        ResetPreviewScene();
                    }
                }
            };

            // Reset preview scene on play mode transition
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                    ResetPreviewScene();
            };

            // Reset preview scene on assembly reload
            AssemblyReloadEvents.beforeAssemblyReload += () => { ResetPreviewScene(); };

            // Create scene on scene transition. Unity doesn't allow us to create a new scene additively when an unsaved
            // scene is open, so we do this now to head off user interaction.
            EditorSceneManager.activeSceneChangedInEditMode += (prev, next) => { GetPreviewScene(); };
            EditorApplication.delayCall += () => { GetPreviewScene(); };

            // Make sure that we never, ever save any gameobjects in the preview scene. If we start saving, destroy them
            // all.
            EditorSceneManager.sceneSaving += (scene, path) =>
            {
                if (scene == _previewScene)
                    foreach (var go in _previewScene.GetRootGameObjects())
                        Object.DestroyImmediate(go);
            };

            _showPreviewScene = false;
            Menu.SetChecked("Tools/NDM Framework/Debug Tools/Show Preview Scene", false);
        }

        [MenuItem("Tools/NDM Framework/Debug Tools/Show Preview Scene")]
        internal static void ShowPreviewSceneMenuItem()
        {
            GetPreviewScene();
            ShowPreviewScene = !_showPreviewScene;

            Menu.SetChecked("Tools/NDM Framework/Debug Tools/Show Preview Scene", _showPreviewScene);
        }

        /// <summary>
        ///     Returns a scene that can be used for temporary GameObjects used in preview filters. This scene will not be
        ///     displayed in the hierarchy; however, they can be interacted with in the scene view normally.
        ///     Objects in this scene (or the scene itself!) may be destroyed at unexpected times; your code must take care
        ///     to handle this case.
        /// </summary>
        /// <returns></returns>
        public static Scene GetPreviewScene()
        {
            if (_previewScene.IsValid()) return _previewScene;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return default;

            _previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            if (!_previewScene.IsValid())
            {
                // Load scene from asset
                var assetPath = AssetDatabase.GUIDToAssetPath(PreviewSceneGuid);
                PreviewSceneSaveHook.PreviewScenePath = assetPath;
                
                _previewScene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);
                PreviewSceneName = _previewScene.name;

                // Make sure it's empty, in case the scene file got overwritten somehow
                foreach (var go in _previewScene.GetRootGameObjects()) Object.DestroyImmediate(go);
            }

            _previewScene.isSubScene = !_showPreviewScene;

            return _previewScene;
        }

        /// <summary>
        ///     Returns true if the given scene is the NDMF preview scene.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsPreviewScene(Scene s)
        {
            return s == _previewScene;
        }
        
        private static void ResetPreviewScene()
        {
            if (_previewScene.IsValid()) EditorSceneManager.CloseScene(_previewScene, true);
            _previewScene = default;
        }
    }
}