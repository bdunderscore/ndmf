#if LEGACY_MODULAR_AVATAR
using UnityEditor;

namespace nadena.dev.ndmf
{
    [InitializeOnLoad]
    internal class LegacyModularAvatarWarning
    {
        static LegacyModularAvatarWarning()
        {
            if (SessionState.GetBool("nadena.dev.legacy-ma-warning", false)) return;
            SessionState.SetBool("nadena.dev.legacy-ma-warning", true);
            EditorApplication.delayCall += DisplayWarning;
        }

        private static void DisplayWarning()
        {
            var isJapanese = true;

            while (true)
            {
                string message, readInOtherLang;
                if (isJapanese)
                {
                    message = "1.7.x以前のModular Avatarがインストールされているようです。\n\n" +
                              "現在お使いのエディタ拡張では、1.7.x以前のModular Avatarと互換性がありません。\n\n" +
                              "Modular Avatarを1.8.0以降に更新してください。";
                    readInOtherLang = "Read in English";
                }
                else
                {
                    message = "Modular Avatar 1.7.x or older is installed.\n\n" +
                              "One or more editor extensions in your project is not compatible with Modular Avatar 1.7.x or older.\n\n" +
                              "Please upgrade to Modular Avatar 1.8.0 or later.";
                    readInOtherLang = "日本語で読む";
                }
            
                if (EditorUtility.DisplayDialog("Modular Avatar", message, "OK", readInOtherLang))
                    return;

                isJapanese = !isJapanese;
            }
        }
    }
}
#endif