#nullable enable

using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Implement this interface on your component to request that an animator attached to this component be
    ///     automatically registered in the VirtualControllerContext, with the component being the context key.
    ///     This is, for example, implemented by ModularAvatarMergeAnimator to ensure that object renames are tracked
    ///     appropriately at all times.
    ///
    ///     When VirtualControllerContext is deactivated, the animator will be committed back to the
    ///     IVirtualizeAnimatorController component (if the component has not been destroyed)
    /// </summary>
    [PublicAPI]
    public interface IVirtualizeAnimatorController
    {
        public RuntimeAnimatorController AnimatorController { get; set; }

        /// <summary>
        ///     Returns the base path prefix that should be applied to curves in the animator controller.
        ///     If clearPath is set to true, this function should return the current path, then clear it (set it to an empty
        ///     string).
        ///     When the @"VirtualControllerContext" is activated, this function will be invoked with clearPath set to true,
        ///     and the resulting path prefix will be used to prefix all curves in the animator controller.
        /// </summary>
        /// <param name="ndmfBuildContext">
        ///     The @"BuildContext" object for the current build (passed as object due to
        ///     runtime assembly limitations)
        /// </param>
        /// <param name="clearPath">True to reset the path to the empty string</param>
        /// <returns>The path prefix (if an empty string is returned, no curves will be modified)</returns>
        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true);

        /// <summary>
        ///     Returns the key of the controller this animator controller is intended to be merged into (if known)
        ///     Typically, this would be a @"VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType" object, which
        ///     allows the import logic to determine whether VRChat Playable Layer Control layer indices need to be adjusted.
        /// </summary>
        public object? TargetControllerKey { get; }
    }
}