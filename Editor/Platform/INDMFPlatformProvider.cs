#nullable enable

using System;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    // [PublicAPI] - pre-alpha API
    public interface INDMFPlatformProvider
    {
        string CanonicalName { get; }
        string DisplayName { get; }
        Texture2D? Icon { get; }

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

        /// Return true if we can initialize this platform's native config from this common config structure
        bool CanInitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            return false;
        }

        /// Initialize this platform's native config from this common config structure (destructive operation)
        void InitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            
        }

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