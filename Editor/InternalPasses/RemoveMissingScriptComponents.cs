using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.builtin
{
    /// <summary>
    /// This pass removes all missing script components from the avatar. It will be run early in the Resolving phase; if
    /// (for some reason) you need to run before this, declare a BeforePass constraint on this type.
    /// </summary>
    [NDMFInternal]
    public sealed class RemoveMissingScriptComponents : Pass<RemoveMissingScriptComponents>
    {
        protected override void Execute(BuildContext context)
        {
            foreach (var child in context.AvatarRootObject.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }
    }
}