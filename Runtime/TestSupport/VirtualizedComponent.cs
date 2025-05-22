#nullable enable

using nadena.dev.ndmf.animator;
using UnityEngine;

namespace nadena.dev.ndmf.UnitTestSupport
{
    internal class VirtualizedComponent : MonoBehaviour, IVirtualizeAnimatorController
    {
        public string MotionBasePath { get; set; } = "";
        public RuntimeAnimatorController? AnimatorController { get; set; }

        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true)
        {
            var result = MotionBasePath;
            if (clearPath)
            {
                MotionBasePath = "";
            }

            return result;
        }

        public object? TargetControllerKey { get; set; } = null;
    }
}