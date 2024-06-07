#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    public interface IRenderFilter
    {
        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; }

        /// <summary>
        /// Instantiates a node in the preview graph. This operation is used when creating a new proxy renderer, and may
        /// perform relatively heavyweight operations to prepare the Mesh, Materials, and Textures for the renderer. It
        /// may not modify other aspects of the renderer; however, these can be done in the OnFrame callback in the
        /// returned IRenderFilterNode.
        ///
        /// When making changes to meshes, textures, and materials, this node must create new instances of these objects,
        /// and destroy them in `IRenderFilterNode.Dispose`.
        /// </summary>
        /// <param name="proxyPairs">An enumerable of (original, proxy) renderer pairs</param>
        /// <param name="context">A compute context that is used to track which values your code depended on in
        /// configuring this node. Changing these values will triger a recomputation of this node.</param>
        /// <returns></returns>
        public Task<IRenderFilterNode> Instantiate(IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context);
    }

    public interface IRenderFilterNode : IDisposable
    {
        /// <summary>
        /// The sharedMesh value
        /// </summary>
        public const ulong Mesh = 1;

        /// <summary>
        /// Materials and their properties
        /// </summary>
        public const ulong Material = 2;

        /// <summary>
        /// The contents of the textures bound to materials
        /// </summary>
        public const ulong Texture = 4;

        /// <summary>
        /// Blendshapes and the bones array
        /// </summary>
        public const ulong Shapes = 8;

        public const ulong Everything = Mesh | Material | Texture | Shapes;

        /// <summary>
        /// Indicates which static aspects of a renderer this node examines. Changes to these aspects will trigger a
        /// rebuild or partial update of this node.
        /// </summary>
        public ulong Reads { get; }

        /// <summary>
        /// Indicates which aspects of a renderer this node changed, relative to the node prior to the last Update
        /// call. This may trigger updates of downstream nodes.
        ///
        /// This value is ignored on the first generation of the node, created from `IRenderFilter.Instantiate`.
        /// </summary>
        public ulong WhatChanged { get; }

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
        /// <param name="updateFlags"></param>
        /// <returns></returns>
        public Task<IRenderFilterNode> Refresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            ulong updateFlags
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
    }
}