#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using API;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;
#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     This extension context converts all "innate" animator controllers bound to the avatar into virtual controllers.
    ///     By "innate", we mean controllers which are understood by the underlying platform (e.g. VRChat). As part of this,
    ///     the controllers and animations are cloned, so that changes to them do not affect the original assets.
    ///     This context acts as a key-value map from arbitrary context keys to virtual controllers. For VRChat controllers,
    ///     the context will be a `VRCAvatarDescriptor.AnimLayerType` enum value. For other sub-components which have
    ///     animator controllers (e.g. the unity `Animator` component), the context key will be that controller. Otherwise,
    ///     it's up to the IPlatformAnimatorBindings implementation to define the context key.
    ///     Upon deactivation, any changes to virtual controllers will be written back to their sources.
    ///     You may also add arbitrary virtual controllers to the context, by setting the value for a given key. These
    ///     controllers will be generally ignored, unless you choose to do something with them. Note that these controllers
    ///     will not be preserved across a context deactivation.
    ///     ## Limitations
    ///     When this context is active, you must not modify the original controllers or their animations. This is because
    ///     certain virtual objects may reference the original assets, and thus changes to them may result in undefined
    ///     behavior.
    ///     After deactivating this context, you must not modify the virtual controllers or their animations. This is because
    ///     subsequent NDMF processing steps may directly modify the serialized animator controllers; conversely, when the
    ///     virtual controller context is reactivated, it may or may not reuse the same virtual nodes as before.
    /// </summary>
    [PublicAPI]
    public sealed class VirtualControllerContext : IExtensionContext
    {
        private class LayerState
        {
            internal static readonly FieldInfo? f_OnAnimatorControllerDirty =
                AccessTools.Field(typeof(AnimatorController), "OnAnimatorControllerDirty");

            internal Object? OriginalObject;
            internal VirtualAnimatorController? VirtualController;
            internal Object? LastCommit;
            internal object? LastInnateKey;

            internal Object? SavedCommittedController;

            public LayerState(Object? originalObject)
            {
                OriginalObject = originalObject;
            }

            public void MarkCommitted(RuntimeAnimatorController committedController, Object? committedObject = null)
            {
                SavedCommittedController = committedController;
                committedObject ??= committedController;
                
                if (f_OnAnimatorControllerDirty != null)
                {
                    LastCommit = committedObject;
                    Action invalidate = () =>
                    {
                        if (LastCommit == committedObject) LastCommit = null;
                        f_OnAnimatorControllerDirty.SetValue(committedController, null);
                    };
                    f_OnAnimatorControllerDirty.SetValue(committedController, invalidate);
                }
            }

            public VirtualAnimatorController GetVirtualController(CloneContext context)
            {
                if (VirtualController != null) return VirtualController;

                if (OriginalObject is RuntimeAnimatorController controller)
                {
                    LastInnateKey = context.ActiveInnateLayerKey;
                    using var _ = context.PushDistinctScope();
                    return VirtualController = context.Clone(controller);
                }

                if (OriginalObject is Motion m)
                {
                    VirtualController = VirtualAnimatorController.Create(context, "Container for " + m.name);
                    var layer = VirtualController.AddLayer(LayerPriority.Default, "Container");
                    var vsm = VirtualStateMachine.Create(context, "Container");
                    layer.StateMachine = vsm;

                    using var _ = context.PushDistinctScope();
                    var state = vsm.AddState("Container", context.Clone(m));
                    vsm.DefaultState = state;

                    return VirtualController;
                }

                throw new NotImplementedException("Can't virtualize object of type " + OriginalObject?.GetType());
            }

            public void Revalidate(CloneContext context, RuntimeAnimatorController newController)
            {
                if (LastCommit == newController && VirtualController != null)
                {
                    // We'll reuse this controller. However, remove all layers from the RuntimeAnimatorController to
                    // reduce overhead from OnAnimatorControllerDirty callbacks
                    if (newController is AnimatorController ac)
                    {
                        ac.layers = Array.Empty<AnimatorControllerLayer>();
                    }

                    using var _ = context.PushActiveInnateKey(LastInnateKey);
                    VirtualController.Reactivate();
                }
                else
                {
                    if (OriginalObject != null) Debug.Log("Controller " + newController + " was changed outside of NDMF animator services; cloning a second time");
                    // force reload from unity object
                    OriginalObject = newController;
                    VirtualController = null;
                    LastCommit = null;
                }
            }

            public void Revalidate(Motion motion)
            {
                if (LastCommit == motion)
                {
                    // no-op
                }
                else
                {
                    OriginalObject = motion;
                    VirtualController = null;
                    LastCommit = null;
                }
            }
        }
        
        private readonly Dictionary<object, LayerState> _layerStates = new();

        // initialized on activate
        private IPlatformAnimatorBindings? _platformBindings;
        private CloneContext? _cloneContext;

        public CloneContext CloneContext =>
            _cloneContext ?? throw new InvalidOperationException("Extension context not initialized");

        public IPlatformAnimatorBindings PlatformBindings =>
            _platformBindings ?? throw new InvalidOperationException("Extension context not initialized");
        
        /// <summary>
        ///     This value is updated every time the set of virtual controllers changes.
        /// </summary>
        public long CacheInvalidationToken { get; private set; }

        /// <summary>
        ///     A mapping of tag objects to @"VirtualAnimatorController"s. The tag object can be, for example, a
        ///     @"VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType", an @"IVirtualizeAnimatorController",
        ///     an @"IVirtualizeMotion", or any other arbitrary object. The default equality operator is used to compare
        ///     tags.
        ///     This dictionary supports adding new entries as well as removing them.
        /// </summary>
        public IDictionary<object, VirtualAnimatorController> Controllers { get; private set; } = null!;

        public void OnActivate(BuildContext context)
        {
            var root = context.AvatarRootObject;

            Controllers = new FilteredDictionaryView<object, LayerState, VirtualAnimatorController>(
                _layerStates,
                new HashSet<object>(),
                (key, state) =>
                {
                    using var _ = CloneContext.PushActiveInnateKey(key);
                    return state.GetVirtualController(CloneContext);
                },
                (k, v) =>
                {
                    CacheInvalidationToken++;
                    _layerStates[k] = new LayerState(null) { VirtualController = v };
                }
            );

            #if NDMF_VRCSDK3_AVATARS
            if (root.TryGetComponent<VRCAvatarDescriptor>(out _))
            {
                _platformBindings = VRChatPlatformAnimatorBindings.Instance;
            }
            else
            {
                _platformBindings = GenericPlatformAnimatorBindings.Instance;
            }
            #else
            _platformBindings = GenericPlatformAnimatorBindings.Instance;
            #endif

            if (_cloneContext == null) _cloneContext = new CloneContext(_platformBindings);

            var innateControllers = _platformBindings.GetInnateControllers(root);
            CacheInvalidationToken++;

            foreach (var (type, controller, _) in innateControllers)
            {
                if (_layerStates.TryGetValue(type, out var currentLayer))
                {
                    currentLayer.Revalidate(CloneContext, controller);
                }
                else
                {
                    _layerStates[type] = new LayerState(controller);
                }

                // Force all layers to be processed, for now. This avoids compatibility issues with NDMF
                // plugins which assume that all layers have been cloned after MA runs.
                _ = Controllers[type];
            }

            ActivateVirtualizedMotions(context);
        }

        private void ActivateVirtualizedMotions(BuildContext context)
        {
            foreach (var virtualizeController in context.AvatarRootObject
                         .GetComponentsInChildren<IVirtualizeAnimatorController>(true))
            {
                var ac = virtualizeController.AnimatorController;
                if (ac == null)
                {
                    _layerStates.Remove(virtualizeController);
                    continue;
                }

                if (virtualizeController.GetMotionBasePath(context, false) == "" &&
                    _layerStates.TryGetValue(virtualizeController, out var currentLayer))
                {
                    currentLayer.Revalidate(CloneContext, ac);
                }
                else
                {
                    var virtualController = _layerStates[virtualizeController] = new LayerState(ac);
                    var basePath = virtualizeController.GetMotionBasePath(context);

                    using var _ = CloneContext.PushDistinctScope();
                    using var _k = CloneContext.PushActiveInnateKey(virtualizeController.TargetControllerKey);

                    var vc = virtualController.GetVirtualController(CloneContext);

                    if (basePath != "")
                    {
                        new AnimationIndex(new[] { vc })
                            .ApplyPathPrefix(basePath);
                    }
                }
            }

            foreach (var virtualizeMotion in context.AvatarRootObject.GetComponentsInChildren<IVirtualizeMotion>(true))
            {
                var motion = virtualizeMotion.Motion;
                if (motion == null)
                {
                    _layerStates.Remove(virtualizeMotion);
                    continue;
                }

                if (virtualizeMotion.GetMotionBasePath(context, false) == "" &&
                    _layerStates.TryGetValue(virtualizeMotion, out var currentLayer))
                {
                    currentLayer.Revalidate(virtualizeMotion.Motion);
                }
                else
                {
                    var virtualMotion = _layerStates[virtualizeMotion] = new LayerState(motion);
                    var basePath = virtualizeMotion.GetMotionBasePath(context);

                    using var _ = CloneContext.PushDistinctScope();
                    var vc = virtualMotion.GetVirtualController(CloneContext);

                    if (basePath != "")
                    {
                        new AnimationIndex(new[] { vc })
                            .ApplyPathPrefix(basePath);
                    }
                }

                // Because IVirtualizeMotion is new, we don't need to worry about compatibility with older plugins
            }
        }

        /// <summary>
        ///     "Forgets" a specific controller. This should usually only be done for controllers which are no longer
        ///     relevant, e.g. if the corresponding component has been removed.
        /// </summary>
        /// <param name="key"></param>
        public void ForgetController(object key)
        {
            CacheInvalidationToken++;
            _layerStates.Remove(key);
        }

        public void OnDeactivate(BuildContext context)
        {
            var root = context.AvatarRootObject;

            var commitContext = new CommitContext(CloneContext.PlatformBindings);
            commitContext.NodeToReference = CloneContext.NodeToReference;

            // Purge any container controllers we don't need anymore
            foreach (var kv in _layerStates.ToList())
            {
                if (kv.Key is IVirtualizeMotion or IVirtualizeAnimatorController && (Component)kv.Key == null)
                {
                    _layerStates.Remove(kv.Key!);
                    if (kv.Key is IVirtualizeMotion && kv.Value.SavedCommittedController != null && context.IsTemporaryAsset(kv.Value.SavedCommittedController))
                    {
                        Object.DestroyImmediate(kv.Value.SavedCommittedController, true);
                    }
                }
            }

            var controllers = _layerStates
                .Where(kvp => kvp.Value.VirtualController != null)
                .ToDictionary(
                    kv => kv.Key,
                    kv =>
                    {
                        commitContext.ActiveInnateLayerKey = kv.Key;
                        kv.Value.VirtualController!.NormalizeFirstLayerWeights();
                        
                        var committed =
                            (RuntimeAnimatorController)commitContext.CommitObject(kv.Value.VirtualController!);
                        kv.Value.MarkCommitted(committed);

                        return committed;
                    });

            using (var scope = context.OpenSerializationScope())
            {
                _platformBindings!.CommitControllers(root, controllers);

                foreach (var kv in controllers)
                {
                    if (kv.Key is IVirtualizeMotion virtualizeMotion && (Component)virtualizeMotion != null)
                    {
                        var motion = virtualizeMotion.Motion =
                            ((AnimatorController)kv.Value).layers[0].stateMachine.defaultState.motion;
                        _layerStates[kv.Key].MarkCommitted(kv.Value, motion);
                    }
                    else if (kv.Key is IVirtualizeAnimatorController virtualizeController &&
                             (Component)virtualizeController != null)
                    {
                        virtualizeController.AnimatorController = kv.Value;
                        _layerStates[kv.Key].MarkCommitted(kv.Value);
                    }
                }
                
                // Save all animator objects to prevent references from breaking later
                foreach (var obj in commitContext.AllObjects)
                {
                    context.AssetSaver.SaveAsset(obj);
                }
            }
        }

        /// <summary>
        ///     Obtains the VirtualMotion for a given IVirtualizeMotion. Returns null if @"IVirtualizeMotion#Motion" is null.
        ///     If the @"IVirtualizeMotion" was not present in the avatar when the VirtualControllerContext was activated,
        ///     adds it to the context immediately.
        /// </summary>
        /// <param name="motion"></param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public VirtualMotion? GetVirtualizedMotion(IVirtualizeMotion motion)
        {
            if (motion.Motion == null) return null;

            if (!_layerStates.TryGetValue(motion, out var layerState))
            {
                _layerStates[motion] = layerState = new LayerState(motion.Motion);
            }

            return layerState
                .GetVirtualController(CloneContext)
                .Layers.First()
                .StateMachine?.DefaultState?.Motion;
        }

        public IEnumerable<VirtualAnimatorController> GetAllControllers()
        {
            return _layerStates.Select(kv => Controllers[kv.Key]).Where(v => v != null)!;
        }


        [return: NotNullIfNotNull("controller")]
        public VirtualAnimatorController? Clone(RuntimeAnimatorController? controller)
        {
            return CloneContext.Clone(controller);
        }

        [return: NotNullIfNotNull("layer")]
        public VirtualLayer? Clone(AnimatorControllerLayer? layer, int index)
        {
            return CloneContext.Clone(layer, index);
        }

        [return: NotNullIfNotNull("stateMachine")]
        public VirtualStateMachine? Clone(AnimatorStateMachine? stateMachine)
        {
            return CloneContext.Clone(stateMachine);
        }

        [return: NotNullIfNotNull("transition")]
        public VirtualStateTransition? Clone(AnimatorStateTransition? transition)
        {
            return CloneContext.Clone(transition);
        }

        [return: NotNullIfNotNull("transition")]
        public VirtualTransition? Clone(AnimatorTransition? transition)
        {
            return CloneContext.Clone(transition);
        }

        [return: NotNullIfNotNull("state")]
        public VirtualState? Clone(AnimatorState? state)
        {
            return CloneContext.Clone(state);
        }

        [return: NotNullIfNotNull("m")]
        public VirtualMotion? Clone(Motion? m)
        {
            return CloneContext.Clone(m);
        }

        [return: NotNullIfNotNull("clip")]
        public VirtualClip? Clone(AnimationClip? clip)
        {
            return CloneContext.Clone(clip);
        }
    }
}