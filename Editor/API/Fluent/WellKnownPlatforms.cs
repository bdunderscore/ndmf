using JetBrains.Annotations;

namespace nadena.dev.ndmf.model
{
    [PublicAPI]
    public static class WellKnownPlatforms
    {
        /// <summary>
        /// A NDMF built-in platform that assumes nothing beyond basic ability to render meshes.
        /// </summary>
        public const string Generic = "nadena.dev.ndmf.generic";
        public const string VRChatAvatar30 = "nadena.dev.ndmf.vrchat.avatar3";
        public const string Resonite = "nadena.dev.ndmf.resonite";
        
        // VRM?
        // Chillout?
        // Warudo?
    }
}