using nadena.dev.Av3BuildFramework;
using UnityEngine;

namespace nadena.dev.ndmf.InternalPasses
{
    [NDMFInternalEarlyPass]
    internal class RemoveEditorOnly : PluginPass
    {
        internal static RemoveEditorOnly Instance = new RemoveEditorOnly();
        
        public override void Process(BuildContext context)
        {
            foreach (Transform t in context.AvatarRootTransform.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject.CompareTag("EditorOnly"))
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }
        }
    }
}