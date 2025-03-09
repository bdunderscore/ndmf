#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Represents an animator controller that has been indexed by NDMF for faster manipulation. This class also
    ///     guarantees that certain assets have been cloned, specifically:
    ///     - AnimatorController
    ///     - StateMachine
    ///     - AnimatorState
    ///     - AnimatorStateTransition
    ///     - BlendTree
    ///     - AnimationClip
    ///     - Any state behaviors attached to the animator controller
    /// </summary>
    [PublicAPI]
    public sealed class VirtualAnimatorController : VirtualNode, ICommittable<AnimatorController>
    {
        private readonly CloneContext _context;
        public string Name { get; set; }

        private ImmutableDictionary<string, AnimatorControllerParameter> _parameters;

        public ImmutableDictionary<string, AnimatorControllerParameter> Parameters
        {
            get => _parameters;
            set => _parameters = I(value ?? throw new ArgumentNullException(nameof(value)));
        }

        private readonly SortedDictionary<LayerPriority, LayerGroup> _layers = new();
        private readonly Dictionary<VirtualLayer, LayerPriority> _layerPriorities = new();

        private struct LayerGroup
        {
            public List<VirtualLayer> Layers;
        }

        /// <summary>
        ///     Constructs a new animator controller
        /// </summary>
        /// <param name="context">
        ///     The CloneContext to use for virtual layer assignment (can be obtained from
        ///     @"VirtualControllerContext")
        /// </param>
        /// <param name="name">The name of the new controller</param>
        /// <returns></returns>
        public static VirtualAnimatorController Create(CloneContext context, string name = "(unnamed)")
        {
            return new VirtualAnimatorController(context, name);
        }

        private VirtualAnimatorController(CloneContext context, string name)
        {
            _context = context;
            _parameters = ImmutableDictionary<string, AnimatorControllerParameter>.Empty;
            Name = name;
        }

        /// <summary>
        ///     Adds a layer to this controller
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="layer"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddLayer(LayerPriority priority, VirtualLayer layer)
        {
            if (_layerPriorities.ContainsKey(layer))
            {
                throw new InvalidOperationException("Layer is already in the controller");
            }

            _layerPriorities[layer] = priority;
            
            Invalidate();

            if (!_layers.TryGetValue(priority, out var group))
            {
                group = new LayerGroup { Layers = new List<VirtualLayer>() };
                _layers.Add(priority, group);
            }

            group.Layers.Add(layer);
        }

        /// <summary>
        ///     Creates a new layer and adds it to this controller
        /// </summary>
        /// <param name="priority"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public VirtualLayer AddLayer(LayerPriority priority, string name)
        {
            // implicitly creates state machine
            var layer = VirtualLayer.Create(_context, name);

            AddLayer(priority, layer);

            return layer;
        }

        /// <summary>
        ///     Returns all layers in this controller
        /// </summary>
        public IEnumerable<VirtualLayer> Layers
        {
            get { return _layers.Values.SelectMany(l => l.Layers); }
        }

        /// <summary>
        ///     Removes a layer from this controller
        /// </summary>
        /// <param name="layer"></param>
        public void RemoveLayer(VirtualLayer layer)
        {
            if (_layerPriorities.TryGetValue(layer, out var priority))
            {
                if (_layers.TryGetValue(priority, out var group))
                {
                    group.Layers.Remove(layer);
                    if (group.Layers.Count == 0)
                    {
                        _layers.Remove(priority);
                    }
                }
            }
        }

        /// <summary>
        ///     Removes all layers that match the given predicate
        /// </summary>
        /// <param name="shouldRemove"></param>
        public void RemoveLayers(Func<VirtualLayer, bool> shouldRemove)
        {
            foreach (var (prio, layers) in _layers.ToList())
            {
                foreach (var layer in layers.Layers.ToList())
                {
                    if (shouldRemove(layer))
                    {
                        layers.Layers.Remove(layer);
                        _layerPriorities.Remove(layer);
                    }
                }

                if (layers.Layers.Count == 0)
                {
                    _layers.Remove(prio);
                }
            }
        }

        internal static VirtualAnimatorController Clone(CloneContext context, RuntimeAnimatorController controller)
        {
            switch (controller)
            {
                case AnimatorController ac: return new VirtualAnimatorController(context, ac);
                case AnimatorOverrideController aoc:
                {
                    using var _ = context.PushOverrideController(aoc);

                    return Clone(context, aoc.runtimeAnimatorController);
                }
                default: throw new NotImplementedException($"Unknown controller type {controller.GetType()}");
            }
        }

        private VirtualAnimatorController(CloneContext context, AnimatorController controller)
        {
            _context = context;
            Name = controller.name;
            _parameters = controller.parameters.ToImmutableDictionary(p => p.name);

            var srcLayers = controller.layers;
            context.AllocateVirtualLayerSpace(srcLayers.Length);

            var p0Layers = srcLayers
                .Select((l, i) => VirtualLayer.Clone(context, l, i))
                .ToList();
            foreach (var layer in p0Layers)
            {
                layer.IsOriginalLayer = true;
            }

            _layers[new LayerPriority(0)] = new LayerGroup { Layers = p0Layers };
        }

        private AnimatorController? _cachedController;

        AnimatorController ICommittable<AnimatorController>.Prepare(CommitContext context)
        {
            if (_cachedController == null) _cachedController = new AnimatorController();

            _cachedController.name = Name;
            _cachedController.parameters = Parameters
                .OrderBy(p => p.Key)
                .Select(p =>
                {
                    p.Value.name = p.Key;
                    return p.Value;
                })
                .ToArray();

            foreach (var (layer, index) in Layers.Select((l, i) => (l, i)))
            {
                context.RegisterVirtualLayerMapping(layer, layer.VirtualLayerIndex);
                context.RegisterPhysicalLayerMapping(index, layer);
            }

            return _cachedController;
        }

        void ICommittable<AnimatorController>.Commit(CommitContext context, AnimatorController obj)
        {
            obj.layers = Layers.Select(context.CommitObject).ToArray();
        }

        protected override IEnumerable<VirtualNode> _EnumerateChildren()
        {
            return Layers;
        }
    }
}