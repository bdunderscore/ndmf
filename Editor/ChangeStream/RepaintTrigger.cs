using UnityEditor;

namespace nadena.dev.ndmf.cs
{
    internal static class RepaintTrigger
    {
        private static bool _requested;

        public static void RequestRepaint()
        {
            if (_requested) return;

            // Note: We need to delay two frames to avoid visual flicker
            _requested = true;
            EditorApplication.delayCall += () =>
            {
                SceneView.RepaintAll();
                EditorApplication.delayCall += () =>
                {
                    _requested = false;
                    SceneView.RepaintAll();
                };
            };
        }
    }
}