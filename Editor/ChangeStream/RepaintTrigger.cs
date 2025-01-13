using nadena.dev.ndmf.preview;
using UnityEditor;

namespace nadena.dev.ndmf.cs
{
    internal static class RepaintTrigger
    {
        private static object _lock = new();
        private static bool _requested;

        public static void RequestRepaint()
        {
            lock (_lock)
            {
                if (_requested) return;

                // Note: We need to delay two frames to avoid visual flicker
                _requested = true;
                NDMFSyncContext.Context.Post(_ =>
                {
                    SceneView.RepaintAll();
                    EditorApplication.delayCall += () =>
                    {
                        _requested = false;
                        SceneView.RepaintAll();
                    };
                }, null);
            }
        }
    }
}