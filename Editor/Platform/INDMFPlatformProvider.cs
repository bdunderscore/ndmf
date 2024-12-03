#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    // [PublicAPI] - pre-alpha API
    internal interface INDMFPlatformProvider
    {
        string QualifiedName { get; }
        string DisplayName { get; }
        Texture2D? Icon => null; 

        // if unset, we use a generic Animator (or NDMFAvatarRoot) 
        Type? AvatarRootComponentType { get => null; } 

        BuildUIElement? CreateBuildUI() => null;

        bool HasNativeUI => false;

        void OpenNativeUI()
        {
        }

        CommonAvatarInfo ExtractCommonAvatarInfo(GameObject avatarRoot)
        {
            return new();
        }

        /// <summary>
       ///  When this platform is _not_ the selected platform, but is the "primary" platform for an avatar,
       /// create portable NDMF components to represent platform-specific dynamics (e.g. dynamic bones).
       ///
       /// This function may be invoked either at build time, or in response to user action. If the latter,
       /// registerUndo will be true, and any actions performed should be registered in the Unity undo system.
       /// </summary>
       /// <param name="context"></param>
        void GeneratePortableComponents(GameObject avatarRoot, bool registerUndo)
        {
            
        }

        /// Return true if we can initialize this platform's native config from this common config structure
        bool CanInitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            return false;
        }

        /// Initialize this platform's native config from this common config structure (destructive operation)
        void InitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            
        }

        /// <summary>
        /// This method is invoked early in the build process (between FirstChance and PlatformInit),
        /// and is provided with a CommonAvatarInfo structure with any configuration extracted from portable
        /// components, or components from the primary platform for the avatar.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="info"></param>
        void InitBuildFromCommonAvatarInfo(BuildContext context, CommonAvatarInfo info)
        {
            InitFromCommonAvatarInfo(context.AvatarRootObject, info);
        }
        
        [ExcludeFromDocs]
        void ___require_csharp_default_member_support()
        {
        }
    }
}