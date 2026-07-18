#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests
{
    public class PreviewExclusionTests
    {
        private sealed class CanEnableRendererFilter : IRenderFilter
        {
            private readonly Renderer _renderer;

            internal CanEnableRendererFilter(Renderer renderer)
            {
                _renderer = renderer;
            }

            public bool CanEnableRenderers => true;

            public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
            {
                return ImmutableList.Create(RenderGroup.For(_renderer));
            }

            public Task<IRenderFilterNode> Instantiate(
                RenderGroup group,
                IEnumerable<(Renderer, Renderer)> proxyPairs,
                ComputeContext context
            )
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public void NDMFPreviewExclusionIsNotInheritedBySpecializedSessions()
        {
            var defaultSession = new PreviewSession
            {
                ExcludeRenderer = _ => true
            };
            var specializedSession = defaultSession.Fork("Specialized preview");

            try
            {
                Assert.That(defaultSession.ExcludeRenderer, Is.Not.Null);
                Assert.That(specializedSession.ExcludeRenderer, Is.Null);
            }
            finally
            {
                specializedSession.Dispose();
                defaultSession.Dispose();
            }
        }

        [Test]
        public void NDMFPreviewExclusionAppliesOnlyToRegisteredAvatarSubtree()
        {
            var avatar = new GameObject("avatar");
            var child = new GameObject("child");
            var unrelated = new GameObject("unrelated");
            child.transform.SetParent(avatar.transform);

            try
            {
                var exclusion = NDMFPreview.ExcludeAvatarFromDefaultPreview(avatar);
                try
                {
                    Assert.That(NDMFPreview.IsExcludedFromDefaultPreview(avatar), Is.True);
                    Assert.That(NDMFPreview.IsExcludedFromDefaultPreview(child), Is.True);
                    Assert.That(NDMFPreview.IsExcludedFromDefaultPreview(unrelated), Is.False);
                }
                finally
                {
                    exclusion.Dispose();
                }

                Assert.That(NDMFPreview.IsExcludedFromDefaultPreview(child), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(unrelated);
            }
        }

        [Test]
        public void NDMFPreviewExclusionIsSpecificToEachTargetSet()
        {
            var avatar = new GameObject("avatar");
            var child = new GameObject("child");
            child.transform.SetParent(avatar.transform);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            var filters = ImmutableList.Create<IRenderFilter>(new CanEnableRendererFilter(renderer));

            try
            {
                var defaultTargets = new TargetSet(
                    filters,
                    ImmutableHashSet<Renderer>.Empty,
                    candidate => candidate.transform.IsChildOf(avatar.transform)
                );
                var specializedTargets = new TargetSet(
                    filters,
                    ImmutableHashSet<Renderer>.Empty,
                    null
                );

                Assert.That(
                    defaultTargets.ResolveActiveStages(new ComputeContext("default preview")),
                    Is.Empty
                );
                Assert.That(
                    specializedTargets.ResolveActiveStages(new ComputeContext("specialized preview")),
                    Has.Count.EqualTo(1)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
            }
        }
    }
}
