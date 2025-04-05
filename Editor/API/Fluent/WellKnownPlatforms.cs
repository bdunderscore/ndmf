using JetBrains.Annotations;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Declares a number of well-known platform names that can be used to declare pass or plugin compatibility.
    ///
    /// Compatibility can be declared in three ways:
    ///
    /// 1. Using the <see cref="RunsOnPlatform"/> or <see cref="RunsOnAllPlatforms"/> attribute on a plugin.
    /// 2. Using <see cref="nadena.dev.ndmf.fluent.Sequence.OnPlatforms(string[],Action<Sequence>)"/>
    ///    or <see cref="nadena.dev.ndmf.fluent.Sequence.OnAllPlatforms(Action<Sequence>)"/> to override the above on
    ///    a group of passes.
    /// 3. Using the <see cref="RunsOnPlatform"/> or <see cref="RunsOnAllPlatforms"/> attributes on a pass.
    ///
    /// The most specific declaration will take precedence. That is, pass attributes always take precedence over Sequence
    /// overrides, and Sequence overrides take precedence over plugin attributes.
    /// </summary>
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