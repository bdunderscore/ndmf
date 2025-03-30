#nullable enable

using nadena.dev.ndmf.animator;
using UnityEngine;

namespace nadena.dev.ndmf.UnitTestSupport
{
    internal class VirtualizedComponent : MonoBehaviour, IVirtualizeAnimatorController
    {
        public RuntimeAnimatorController AnimatorController { get; set; }

        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true)
        {
            return "";
        }

        public object? TargetControllerKey { get; set; } = null;
    }
}