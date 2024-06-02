#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using nadena.dev.ndmf.rq;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal struct ProxyNodeKey
    {
        public IRenderFilter Filter;
        public ImmutableList<long> Sources; // Sorted

        public ProxyNodeKey(Renderer r)
        {
            Filter = null;
            Sources = ImmutableList<long>.Empty.Add(r.GetInstanceID());
        }

        public ProxyNodeKey(IRenderFilter filter, IEnumerable<long> sources)
        {
            Filter = filter;
            Sources = sources.OrderBy(t => t).ToImmutableList();
        }

        public bool Equals(ProxyNodeKey other)
        {
            return ReferenceEquals(Filter, other.Filter) && Sources.SequenceEqual(other.Sources);
        }

        public override bool Equals(object obj)
        {
            return obj is ProxyNodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Filter != null ? Filter.GetHashCode() : 0);

                foreach (var source in Sources)
                {
                    hashCode = hashCode * 397 ^ source.GetHashCode();
                }

                return hashCode;
            }
        }
    }

    internal class ProxyNode : IDisposable
    {
        private static long IdSequence = Int32.MaxValue;

        public long Id { get; }
        public ProxyNodeKey Key { get; private set; }
        public IRenderFilter Filter => Key.Filter;

        public ImmutableDictionary<Renderer, ProxyNode> SourceNodes { get; }
        public Task<ImmutableDictionary<Renderer, MeshState>> PrepareTask { get; }

        private readonly TaskCompletionSource<object> _invalidater = new();
        public Task InvalidatedTask => _invalidater.Task;

        public bool Invalidated => InvalidatedTask.IsCompleted;
        public bool Disposed { get; private set; }

        public ProxyNode(Renderer renderer)
        {
            Id = renderer.GetInstanceID();
            SourceNodes = ImmutableDictionary<Renderer, ProxyNode>.Empty;
            Key = new ProxyNodeKey(null, new long[] { renderer.GetInstanceID() });
            PrepareTask = Task.FromResult(ImmutableDictionary<Renderer, MeshState>.Empty.Add(
                renderer,
                new MeshState(renderer)
            ));
        }

        public ProxyNode(
            IRenderFilter filter,
            IImmutableList<Renderer> renderGroup,
            ImmutableDictionary<Renderer, ProxyNode> sourceNodes
        )
        {
            Id = (IdSequence++);
            SourceNodes = renderGroup.ToImmutableDictionary(r => r, r => sourceNodes[r]);

            Key = new ProxyNodeKey(filter, renderGroup.Select(r => sourceNodes[r].Id));

            foreach (var node in SourceNodes.Values)
            {
                node.InvalidatedTask.ContinueWith(_ =>
                {
                    Debug.Log("Trigger invalidate");
                    Invalidate();
                });
            }

            using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
            {
                ComputeContext context = new ComputeContext(() => "ProxyNode");
                context.OnInvalidate = InvalidatedTask;
                context.Invalidate = () =>
                {
                    Debug.Log("Trigger invalidate");
                    Invalidate();
                };

                PrepareTask = Task.Factory.StartNew(
                    async () =>
                    {
                        // Wait for all tasks to complete, or for invalidation
                        var inputTasks = SourceNodes.Values.Select(n => n.PrepareTask).ToList();
                        var allInputReady = Task.WhenAll(inputTasks).PreventRecursion();
                        await Task.WhenAny(allInputReady, InvalidatedTask);

                        if (Invalidated) return null;

                        Debug.Log($"Execute node " + Id + " for filter " + filter);
                        var inputMeshes = SourceNodes.Select(kvp =>
                        {
                            var node = kvp.Value;
                            var state = kvp.Value.PrepareTask.Result[kvp.Key];
                            Debug.Log($"Input node ID " + node.Id + " mesh name " + state.Mesh?.name);

                            return state.Clone(Id);
                        }).ToList();

                        await filter.MutateMeshData(inputMeshes, context);

                        return inputMeshes.ToImmutableDictionary(m => m.Original);
                    },
                    context.CancellationToken,
                    TaskCreationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext()
                ).Unwrap();
            }
        }

        public void Invalidate()
        {
            Debug.Log("Invalidate");
            _invalidater.TrySetResult(null);
        }

        public void Dispose()
        {
            Invalidate();
            Disposed = true;

            PrepareTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    var value = t.Result;
                    if (value != null)
                    {
                        foreach (var meshState in value.Values)
                        {
                            meshState.Dispose();
                        }
                    }
                }
            });

            // clear private state?
        }
    }
}