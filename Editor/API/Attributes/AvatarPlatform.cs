using UnityEngine;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Declares which platforms a plugin or pass will execute for. Plugins and passes which execute for "Generic" will
    /// always execute; otherwise, you can declare a specific platform or platforms to support.
    /// </summary>
    public enum AvatarPlatform
    {
        Generic,
        VRChat,
        UniVRM
    }

    /// <summary>
    /// The platform driver provides certain common services which depend on the specific avatar platform being used -
    /// e.g. heuristics for finding avatar roots.
    /// </summary>
    public abstract class PlatformDriver
    {
        public static AvatarPlatform CurrentPlatform => Current.Platform;
        public static PlatformDriver Current;
        
        public abstract AvatarPlatform Platform { get; }

        public abstract Transform FindAvatarRoot(Transform t);
    }
}