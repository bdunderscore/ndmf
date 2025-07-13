#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Exposes information about what aspects of an avatar a component needs at runtime, so that optimizers can avoid
    /// removing necessary aspects. Optimizers should call this interface on all components that implement it, and ensure
    /// that the required aspects are not removed or altered as part of optimization.
    ///
    /// If an unknown aspect is encountered, the optimizer should not modify the targeted object(s) at all.
    /// </summary>
    public interface IRuntimeReflection
    {
        IEnumerable<IAspect> GetRequiredAspects();

        enum AspectScope
        {
            /// <summary>
            /// This requirement applies to the single unity object specified.
            /// </summary>
            SingleObject,
            /// <summary>
            /// This requirement applies to an entire game object, and all of its components (but not children).
            /// </summary>
            SingleGameObject,
            /// <summary>
            /// This requirement applies to an entire game object, and all of its components and children.
            /// </summary>
            Hierarchy,
        }

        interface IAspect
        {
            UnityEngine.Object Target { get; }
            UnityEngine.GameObject? TargetGameObject { get; }
            AspectScope Scope { get; }
        }

        abstract class AbstractAspect : IAspect
        {
            public Object Target { get; protected set; }
            public virtual GameObject? TargetGameObject => (Target as GameObject) ?? (Target as Component)?.gameObject; 
            public virtual AspectScope Scope => AspectScope.SingleObject;
        }

        /// <summary>
        /// Requires that the specified object(s) exist, but their contents can be modified.
        /// When applied to a hierarchy, this means that intermediate objects in the hierarchy should not be removed
        /// for optimization purposes.
        /// </summary>
        class Presence : AbstractAspect
        {
        }

        /// <summary>
        /// Requires that the specified Transform component exists and that its global transformation matrix is
        /// unchanged from the pre-optimization state. However, the local position, rotation, and scale can
        /// change if optimization reparents the transform.
        /// </summary>
        class TransformPose : AbstractAspect
        {
        }

        /// <summary>
        /// Requires that the specified blendshapes be retained on a SkinnedMeshRenderer.
        /// </summary>
        class Blendshapes : AbstractAspect
        {
            public IEnumerable<string> BlendshapeNames { get; }
        }
    }
}