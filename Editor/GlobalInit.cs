#region

using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.UI;
using nadena.dev.ndmf.runtime;
using UnityEditor;

#endregion

namespace nadena.dev.ndmf
{
    [InitializeOnLoad]
    internal static class GlobalInit
    {
        static GlobalInit()
        {
            RuntimeUtil.DelayCall = call => { EditorApplication.delayCall += () => call(); };

            EditorApplication.delayCall += () =>
            {
                var resolver = new PluginResolver();
                PreviewSession.Current = resolver.PreviewSession;

                PreviewPrefs.instance.OnPreviewConfigChanged += () =>
                {
                    var oldSession = PreviewSession.Current;
                    PreviewSession.Current = resolver.PreviewSession;
                    oldSession.Dispose();

                    SceneView.RepaintAll();
                };
            };
        }
    }
}