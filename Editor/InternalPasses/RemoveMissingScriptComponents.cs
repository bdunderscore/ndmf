using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.builtin
{
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