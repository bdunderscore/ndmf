#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Base platform bindings for generic unity animators.
    /// </summary>
    public sealed class GenericPlatformAnimatorBindings : IPlatformAnimatorBindings
    {
        public static readonly GenericPlatformAnimatorBindings Instance = new();

        private GenericPlatformAnimatorBindings()
        {
        }
        
        public bool IsSpecialMotion(Motion m)
        {
            return false;
        }

        public IEnumerable<(object, RuntimeAnimatorController, bool)> GetInnateControllers(GameObject root)
        {
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                var controller = animator.runtimeAnimatorController;

                if (controller != null)
                {
                    yield return (animator, controller, false);
                }
            }
        }

        public void CommitControllers(GameObject root, IDictionary<object, RuntimeAnimatorController> controllers)
        {
            foreach (var (key, controller) in controllers)
            {
                if (key is Animator a && a != null)
                {
                    a.runtimeAnimatorController = controller;
                }
            }
        }
    }
}