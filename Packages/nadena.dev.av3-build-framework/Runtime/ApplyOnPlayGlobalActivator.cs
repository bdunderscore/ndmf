#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.build_framework.runtime
{
    [InitializeOnLoad]
    class ApplyOnPlayGlobalActivator : MonoBehaviour
    {
        private const string TAG_OBJECT_NAME = "ModularAvatarInternal_Activator";

        static ApplyOnPlayGlobalActivator()
        {
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (scene.IsValid())
                {
                    EditorApplication.delayCall += () => CreateIfNotPresent(scene);
                }
            };
            
            EditorApplication.delayCall += () => CreateIfNotPresent(SceneManager.GetActiveScene());
        }
        
        internal enum OnDemandSource
        {
            Awake,
            Start
        }

        internal delegate void OnDemandProcessAvatarDelegate(OnDemandSource source, MonoBehaviour component);

        internal static OnDemandProcessAvatarDelegate OnDemandProcessAvatar = (_m, _c) => { };
        
        private void Awake()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;

            var scene = gameObject.scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>())
                {
                    // TODO: Check whether each avatar needs processing (activation components)
                    avatar.gameObject.GetOrAddComponent<AvatarActivator>().hideFlags = HideFlags.HideInInspector;
                }
            }
        }

        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;

                gameObject.hideFlags = HIDE_FLAGS;
            };
        }

        internal static void CreateIfNotPresent(Scene scene)
        {
            if (!scene.IsValid() || EditorSceneManager.IsPreviewScene(scene)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool rootPresent = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<ApplyOnPlayGlobalActivator>() != null)
                {
                    root.hideFlags = HIDE_FLAGS;
                    if (rootPresent) DestroyImmediate(root);
                    rootPresent = true;
                }
            }

            if (rootPresent) return;

            var oldActiveScene = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                var gameObject = new GameObject(TAG_OBJECT_NAME);
                gameObject.AddComponent<ApplyOnPlayGlobalActivator>();
                gameObject.hideFlags = HIDE_FLAGS;
            }
            finally
            {
                SceneManager.SetActiveScene(oldActiveScene);
            }
        }

        //private const HideFlags HIDE_FLAGS = HideFlags.HideInHierarchy;
        private const HideFlags HIDE_FLAGS = HideFlags.HideInHierarchy;
    }
    
    
    [AddComponentMenu("")]
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-9997)]
    public class AvatarActivator : MonoBehaviour
    {
        private void Awake()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            ApplyOnPlayGlobalActivator.OnDemandProcessAvatar(ApplyOnPlayGlobalActivator.OnDemandSource.Awake, this);
        }

        private void Start()
        {
            if (!RuntimeUtil.isPlaying || this == null) return;
            ApplyOnPlayGlobalActivator.OnDemandProcessAvatar(ApplyOnPlayGlobalActivator.OnDemandSource.Start, this);
        }

        private void Update()
        {
            DestroyImmediate(this);
        }
    }
}
#endif