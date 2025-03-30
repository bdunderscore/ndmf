#nullable enable

using System.Collections.Immutable;
using JetBrains.Annotations;
using nadena.dev.ndmf.preview.UI;
using nadena.dev.ndmf.ui;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     General utilities for controlling the NDMF preview system.
    /// </summary>
    [PublicAPI]
    // ReSharper disable once InconsistentNaming
    public static class NDMFPreview
    {
        private static PreviewSession? _globalPreviewSession;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += () =>
            {
                Menu.SetChecked(Menus.ENABLE_PREVIEW_MENU_NAME, EnablePreviewsUI);

                var resolver = new PluginResolver(includeDisabled: true);
                _globalPreviewSession = resolver.PreviewSession;
                PreviewSession.Current = resolver.PreviewSession;

                PreviewPrefs.instance.OnPreviewConfigChanged += () =>
                {
                    var oldSession = PreviewSession.Current;
                    PreviewSession.Current = resolver.PreviewSession;
                    oldSession?.Dispose();
                };

                SetPreviewState();
            };

            EditorApplication.playModeStateChanged += state =>
            {
                switch (state)
                {
                    // To avoid visual artifacts when transitioning we only reset when leaving play mode (since preview
                    // is forced off in play mode we don't care about the depth when we're in play mode).
                    case PlayModeStateChange.ExitingPlayMode:
                        DisablePreviewDepth = 0;
                        break;
                }
            };
        }

        private static int _disablePreviewDepth;

        /// <summary>
        ///     When this counter is non-zero, all NDMF preview systems will be disabled. This is intended for transiently
        ///     disabling previews and will not be preserved across domain reloads or play mode transitions.
        /// </summary>
        [PublicAPI]
        public static int DisablePreviewDepth
        {
            get => _disablePreviewDepth;
            set
            {
                _disablePreviewDepth = value;
                SetPreviewState();
            }
        }

        private static void SetPreviewState()
        {
            PreviewSession.Current = !EnablePreviewsUI || _disablePreviewDepth != 0 ? null : _globalPreviewSession;
            SceneView.RepaintAll();
        }

        internal static bool EnablePreviewsUI
        {
            get => NDMFPreviewPrefs.instance.EnablePreview;
            set
            {
                NDMFPreviewPrefs.instance.EnablePreview = value;
                EditorUtility.SetDirty(NDMFPreviewPrefs.instance);
                Menu.SetChecked(Menus.ENABLE_PREVIEW_MENU_NAME, value);
                SetPreviewState();
            }
        }

        [MenuItem(Menus.ENABLE_PREVIEW_MENU_NAME, false, Menus.ENABLE_PREVIEW_PRIO)]
        private static void ToggleEnablePreviews()
        {
            EnablePreviewsUI = !EnablePreviewsUI;
        }

        [MenuItem("Tools/NDM Framework/Debug Tools/Force Reset Preview", false, 101)]
        internal static void ForceResetPreview()
        {
            _globalPreviewSession?.ForceRebuild();
        }

        /// <summary>
        /// If the given game object is a preview proxy object, returns the original object that the proxy was created
        /// for.
        /// </summary>
        /// <param name="proxy">The suspected proxy object</param>
        /// <returns>The original object, or null if the given object is not a proxy</returns>
        public static GameObject? GetOriginalObjectForProxy(GameObject proxy)
        {
            var sess = PreviewSession.Current;

            if (sess == null)
            {
                return null;
            }

            return sess.ProxyToOriginalObject.GetValueOrDefault(proxy);
        }
    }
}