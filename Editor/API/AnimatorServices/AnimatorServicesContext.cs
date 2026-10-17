#nullable enable

using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Provides a number of NDMF services based on virtualizing animator controllers.
    ///     While this context is active, NDMF will automatically track object renames, and apply them to all known
    ///     animators. It will also keep animators cached in the VirtualControllerContext (which is also, as a convenience,
    ///     available through this class).
    /// 
    ///     Note that any new objects created should be registered in the @"ObjectPathRemapper" if they'll be used in animations;
    ///     this ensures that subsequent movements will be tracked properly. Likewise, use @"ObjectPathRemapper" to obtain
    ///     (virtual) object paths for newly created objects while this context is active.
    /// </summary>
    [DependsOnContext(typeof(VirtualControllerContext))]
    [PublicAPI]
    public sealed class AnimatorServicesContext : IExtensionContext
    {
        private BuildContext? _context;

        public VirtualControllerContext ControllerContext =>
            (_context ?? throw new InvalidOperationException(
                "ControllerContext is not available outside of the AnimatorServicesContext"))
            .Extension<VirtualControllerContext>();

        private AnimationIndex? _animationIndex;

        public AnimationIndex AnimationIndex => _animationIndex ??= CreateAnimationIndex();

        private ObjectPathRemapper? _objectPathRemapper;

        public ObjectPathRemapper ObjectPathRemapper => _objectPathRemapper ??
                                                        throw new InvalidOperationException(
                                                            "ObjectPathRemapper is not available outside of the AnimatorServicesContext");

        public void OnActivate(BuildContext context)
        {
            _context = context;
            // Record paths before any pass can change the hierarchy, without initializing animator services.
            _objectPathRemapper = new ObjectPathRemapper(context.AvatarRootTransform);
        }

        private AnimationIndex CreateAnimationIndex()
        {
            return new AnimationIndex(
                () => ControllerContext.GetAllControllers(),
                () => ControllerContext.CacheInvalidationToken
            )
            {
                PlatformBindings = ControllerContext.PlatformBindings
            };
        }

        public void OnDeactivate(BuildContext context)
        {
            AnimationIndex.RewritePaths(ObjectPathRemapper.GetVirtualToRealPathMap());

            _objectPathRemapper = null;
            _animationIndex = null;
            _context = null;
        }
    }
}