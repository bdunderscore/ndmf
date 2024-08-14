#region

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using nadena.dev.ndmf.config;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.ui
{
    internal static class Menus
    {
        private const string APPLY_ON_PLAY_MENU_NAME = "Tools/NDM Framework/Apply on Play";
        private const string TOPLEVEL_MANUAL_BAKE_MENU_NAME = "Tools/NDM Framework/Manual bake avatar";
        private const int APPLY_ON_PLAY_PRIO = 1;
        private const int TOPLEVEL_MANUAL_BAKE_PRIO = 2;

        [InitializeOnLoadMethod]
        static void Init()
        {
            EditorApplication.delayCall += OnSettingsChanged;
            Config.OnChange += OnSettingsChanged;
        }

        [MenuItem("GameObject/NDM Framework/Manual bake avatar", true, 49)]
        private static bool ValidateManualBakeGameObject()
        {
            return AvatarProcessor.CanProcessObject(Selection.activeGameObject);
        }

        [MenuItem("GameObject/NDM Framework/Manual bake avatar", false, 49)]
        private static void ManualBakeGameObject()
        {
            AvatarProcessor.ProcessAvatarUI(Selection.activeGameObject);
        }

        [MenuItem(TOPLEVEL_MANUAL_BAKE_MENU_NAME, true, TOPLEVEL_MANUAL_BAKE_PRIO)]
        private static bool ValidateManualBakeToplevel()
        {
            return AvatarProcessor.CanProcessObject(Selection.activeGameObject);
        }

        [MenuItem(TOPLEVEL_MANUAL_BAKE_MENU_NAME, false, TOPLEVEL_MANUAL_BAKE_PRIO)]
        private static void ManualBakeToplevel()
        {
            AvatarProcessor.ProcessAvatarUI(Selection.activeGameObject);
        }

        [MenuItem(APPLY_ON_PLAY_MENU_NAME, false, APPLY_ON_PLAY_PRIO)]
        private static void ApplyOnPlay()
        {
            Config.ApplyOnPlay = !Config.ApplyOnPlay;
        }

        private static void OnSettingsChanged()
        {
            Menu.SetChecked(APPLY_ON_PLAY_MENU_NAME, Config.ApplyOnPlay);
        }

#if NDMF_DEBUG
        [MenuItem("Tools/NDM Framework/Debug Tools/Domain Reload", false, 101)]
        private static void DomainReload()
        {
            EditorUtility.RequestScriptReload();
        }

        [MenuItem("Tools/NDM Framework/Debug Tools/Invalidate shadow hierarchy", false, 101)]
        private static void InvalidateShadowHierarchy()
        {
            ObjectWatcher.Instance.Hierarchy.InvalidateAll();
        }
#endif
        
#if UNITY_2022_1_OR_NEWER
        [MenuItem("Tools/NDM Framework/Debug Tools/Profile build", false, 101)]
        private static void ProfileBuild()
        {
            var av = Selection.activeGameObject;
            IEnumerator coro = ProfileBuildCoro(av);

            EditorApplication.CallbackFunction updateCall = null;
            updateCall = () =>
            {
                if (coro == null)
                {
                    EditorApplication.update -= updateCall;
                }
                else
                {
                    coro.MoveNext();
                }
            };
            
            EditorApplication.update += updateCall;
        }

        [MenuItem("Tools/NDM Framework/Debug Tools/Profile build", true, 101)]
        private static bool ProfileBuild_Validate()
        {
            return Selection.activeGameObject != null 
                   && RuntimeUtil.IsAvatarRoot(Selection.activeGameObject.transform);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static IEnumerator ProfileBuildCoro(GameObject av)
        {
            Type ty_PW = typeof(ProfilerWindow);

            MethodInfo m_OnTargetedEditorConnectionChanged =
                ty_PW.GetMethod("OnTargetedEditorConnectionChanged", BindingFlags.Instance | BindingFlags.NonPublic);

            MethodInfo m_SetRecordingEnabled =
                ty_PW.GetMethod("SetRecordingEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
            
            Type ty_EditorConnectionTarget = m_OnTargetedEditorConnectionChanged.GetParameters()[0].ParameterType;
            var MainEditorProcessEditmode = Enum.Parse(ty_EditorConnectionTarget, "MainEditorProcessEditmode");
            var profWindow = EditorWindow.GetWindow<ProfilerWindow>();

            m_OnTargetedEditorConnectionChanged.Invoke(profWindow, new object[] { MainEditorProcessEditmode });
            m_SetRecordingEnabled.Invoke(profWindow, new object[] { true });
            
            yield return null; // wait one frame

            if (av == null) yield break;

            var clone = Object.Instantiate(av);

            try
            {
                AvatarProcessor.ProcessAvatar(clone);

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            //yield return null;
            m_SetRecordingEnabled.Invoke(profWindow, new object[] { false });
            yield return null;
            
            Object.DestroyImmediate(clone);
            AvatarProcessor.CleanTemporaryAssets();
        }
#endif
    }
}