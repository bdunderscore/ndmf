using JetBrains.Annotations;
#if NDMF_VRCSDK3_AVATARS
using VRC.SDKBase;
#endif

namespace nadena.dev.ndmf
{
    /// <summary>
    ///     This interface derives from VRChat's IEditorOnly, if the VRChat SDK is present, and otherwise is a no-op
    ///     interface. This allows downstream plugins to mark their components as editor-only without needing to #if
    ///     the reference to be compatible with non-VRCSDk configurations.
    /// </summary>
    [PublicAPI]
    public interface INDMFEditorOnly
#if NDMF_VRCSDK3_AVATARS
        : IEditorOnly
#endif
    {
    }
}