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
        public static string AvatarRootPath(this GameObject child)
        {
            return RuntimeUtil.AvatarRootPath(child);
        }

        [CanBeNull]
        public static string AvatarRootPath(this Component child)
        {
            return child.gameObject.AvatarRootPath();
        }
    }
}