#nullable enable

using System;
using System.Collections.Generic;
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

        public CommonAvatarInfo ExtractCommonAvatarInfo(GameObject avatarRoot)
        {
            var cai = new CommonAvatarInfo();
            
            var viewpoint = avatarRoot.GetComponentsInChildren<NDMFViewpoint>();
            if (viewpoint.Length > 0)
            {
                if (viewpoint.Length > 1)
                {
                    // TODO - error reporting
                    throw new InvalidOperationException("Multiple NDMF Viewpoint components found");
                }
                
                cai.EyePosition = avatarRoot.transform.InverseTransformPoint(viewpoint[0].transform.position);
            }

            var visemes = avatarRoot.GetComponentsInChildren<PortableBlendshapeVisemes>();
            if (visemes.Length > 0)
            {
                if (visemes.Length > 1)
                {
                    // TODO - error reporting
                    throw new InvalidOperationException("Multiple Portable Blendshape Visemes components found");
                }

                var config = visemes[0];
                if (config.TargetRenderer != null)
                {
                    cai.VisemeRenderer = config.m_targetRenderer;
                    cai.VisemeBlendshapes.Clear();

                    foreach (var shape in (IEnumerable<PortableBlendshapeVisemes.Shape>)config.Shapes ?? Array.Empty<PortableBlendshapeVisemes.Shape>())
                    {
                        if (string.IsNullOrWhiteSpace(shape.VisemeName) ||
                            string.IsNullOrWhiteSpace(shape.Blendshape))
                        {
                            continue;
                        }

                        cai.VisemeBlendshapes[shape.VisemeName!] = shape.Blendshape!;
                    } 
                }
            }

            return cai;
        }
    }
}