using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using UnityEngine;

namespace nadena.dev.ndmf.builtin
{
    /// <summary>
    /// This pass removes all objects tagged with "EditorOnly" from the avatar. It will be run early in the Resolving
    /// phase; if you need to run before this, declare a BeforePass constraint on this type.
    /// </summary>
    [NDMFInternalEarlyPass]
    public sealed class RemoveEditorOnlyPass : Pass<RemoveEditorOnlyPass>
    {
        internal static RemoveEditorOnlyPass Instance = new RemoveEditorOnlyPass();
        public override string QualifiedName => "nadena.dev.ndmf.system.RemoveEditorOnly";
        public override string DisplayName => "Remove EditorOnly Objects";

        [ExcludeFromDocs]
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