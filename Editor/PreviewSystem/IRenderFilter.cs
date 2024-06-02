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
    public sealed class MeshState
    {
        internal long NodeId { get; }

        public Renderer Original { get; }

        private bool _meshIsOwned;
        private Mesh _mesh;

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

        public ImmutableList<Material> Materials
        {
            get => _materials;
            set
            {
                _materials = value;
                _materialsAreOwned = true;
            }
        }

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

    public interface IRenderFilter
    {
        public ReactiveValue<IImmutableList<IImmutableList<Renderer>>> TargetGroups { get; }

        public Task MutateMeshData(IList<MeshState> state, ComputeContext context)
        {
            return Task.CompletedTask;
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
        }
    }
}