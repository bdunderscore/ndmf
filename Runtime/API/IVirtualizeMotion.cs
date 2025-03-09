#nullable enable

using JetBrains.Annotations;
using UnityEngine;

namespace API
{
    /// <summary>
    ///     Implement this interface to request that the referenced motion be converted to a virtual motion when the
    ///     @"VirtualControllerContext" is activated. Internally, VirtualControllerContext will generate a single-state
    ///     animator controller to contain the IVirtualizeMotion motion.
    ///     To access the VirtualMotion, use the @"VirtualControllerContext#GetVirtualizedMotion" method.
    ///     As with @"IVirtualizeAnimatorController", when the VirtualControllerContext is deactivated, the new motion will
    ///     be written back to its original component.
    /// </summary>
    [PublicAPI]
    public interface IVirtualizeMotion
    {
        /// <summary>
        ///     The motion to virtualize.
        /// </summary>
        public Motion Motion { get; set; }

        /// <summary>
        ///     Returns the base path prefix that should be applied to curves in the motion.
        ///     If clearPath is set to true, this function should return the current path, then clear it (set it to an empty
        ///     string).
        ///     When the @"VirtualControllerContext" is activated, this function will be invoked with clearPath set to true,
        ///     and the resulting path prefix will be used to prefix all curves in the motion.
        /// </summary>
        /// <param name="ndmfBuildContext">
        ///     The @"BuildContext" object for the current build (passed as object due to
        ///     runtime assembly limitations)
        /// </param>
        /// <param name="clearPath">True to reset the path to the empty string</param>
        /// <returns>The path prefix (if an empty string is returned, no curves will be modified)</returns>
        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true);
    }
}