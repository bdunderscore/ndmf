using UnityEngine;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Declares which platforms a plugin or pass will execute for. Plugins and passes which execute for "Generic" will
    /// always execute; otherwise, you can declare a specific platform or platforms to support.
    /// </summary>
    public sealed class AvatarPlatform
    {
        public static AvatarPlatform Generic = AvatarPlatform.Named("Generic");
        public static AvatarPlatform VRChat = AvatarPlatform.Named("VRChat");
        public static AvatarPlatform UniVRM = AvatarPlatform.Named("UniVRM");
        
        public string Name { get; }

        private AvatarPlatform(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Constructs an AvatarPlatform using a bare string for a name. This can be used to declare support for
        /// specific platforms without needing to take a compile-time dependency on that platform's driver package.
        ///
        /// AvatarPlatforms with the same name will compare equal.
        /// </summary>
        /// <param name="name">the name of the platform</param>
        /// <returns>an AvatarPlatform object wrapping that name</returns>
        public static AvatarPlatform Named(string name)
        {
            return new AvatarPlatform(name);
        }

        private bool Equals(AvatarPlatform other)
        {
            return Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is AvatarPlatform other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public static bool operator ==(AvatarPlatform left, AvatarPlatform right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AvatarPlatform left, AvatarPlatform right)
        {
            return !Equals(left, right);
        }
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