#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// A group of renderers that will be processed together.
    /// </summary>
    [PublicAPI]
    public class RenderGroup
    {
        public ImmutableList<Renderer> Renderers { get; }

        internal RenderGroup(ImmutableList<Renderer> renderers)
        {
            Renderers = renderers;
        }

        internal RenderGroup WithoutData()
        {
            return new RenderGroup(Renderers);
        }

        public static RenderGroup For(IEnumerable<Renderer> renderers)
        {
            return new(renderers.OrderBy(r => r.GetInstanceID()).ToImmutableList());
        }

        public static RenderGroup For(Renderer renderer)
        {
            return new(ImmutableList.Create(renderer));
        }

        public RenderGroup WithData<T>(T data)
        {
            return new RenderGroup<T>(Renderers, data);
        }

        public T GetData<T>()
        {
            return ((RenderGroup<T>)this).Context;
        }

        protected bool Equals(RenderGroup other)
        {
            return Equals(Renderers, other.Renderers);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((RenderGroup)obj);
        }

        public override int GetHashCode()
        {
            return (Renderers != null ? Renderers.GetHashCode() : 0);
        }
    }

    internal sealed class RenderGroup<T> : RenderGroup
    {
        public T Context { get; }

        internal RenderGroup(ImmutableList<Renderer> renderers, T context) : base(renderers)
        {
            Context = context;
        }

        private bool Equals(RenderGroup<T> other)
        {
            return base.Equals(other) && EqualityComparer<T>.Default.Equals(Context, other.Context);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is RenderGroup<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ EqualityComparer<T>.Default.GetHashCode(Context);
            }
        }
    }

    [PublicAPI]
    public interface IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context);
        
        /// <summary>
        /// Instantiates a node in the preview graph. This operation is used when creating a new proxy renderer, and may
        /// perform relatively heavyweight operations to prepare the Mesh, Materials, and Textures for the renderer. It
        /// may not modify other aspects of the renderer; however, these can be done in the OnFrame callback in the
        /// returned IRenderFilterNode.
        /// 
        /// When making changes to meshes, textures, and materials, this node must create new instances of these objects,
        /// and destroy them in `IRenderFilterNode.Dispose`.
        /// </summary>
        /// <param name="group"></param>
        /// <param name="proxyPairs">An enumerable of (original, proxy) renderer pairs</param>
        /// <param name="context">A compute context that is used to track which values your code depended on in
        ///     configuring this node. Changing these values will triger a recomputation of this node.</param>
        /// <returns></returns>
        public Task<IRenderFilterNode> Instantiate(
            RenderGroup group,
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context
        );

        /// <summary>
        ///     Evaluate whether the filter as a whole should be enabled. Returns true by default.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public bool IsEnabled(ComputeContext context)
        {
            return true;
        }

        /// <summary>
        ///     Returns a set of control nodes that can be used to modify the behavior of this filter. By default, returns an
        ///     empty set.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TogglablePreviewNode> GetPreviewControlNodes()
        {
            yield break;
        }

        // Allow for future expansion
        [ExcludeFromDocs]
        [UsedImplicitly]
        void __please_enable_dotnet_80_or_higher_for_default_methods()
        {
        }
    }

    [Flags]
    public enum RenderAspects
    {
        /// <summary>
        /// The sharedMesh property of a renderer or mesh filter
        /// </summary>
        Mesh = 1,
        /// <summary>
        /// The sharedMaterials property of a renderer
        /// </summary>
        Material = 2,
        /// <summary>
        /// A texture referenced by a material, or the contents of a render texture
        /// </summary>
        Texture = 4,
        /// <summary>
        /// Blendshapes or bones of a skinned mesh renderer
        /// </summary>
        Shapes = 8,


        Everything = 0x7FFFFFFF
    }

    public interface IRenderFilterNode : IDisposable
    {
        /// <summary>
        /// Indicates which aspects of a renderer this node changed, relative to the node prior to the last Update
        /// call. This may trigger updates of downstream nodes.
        ///
        /// This value is ignored on the first generation of the node, created from `IRenderFilter.Instantiate`.
        /// </summary>
        public RenderAspects WhatChanged { get; }

        /// <summary>
        /// Recreates this RenderFilterNode, with a new set of target renderers. The node _may_ reuse state, including
        /// things such as output RenderTextures, from its prior run. It may also fast-fail and return null; in this
        /// case, the preview pipeline will create a new node from its original `IRenderFilter` instead. Finally,
        /// it may return itself; in this case, it will continue to be used with the new renderers. 
        ///
        /// This function is passed a list of original-proxy object pairs, which are guaranteed to have the same
        /// original objects, in the same order, as the initial call to Instantiate, but will have new proxy objects.
        /// It is also passed an update flags field, which indicates which upstream nodes have changed since the last
        /// update. This may be zero if the update was triggered by an invalidation on the compute context for this
        /// node itself.
        ///
        /// As with `IRenderFilter.Instantiate`, the OnFrame effects of prior stages in the pipeline will be applied
        /// before invoking this function. This ensures any changes to bones, blendshapes, etc will be reflected in this
        /// mesh.
        ///
        /// This function must not destroy the original Node. If it chooses to share resources with the original node,
        /// those resources must not be released until both old and new nodes are destroyed.
        /// </summary>
        /// <param name="proxyPairs"></param>
        /// <param name="context"></param>
        /// <param name="renderFilterContext"></param>
        /// <param name="updatedAspects"></param>
        /// <returns></returns>
        public Task<IRenderFilterNode> Refresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            return Task.FromResult<IRenderFilterNode>(null);
        }

        /// <summary>
        /// Invoked on each frame, and may modify the target renderers bound to this render filter node. Generally,
        /// you should not modify the mesh or materials in this method, but you may change other properties, such as
        /// the bones array, blend shapes, or the active state of the renderer. These properties will be reset to the
        /// original renderer state on each frame.
        ///
        /// This function is passed the original and replacement renderers, and is invoked for each renderer in question.
        /// If an original renderer is destroyed, OnFrame will be called only on remaining renderers, until the preview
        /// pipeline rebuild is completed.
        /// </summary>
        public void OnFrame(Renderer original, Renderer proxy)
        {
        }

        void IDisposable.Dispose()
        {
        }

        // Allow for future expansion
        [ExcludeFromDocs]
        [UsedImplicitly]
        void __please_enable_dotnet_80_or_higher_for_default_methods()
        {
        }
    }
}