using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using UnityEngine;

namespace nadena.dev.ndmf.builtin
{
    [NDMFInternalEarlyPass]
    public class RemoveEditorOnlyPass : Pass<RemoveEditorOnlyPass>
    {
        internal static RemoveEditorOnlyPass Instance = new RemoveEditorOnlyPass();
        public override string QualifiedName => "nadena.dev.ndmf.system.RemoveEditorOnly";
        public override string DisplayName => "Remove EditorOnly Objects";

        protected override void Execute(BuildContext context)
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