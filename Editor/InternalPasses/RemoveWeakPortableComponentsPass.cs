using System.Collections.Generic;
using nadena.dev.ndmf.multiplatform.components;
using UnityEngine;

namespace nadena.dev.ndmf.multiplatform.editor.Passes
{
    public class RemoveWeakPortableComponentsPass : Pass<RemoveWeakPortableComponentsPass>
    {
        protected override void Execute(BuildContext context)
        {
            Dictionary<Transform, PortableDynamicBone> boneMap = new();

            foreach (var pdb in context.AvatarRootTransform.GetComponentsInChildren<PortableDynamicBone>(true))
            {
                var root = pdb.Root;
                if (root.Value == null) root.Value = pdb.transform;

                if (boneMap.TryGetValue(root, out var prior))
                {
                    if (prior.IsWeak && !pdb.IsWeak)
                    {
                        UnityEngine.Object.DestroyImmediate(prior);
                        boneMap[root] = pdb;
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(pdb);
                    }
                }
                else
                {
                    boneMap[root] = pdb;
                }
            }
        }
    }
}