#region

using System;
using JetBrains.Annotations;
using nadena.dev.ndmf.runtime;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.util
{
    public static class MiscHelpers
    {
        [CanBeNull]
        [Obsolete("Please use RuntimeUtil.RelativePath() instead.")]
        public static string RelativePath(GameObject root, GameObject child)
        {
            return RuntimeUtil.RelativePath(root, child);
        }

        [CanBeNull]
        [Obsolete("Please use RuntimeUtil.AvatarRootPath() instead.")]
        public static string AvatarRootPath(this GameObject child)
        {
            if (child == null) return null;
            var avatar = PlatformExtensions.FindAvatarInParents(child.transform);
            if (avatar == null) return null;
            return RelativePath(avatar.gameObject, child);
        }

        [CanBeNull]
        [Obsolete("Please use RuntimeUtil.AvatarRootPath() instead.")]
        public static string AvatarRootPath(this Component child)
        {
            return child.gameObject.AvatarRootPath();
        }
    }
}