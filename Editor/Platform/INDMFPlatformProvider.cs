#nullable enable

using System;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    [PublicAPI]
    public interface INDMFPlatformProvider
    {
        /// <summary>
        ///  The internal fully qualified name of this platform. This is used when declaring platform compatibility.
        ///  See `WellKnownPlatforms` for a list of well-known platform qualified names.
        /// </summary>
        string QualifiedName { get; }
        /// <summary>
        /// The display name of this platform. This is used in the UI to identify the platform.
        /// </summary>
        string DisplayName { get; }
        /// <summary>
        /// An optional icon to display in the UI for this platform.
        /// </summary>
        Texture2D? Icon => null;

        /// <summary>
        ///  If true, this platform has some kind of avatar-wide native configuration components.
        ///  Currently, this controls whether NDMFAvatarRoot's inspector offers to convert configuration to/from this
        ///  platform.
        /// </summary>
        bool HasNativeConfigData => false;

        /// <summary>
        /// The component type which marks the root of the avatar. If unset, we will use NDMFAvatarRoot instead.
        ///
        /// This is used to identify the "primary" platform for the avatar, in addition to identifying the avatar root
        /// itself.
        /// </summary>
        Type? AvatarRootComponentType { get => null; } 

        /// <summary>
        /// Creates a UI Elements element to use as the build control UI to be shown in the NDMF console when this
        /// platform is selected.
        /// </summary>
        /// <returns></returns>
        BuildUIElement? CreateBuildUI() => null;

        /// <summary>
        /// Indicates there is some kind of native UI window (eg the VRCSDK build window) that can be opened.
        /// </summary>
        bool HasNativeUI => false;

        /// <summary>
        /// Opens the platform native UI window.
        /// </summary>
        void OpenNativeUI()
        {
        }

        /// <summary>
        /// Extracts information from platform-specific components on the avatar, and presents it as a CommonAvatarInfo
        /// object. The platform may choose only to supply a subset of the information in the CAI structure.
        /// </summary>
        /// <param name="avatarRoot"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Return true if we can initialize this platform's native config from the provided common config structure.
        /// </summary>
        /// <param name="avatarRoot"></param>
        /// <param name="info"></param>
        /// <returns></returns>
        bool CanInitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            return false;
        }

        /// <summary>
        /// Destructively initialize or overwrite this platform's native config from this common config structure.
        /// The caller will take care of undo and prefab override management, if necessary.
        /// </summary>
        /// <param name="avatarRoot"></param>
        /// <param name="info"></param>
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