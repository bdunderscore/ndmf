using nadena.dev.ndmf.cs;
using UnityEditor;

namespace nadena.dev.ndmf.preview
{
    internal class VisibilityMonitor
    {
        internal static long Sequence { get; private set; }
        
        internal static ListenerSet<bool> OnVisibilityChange { get; } = new();

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneVisibilityManager.visibilityChanged += () =>
            {
                OnVisibilityChange.Fire(true);
                
                Sequence++;
            };
            
            SceneVisibilityManager.pickingChanged += () =>
            {
                Sequence++;
            };
        }
    }
}