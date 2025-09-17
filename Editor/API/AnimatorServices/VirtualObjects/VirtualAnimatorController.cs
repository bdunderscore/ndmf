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

        private AnimatorControllerParameter CloneParameter(AnimatorControllerParameter acp)
        {
            return new AnimatorControllerParameter
            {
                name = acp.name,
                type = acp.type,
                defaultBool = acp.defaultBool,
                defaultFloat = acp.defaultFloat,
                defaultInt = acp.defaultInt
            };
        }

        // For tests
        internal bool DetectedParametersInteriorMutability;

        private Dictionary<string, AnimatorControllerParameterType> _lastParameterType = new();

        private void CheckParameterMutation()
        {
            foreach (var (k, v) in _parameters)
            {
                if (DetectedParametersInteriorMutability) break;

                if (_lastParameterType.TryGetValue(k, out var lastType) && lastType != v.type)
                {
                    DetectedParametersInteriorMutability = true;
                    Debug.LogError($"Parameter {k} changed type from {lastType} to {v.type} in-place. This is not " +
                                   "supported and will become an error in future versions. If you need to change a " +
                                   "parameter type, create a new AnimatorControllerParameter object and use the " +
                                   "Parameters setter to update the parameters map.");
                }
            }
        }

        private void UpdateParameterCache()
        {
            _lastParameterType.Clear();

            foreach (var (k, v) in _parameters)
            {
                _lastParameterType[k] = v.type;
            }
        }
        
        /// <summary>
        /// The animator controller parameters. The value returned from this property is an immutable dictionary,
        /// with all values being cloned parameters, so you must explicitly set this property in order to change it.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public ImmutableDictionary<string, AnimatorControllerParameter> Parameters
        {
            // Clone all of the parameter objects to ensure immutability
            /*get => _parameters.ToImmutableDictionary(
                p => p.Key,
                p => CloneParameter(p.Value)
            );*/

            // For backwards compatibility, we can't clone parameters (yet). But we can detect when mutability
            // occurs and issue a log message (and eventually make it a fatal error)
            get => _parameters;
            set
            {
                if (ReferenceEquals(_parameters, value)) return;
                CheckParameterMutation();
                
                var oldParameters = _parameters;
                _parameters = I(value ?? throw new ArgumentNullException(nameof(value)))
                    // Clone all of the parameter objects to ensure immutability
                    .ToImmutableDictionary(
                        p => p.Key,
                        p => CloneParameter(p.Value)
                    );
                UpdateParameterCache();

                var changed = oldParameters.Keys.SelectMany(key =>
                {
                    if (!_parameters.TryGetValue(key, out var newParam))
                    {
                        return Enumerable
                            .Empty<(string, AnimatorControllerParameterType, AnimatorControllerParameterType)>();
                    }

                    if (oldParameters[key].type == newParam.type)
                    {
                        return Enumerable
                            .Empty<(string, AnimatorControllerParameterType, AnimatorControllerParameterType)>();
                    }

                    return new[] { (key, oldParameters[key].type, newParam.type) };
                }).ToList();
                if (changed.Count > 0)
                {
                    _context.PlatformBindings.OnParameterTypeChanges(this, changed);
                }
            }
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
        ///     Returns all layers in this controller.
        ///
        ///     When used in 'set' mode, the set of layers will be replaced by the provided value, with all layers
        ///     at layer priority zero.
        /// </summary>
        public IEnumerable<VirtualLayer> Layers
        {
            get { return _layers.Values.SelectMany(l => l.Layers); }
            set
            {
                _layerPriorities.Clear();
                _layers.Clear();

                foreach (var layer in value)
                {
                    AddLayer(new LayerPriority(0), layer);
                }
            }
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

        /// <summary>
        ///     Sets the layer weight for all layers with a zero
        ///     [OriginalPhysicalLayerIndex](xref:VirtualLayer.OriginalPhysicalLayerIndex)
        ///     to one, and sets [OriginalPhysicalLayerIndex](xref:VirtualLayer.OriginalPhysicalLayerIndex) to null for all
        ///     layers except the first layer.
        ///     This should be invoked after merging controllers to correct for the fact that Unity considers the first layer
        ///     to always have weight one, even if the serialized weight is not one.
        ///     This function is automatically invoked when deactivating the VirtualControllerContext.
        /// </summary>
        public void NormalizeFirstLayerWeights()
        {
            var isFirst = true;
            foreach (var layer in Layers)
            {
                if (layer.OriginalPhysicalLayerIndex == 0)
                {
                    layer.DefaultWeight = 1;
                }

                layer.OriginalPhysicalLayerIndex = isFirst ? 0 : null;

                isFirst = false;
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
            _parameters = controller.parameters
                .Select(CloneParameter)
                .GroupBy(p => p.name)
                .Select(pg => pg.Last())
                .ToImmutableDictionary(p => p.name);
            UpdateParameterCache();

            var srcLayers = controller.layers;
            _virtualLayerBase = context.AllocateVirtualLayerSpace(srcLayers.Length);

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
        private readonly int _virtualLayerBase;

        AnimatorController ICommittable<AnimatorController>.Prepare(CommitContext context)
        {
            _context.PlatformBindings.PreCommitController(this);

            CheckParameterMutation();
            
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

        /// <summary>
        /// Re-virtualize behaviors when reactivating the context.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal void Reactivate()
        {
            var layers = Layers.ToList();
            using var _ = this._context.PushPhysicalToVirtualLayerMapper(physLayer =>
            {
                if (physLayer < 0 || physLayer >= layers.Count) return -1;
                return layers[physLayer].VirtualLayerIndex;
            });

            foreach (var layer in layers)
            {
                foreach (var behaviors in layer.SyncedLayerBehaviourOverrides.Values)
                {
                    foreach (var behavior in behaviors)
                    {
                        _context.PlatformBindings.VirtualizeStateBehaviour(_context, behavior);
                    }
                }
            }

            foreach (var node in AllReachableNodes())
            {
                IEnumerable<StateMachineBehaviour> behaviours;

                if (node is VirtualState vs)
                {
                    behaviours = vs.Behaviours;
                }
                else if (node is VirtualStateMachine vsm)
                {
                    behaviours = vsm.Behaviours;
                }
                else
                {
                    continue;
                }

                foreach (var behavior in behaviours)
                {
                    this._context.PlatformBindings.VirtualizeStateBehaviour(this._context, behavior);
                }
            }
        }
    }
}