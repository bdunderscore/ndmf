#region

using System;
using System.Collections;
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
        internal ImmutableDictionary<Renderer, string> DebugNames { get; }
        
        public bool IsEmpty => Renderers.IsEmpty;

        internal RenderGroup(ImmutableList<Renderer> renderers, ImmutableDictionary<Renderer, string> debugNames)
        {
            Renderers = renderers;
            DebugNames = debugNames;
        }

        internal RenderGroup WithoutData()
        {
            return new RenderGroup(Renderers, DebugNames);
        }

        public static RenderGroup For(IEnumerable<Renderer> renderers)
        {
            var frozen = renderers.OrderBy(r => r.GetInstanceID()).ToImmutableList();
            var names = frozen.ToImmutableDictionary(r => r, r => r.name);
            return new RenderGroup(frozen, names);
        }

        public static RenderGroup For(Renderer renderer)
        {
            return new RenderGroup(
                ImmutableList.Create(renderer),
                ImmutableDictionary.Create<Renderer, string>()
                    .Add(renderer, renderer.name)
            );
        }

        public RenderGroup WithData<T>(T data)
        {
            return new RenderGroup<T>(Renderers, DebugNames, data);
        }

        internal virtual RenderGroup Filter(HashSet<Renderer> activeRenderers)
        {
            return new RenderGroup(Renderers.RemoveAll(r => !activeRenderers.Contains(r)), DebugNames);
        }

        public T GetData<T>()
        {
            return ((RenderGroup<T>)this).Context;
        }

        protected bool Equals(RenderGroup other)
        {
            return Renderers.SequenceEqual(other.Renderers);
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
            var hashCode = 0;
            foreach (var renderer in Renderers)
            {
                hashCode = HashCode.Combine(hashCode, renderer.GetInstanceID());
            }

            return hashCode;
        }

        public override string ToString()
        {
            return "RenderGroup(" + string.Join(", ", Renderers.Select(r => r.name)) + ")";
        }

        internal virtual RenderGroup FilterLive()
        {
            if (Renderers.All(r => r != null)) return this;

            return new RenderGroup(Renderers.Where(r => r != null).ToImmutableList(), DebugNames);
        }
    }

    internal sealed class RenderGroup<T> : RenderGroup
    {
        public T Context { get; }

        internal RenderGroup(ImmutableList<Renderer> renderers, ImmutableDictionary<Renderer, string> DebugNames,
            T context) : base(renderers, DebugNames)
        {
            Context = context;
        }
        
        internal override RenderGroup Filter(HashSet<Renderer> activeRenderers)
        {
            return new RenderGroup<T>(Renderers.RemoveAll(r => !activeRenderers.Contains(r)), DebugNames, Context);
        }
        
        private bool Equals(RenderGroup<T> other)
        {
            if (Context is IEnumerable l && other.Context is IEnumerable ol)
            {
                // This is a common mistake; List does not implement a useful Equals for us. Work around it
                // on behalf of our consumers...
                return base.Equals(other) && l.Cast<object>().SequenceEqual(ol.Cast<object>());
            }
            
            return base.Equals(other) && EqualityComparer<T>.Default.Equals(Context, other.Context);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is RenderGroup<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            if (Context is IEnumerable l)
            {
                return l.Cast<object>().Aggregate(base.GetHashCode(), (acc, o) => HashCode.Combine(acc, o.GetHashCode()));
            }
            
            return HashCode.Combine(base.GetHashCode(), EqualityComparer<T>.Default.GetHashCode(Context));
        }

        internal override RenderGroup FilterLive()
        {
            if (Renderers.All(r => r != null)) return this;

            return new RenderGroup<T>(Renderers.Where(r => r != null).ToImmutableList(), DebugNames, Context);
        }
    }

    [PublicAPI]
    public interface IRenderFilter
    {
        /// <summary>
        /// Set to true if this RenderFilter might enable a renderer which is otherwise disabled. If you do, this should
        /// be performed by changing the `enabled` property of the proxy renderer.
        ///
        /// Note that when enabling proxy renderers, it's up to you to consider the state of parent objects of the
        /// original; if you set enabled to true, it'll be displayed.
        ///
        /// If all render filters interacting with a particular renderer have CanEnableRenderers set to false, the
        /// preview pipeline might skip all preview processing for that renderer.
        /// </summary>
        public virtual bool CanEnableRenderers => false;

        /// <summary>
        /// Indicates that the preview system must not remove renderers from a multi-node render group, even if they
        /// are disabled. If this is set to true, then all renderers you return in a group from GetTargetGroups will be
        /// included when that group is instantiated.
        ///
        /// Note that even if this is true, the preview system can eliminate nodes where _all_ renderers are disabled.
        /// </summary>
        public virtual bool StrictRenderGroup => false;
        
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

        /// <summary>
        ///     Invoked on each frame, once per render group.
        /// </summary>
        void OnFrameGroup()
        {
        }
    }
}