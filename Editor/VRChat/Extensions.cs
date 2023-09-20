using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.ndmf
{
    public sealed partial class BuildContext
    {
        private VRCAvatarDescriptor _avatarDescriptor;
        
        /// <summary>
        /// The VRChat avatar descriptor for the avatar being built.
        /// </summary>
        public VRCAvatarDescriptor AvatarDescriptor => _avatarDescriptor;

        public BuildContext(VRCAvatarDescriptor obj, string assetRootPath)
            : this(obj.gameObject, assetRootPath)
        {
        }

        
        private void PlatformInit()
        {
            _avatarDescriptor = _avatarRootObject.GetComponent<VRCAvatarDescriptor>();
        }
    }
    
    internal static class PlatformExtensions
    {
        public static Transform FindAvatarInParents(Transform target)
        {
            while (target != null)
            {
                var av = target.GetComponent<VRCAvatarDescriptor>();
                if (av != null) return av.transform;
                target = target.parent;
            }

            return null;
        }
        
        public static bool CanProcessObject(GameObject avatar)
        {
            return (avatar != null && avatar.GetComponent<VRCAvatarDescriptor>() != null);
        }
    }
}