#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Represents the current state of a mesh. IRenderFilters mutate this state in order to perform heavyweight portions
    /// of a preview rendering operation.
    ///
    /// TODO: This API is likely to change radically in future alpha releases.
    /// </summary>
    public sealed class MeshState
    {
        internal long NodeId { get; }

        /// <summary>
        /// The original renderer that this MeshState was born from
        /// </summary>
        public Renderer Original { get; }

        private bool _meshIsOwned;
        private Mesh _mesh;

        /// <summary>
        /// The current mesh associated with this renderer. Important: When setting this value, you must set to a _new_
        /// mesh. This new mesh will be destroyed when the preview state is recomputed.
        /// </summary>
        public Mesh Mesh
        {
            get => _mesh;
            set
            {
                _meshIsOwned = true;
                _mesh = value;
                if (_mesh != null) _mesh.name = "Mesh #" + NodeId;
            }
        }

        private bool _materialsAreOwned;
        private ImmutableList<Material> _materials;

        /// <summary>
        /// The materials associated with this mesh. Important: When setting this value, you must set to a list of entirely
        /// _new_ materials. These new materials will be destroyed when the preview state is recomputed.
        /// </summary>
        public ImmutableList<Material> Materials
        {
            get => _materials;
            set
            {
                _materials = value;
                _materialsAreOwned = true;
            }
        }

        /// <summary>
        /// An event which will be invoked when the mesh state is discarded. This can be used to destroy any resources
        /// you've created other than Meshes and Materials - e.g. textures.
        /// </summary>
        public event Action OnDispose;

        private bool _disposed = false;

        internal MeshState(Renderer renderer)
        {
            Original = renderer;

            if (renderer is SkinnedMeshRenderer smr)
            {
                Mesh = Object.Instantiate(smr.sharedMesh);
            }
            else if (renderer is MeshRenderer mr)
            {
                Mesh = Object.Instantiate(mr.GetComponent<MeshFilter>().sharedMesh);
            }

            Materials = renderer.sharedMaterials.Select(m => new Material(m)).ToImmutableList();
        }

        private MeshState(MeshState state, long nodeId)
        {
            Original = state.Original;
            _mesh = state._mesh;
            _materials = state._materials;
            NodeId = nodeId;
        }

        // Not IDisposable as we don't want to expose that as a public API
        internal void Dispose()
        {
            if (_disposed) return;

            if (_meshIsOwned) Object.DestroyImmediate(Mesh);
            if (_materialsAreOwned)
            {
                foreach (var material in Materials)
                {
                    Object.DestroyImmediate(material);
                }
            }

            OnDispose?.Invoke();
        }

        internal MeshState Clone(long nodeId)
        {
            return new MeshState(this, nodeId);
        }
    }

    /// <summary>
    /// An interface implemented by components which need to modify the appearance of a renderer for preview purposes.
    /// </summary>
    public interface IRenderFilter
    {
        /// <summary>
        /// A list of lists of renderers this filter operates on. The outer list is a list of renderer groups; each
        /// group of renderers will be passed to this filter as one unit, allowing for cross-renderer operation such
        /// as texture atlasing.
        /// </summary>
        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; }

        /// <summary>
        /// Performs any heavyweight operations required to prepare the renderers for preview. This method is called
        /// once when the preview pipeline is set up. You can use the attached ComputeContext to arrange for the
        /// preview pipeline to be recomputed when something changes with the initial state of the renderer.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task MutateMeshData(IList<MeshState> state, ComputeContext context)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called on each frame to perform lighter-weight operations on the renderers, such as manipulating blend shapes
        /// or the bones array. 
        /// </summary>
        /// <param name="original"></param>
        /// <param name="proxy"></param>
        public void OnFrame(Renderer original, Renderer proxy)
        {
        }
    }
}