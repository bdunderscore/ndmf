#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.ndmf.runtime
{
    /// <summary>
    /// This component is used to trigger MA processing upon entering play mode (prior to Av3Emu running).
    /// We create it on a hidden object via AvatarTagObject's OnValidate, and it will proceed to add MAAvatarActivator
    /// components to all avatar roots which contain MA components. This MAAvatarActivator component then performs MA
    /// processing on Awake.
    ///
    /// Note that we do not directly process the avatars from MAActivator. This is to avoid processing avatars that are
    /// initially inactive in the scene (which can have high overhead if the user has a lot of inactive avatars in the
    /// scene).
    /// </summary>
    [InitializeOnLoad]
    class ApplyOnPlayGlobalActivator : MonoBehaviour
    {
        private const string TAG_OBJECT_NAME = "nadena.dev.ndmf__Activator";

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
            if (!RuntimeUtil.IsPlaying || this == null) return;

            var scene = gameObject.scene;

            // Check if Lyuma's Av3Emulator is present and enabled; if so, we leave preprocessing up to it.
            // First, find the type...

            Type ty_av3emu = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ty_av3emu = assembly.GetType("Lyuma.Av3Emulator.Runtime.LyumaAv3Emulator", false);
                if (ty_av3emu != null) break;
            }

            if (ty_av3emu != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren(ty_av3emu) != null)
                    {
                        return;
                    }
                }
            }

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
            if (!RuntimeUtil.IsPlaying || this == null) return;
            ApplyOnPlayGlobalActivator.OnDemandProcessAvatar(ApplyOnPlayGlobalActivator.OnDemandSource.Awake, this);
        }

        private void Start()
        {
            if (!RuntimeUtil.IsPlaying || this == null) return;
            ApplyOnPlayGlobalActivator.OnDemandProcessAvatar(ApplyOnPlayGlobalActivator.OnDemandSource.Start, this);
        }

        private void Update()
        {
            DestroyImmediate(this);
        }
    }
}
#endif