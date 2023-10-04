#region

using nadena.dev.ndmf.config;
using UnityEditor;
using UnityObject = UnityEngine.Object;

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

        // Avoid cluttering the GameObject context menu with duplicate entries. Users are more familiar with MA anyway,
        // so we'll keep that entry.
#if !MODULAR_AVATAR
        [MenuItem("GameObject/[NDMF] Manual bake avatar", true, 49)]
        private static bool ValidateManualBakeGameObject()
        {
            return AvatarProcessor.CanProcessObject(Selection.activeGameObject);
        }

        [MenuItem("GameObject/[NDMF] Manual bake avatar", false, 49)]
        private static void ManualBakeGameObject()
        {
            AvatarProcessor.ProcessAvatarUI(Selection.activeGameObject);
        }
#endif

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
    }
}