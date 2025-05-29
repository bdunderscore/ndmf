#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.runtime.components;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    /// <summary>
    /// A minimal "generic" platform. This is only really useful for manual bake avatar.
    /// </summary>
    [NDMFPlatformProvider]
    public sealed class GenericPlatform : INDMFPlatformProvider
    {
        public static INDMFPlatformProvider Instance { get; } = new GenericPlatform();

        private GenericPlatform()
        {
        }
        
        public string QualifiedName => WellKnownPlatforms.Generic;
        public string DisplayName => "Generic Avatar";
        public Texture2D? Icon => null;
        public Type? AvatarRootComponentType => typeof(NDMFAvatarRoot);

        public bool HasNativeConfigData => true;
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

        public void InitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            if (info.EyePosition.HasValue)
            {
                InitEyePosition(avatarRoot, info.EyePosition.Value);
            }

            if (info.VisemeRenderer != null && info.VisemeBlendshapes.Count > 0)
            {
                InitVisemes(avatarRoot, info);
            }
        }

        private void InitVisemes(GameObject avatarRoot, CommonAvatarInfo info)
        {
            var visemesComponents = avatarRoot.GetComponentsInChildren<PortableBlendshapeVisemes>();
            if (visemesComponents.Length > 1)
            {
                throw new Exception("Multiple Portable Blendshape Visemes components found");
            }

            PortableBlendshapeVisemes visemes;
            if (visemesComponents.Length == 0)
            {
                var container = new GameObject("Visemes");
                container.transform.SetParent(avatarRoot.transform, false);
                visemes = container.AddComponent<PortableBlendshapeVisemes>();
            }
            else
            {
                visemes = visemesComponents[0];
            }
            
            visemes.TargetRenderer = info.VisemeRenderer;
            visemes.Shapes.RemoveAll(s => s.Blendshape == null || !info.VisemeBlendshapes.ContainsKey(s.Blendshape));

            var toAdd = new Dictionary<string, string>(info.VisemeBlendshapes);
            for (int i = 0; i < visemes.Shapes.Count; i++)
            {
                var shape = visemes.Shapes[i];
                shape.Blendshape = toAdd[shape.VisemeName!];
                toAdd.Remove(shape.VisemeName!);
            }

            foreach (var missingShape in CommonAvatarInfo.KnownVisemes.Concat(toAdd.Keys).Distinct().ToList())
            {
                if (!toAdd.ContainsKey(missingShape)) continue;
                
                visemes.Shapes.Add(new()
                {
                    VisemeName = missingShape,
                    Blendshape = toAdd[missingShape]
                });
            }
        }

        private void InitEyePosition(GameObject avatarRoot, Vector3 eyePosition)
        {
            var viewpoints = avatarRoot.GetComponentsInChildren<NDMFViewpoint>();
            if (viewpoints.Length > 1)
            {
                throw new Exception("Multiple NDMF Viewpoint components found");
            }

            NDMFViewpoint viewpoint;
            if (viewpoints.Length == 0)
            {
                var container = new GameObject("Viewpoint");
                container.transform.SetParent(avatarRoot.transform, false);
                viewpoint = container.AddComponent<NDMFViewpoint>();
            }
            else
            {
                viewpoint = viewpoints[0];
            }

            viewpoint.transform.position = avatarRoot.transform.TransformPoint(eyePosition);
        }

        public void InitBuildFromCommonAvatarInfo(BuildContext context, CommonAvatarInfo info)
        {
            // no-op
        }
    }
}