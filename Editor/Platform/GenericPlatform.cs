#nullable enable

using System;
using nadena.dev.ndmf.model;
using nadena.dev.ndmf.runtime.components;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    [NDMFPlatformProvider]
    internal sealed class GenericPlatform : INDMFPlatformProvider
    {
        public static INDMFPlatformProvider Instance { get; } = new GenericPlatform();

        private GenericPlatform()
        {
        }
        
        public string QualifiedName => WellKnownPlatforms.Generic;
        public string DisplayName => "Generic Avatar";
        public Texture2D? Icon => null;
        public Type? AvatarRootComponentType => typeof(NDMFAvatarRoot);

        public bool HasNativeUI => false;

        public void OpenNativeUI()
        {
            throw new NotImplementedException();
        }
    }
}