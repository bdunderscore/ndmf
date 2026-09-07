#region

using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal sealed class ShadowBoneManager : IDisposable, IShadowBoneManagerHandle
    {
        private static int editorFrameCount;

        private static readonly ProfilerMarker UpdateMarker = new("ShadowBoneManager.Update");
        private static readonly ProfilerMarker CompleteMarker = new("ShadowBoneManager.CompleteTransfers");
        private static readonly ProfilerMarker ReadScheduleMarker = new("ShadowBoneManager.ReadTransforms.Schedule");
        private static readonly ProfilerMarker WriteScheduleMarker = new("ShadowBoneManager.WriteBoneStates.Schedule");

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.update += () => editorFrameCount++;
        }

        private sealed class BoneState
        {
            internal Transform Original;
            internal Transform Proxy;
            internal BoneState ParentHint;
            internal bool UsedInPipeline;
        }

        internal sealed class RendererReplacementState
        {
            internal readonly Dictionary<Transform, Transform> Mappings = new();
            internal readonly Transform[] OriginalBones;
            internal readonly Transform[] Bones;
            internal Transform RootBone, OriginalRootBone;
            internal Transform ProbeAnchor, OriginalProbeAnchor;
            internal Transform Parent, OriginalParent;

            internal RendererReplacementState(Renderer renderer)
            {
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    var bones = skinnedRenderer.bones;
                    OriginalBones = (Transform[])bones.Clone();
                    Bones = (Transform[])OriginalBones.Clone();
                    OriginalRootBone = RootBone = skinnedRenderer.rootBone;
                }
                else
                {
                    OriginalBones = Bones = Array.Empty<Transform>();
                }

                OriginalProbeAnchor = ProbeAnchor = renderer.probeAnchor;
                OriginalParent = Parent = renderer.transform.parent;
            }

            internal RendererReplacementState(RendererReplacementState source)
            {
                OriginalBones = source.OriginalBones;
                Bones = (Transform[])source.Bones.Clone();
                OriginalParent = source.OriginalParent;
                Parent = source.Parent;
                OriginalProbeAnchor = source.OriginalProbeAnchor;
                OriginalRootBone = source.OriginalRootBone;
                RootBone = source.RootBone;
                ProbeAnchor = source.ProbeAnchor;
                Mappings = new Dictionary<Transform, Transform>(source.Mappings);
            }
        }

        public sealed class StageScope : IDisposable
        {
            private readonly int _stage;
            private readonly ShadowBoneManager _manager;
            private readonly int? _previousStage;
            private readonly IReadOnlyDictionary<Renderer, Renderer> _stateProxies;
            private readonly Dictionary<Renderer, RendererReplacementState> _rollbackState = new();
            private readonly Dictionary<Renderer, RendererReplacementState> _pendingState = new();
            private bool _committed;

            public int? Stage => _stage;
            public bool IsDisposed { get; private set; }

            internal IReadOnlyDictionary<Renderer, Renderer> StageProxies => _stateProxies;

            internal StageScope(
                ShadowBoneManager manager,
                int stage,
                IReadOnlyDictionary<Renderer, Renderer> proxies
            )
            {
                _manager = manager;
                _previousStage = manager.CurrentStage;
                _stage = stage;
                _stateProxies = proxies;
                foreach (var pair in proxies) manager._setupProxies[pair.Key] = pair.Value;
                manager.ApplyImplicitReplacements(proxies);
            }

            public void Dispose()
            {
                if (IsDisposed) return;
                if (!_committed) Rollback();
                
                IsDisposed = true;
                if (_manager._activeScope.Value == this) _manager._activeScope.Value = null;
            }

            internal RendererReplacementState StateForRenderer(Renderer r)
            {
                if (_manager._activeScope.Value != this || IsDisposed)
                {
                    throw new InvalidOperationException(
                        "StateForRenderer may only be called from IRenderFilter.Instantiate or IRenderFilterNode.Refresh."
                    );
                }

                if (_pendingState.TryGetValue(r, out var state)) return state;

                // We initialize from the setup proxy so we can capture any non-SBM manipulation
                // happening.
                var setupProxy = _manager._setupProxies[r];
                state = new RendererReplacementState(setupProxy);
                _rollbackState[setupProxy] = new RendererReplacementState(state);
                _pendingState[r] = state;

                return state;
            }

            private void Rollback()
            {
                foreach (var (proxy, state) in _rollbackState)
                {
                    ApplyRendererState(proxy, state);
                }
            }

            public void Commit()
            {
                if (!_manager._replacements.TryGetValue(_stage, out var stageState))
                {
                    stageState = new Dictionary<Renderer, RendererReplacementState>();
                    _manager._replacements[_stage] = stageState;
                }

                foreach (var (k, v) in _pendingState)
                {
                    stageState[k] = v;
                }

                _committed = true;
            }
        }


        private readonly AsyncLocal<StageScope> _activeScope = new();
        private readonly Dictionary<int, Dictionary<Renderer, RendererReplacementState>> _replacements = new();
        
        private readonly Dictionary<Transform, BoneState> _bones = new();
        private readonly List<Transform> _toRemove = new();
        private readonly List<Renderer> _renderersToRemove = new();
        private readonly List<BoneState> _stateList = new();
        private readonly Dictionary<Renderer, int> _firstRendererStages = new();
        private readonly Dictionary<Transform, Transform> _implicitReplacements = new();
        private readonly Dictionary<Renderer, Renderer> _setupProxies = new();

        private TransformAccessArray _sourceBones;
        private TransformAccessArray _proxyBones;
        private NativeArray<bool> _boneIsValid;
        private NativeArray<TransformState> _boneStates;
        private bool _transferDataDirty = true;
        private int _lastUpdateFrame = -1;
        private bool _disposed;
        private bool _unexpectedDestroy = false;

        internal IShadowBoneManagerHandle Handle => this;
        internal long LeaseId { get; private set; }

        internal bool UnexpectedlyDestroyed => _unexpectedDestroy;

        internal int? CurrentStage
        {
            get
            {
                ThrowIfDisposed();
                return _activeScope.Value?.Stage;
            }
        }

        public Transform GetBone(Transform bone)
        {
            ThrowIfDisposed();
            if (bone == null) return null;
            PreviewContext.Instance.VerifyShadowBoneManagerLease(this);
            return GetBoneInternal(bone);
        }

        public void ReplaceBone(Renderer originalRenderer, Transform original, Transform replacement)
        {
            ThrowIfDisposed();
            PreviewContext.Instance.VerifyShadowBoneManagerLease(this);

            if (CurrentStage == null || _activeScope.Value == null)
            {
                throw new InvalidOperationException(
                    "ReplaceBone may only be called from IRenderFilter.Instantiate or IRenderFilterNode.Refresh.");
            }

            if (originalRenderer == null) throw new ArgumentNullException(nameof(originalRenderer));
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (!_activeScope.Value.StageProxies.TryGetValue(originalRenderer, out var proxy) || proxy == null)
            {
                throw new ArgumentException(
                    "The original renderer is not part of the active render-filter stage.",
                    nameof(originalRenderer)
                );
            }

            var state = _activeScope.Value.StateForRenderer(originalRenderer);

            if (state.Mappings.TryGetValue(original, out var previous) && previous != replacement)
            {
                ReplaceReferences(state, previous, replacement);
            }
            else if (_implicitReplacements.TryGetValue(original, out var implicitReplacement))
            {
                ReplaceReferences(state, implicitReplacement, replacement);
            }
            else
            {
                ReplaceReferences(state, original, replacement);
            }

            state.Mappings[original] = replacement;
            ApplyRendererState(proxy, state);
        }

        internal StageScope EnterStage(
            int stageIndex,
            IReadOnlyDictionary<Renderer, Renderer> proxies
        )
        {
            ThrowIfDisposed();
            var scope = new StageScope(this, stageIndex, proxies);
            _activeScope.Value = scope;

            return scope;
        }

        internal void RegisterRendererStage(Renderer original, int firstStageIndex)
        {
            ThrowIfDisposed();
            _firstRendererStages.TryAdd(original, firstStageIndex);
        }

        internal void SetInitialParent(Renderer original, Renderer proxy)
        {
            ThrowIfDisposed();
            if (original == null || proxy == null || original is SkinnedMeshRenderer) return;

            var targetParent = GetBoneInternal(original.transform);

            var proxyTransform = proxy.transform;
            if (targetParent == proxyTransform.parent) return;

            proxyTransform.SetParent(targetParent, false);
            proxyTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            proxyTransform.localScale = Vector3.one;
        }

        internal void OnFrameStart(int stageIndex, IReadOnlyDictionary<Renderer, Renderer> activeProxies)
        {
            ThrowIfDisposed();
            if (stageIndex == 0 && _lastUpdateFrame != editorFrameCount)
            {
                _lastUpdateFrame = editorFrameCount;
                UpdateAllBones();
            }

            ApplyImplicitReplacements(activeProxies);
            ReplayStage(stageIndex, activeProxies);
        }

        internal void ResetPipeline()
        {
            ThrowIfDisposed();
            unchecked
            {
                LeaseId++;
            }

            _lastUpdateFrame = -1;
            _unexpectedDestroy = false;
            _replacements.Clear();
            _implicitReplacements.Clear();
            _setupProxies.Clear();
            foreach (var state in _bones.Values)
            {
                state.UsedInPipeline = false;
            }
            _firstRendererStages.Clear();
        }

        internal void SealPipeline()
        {
            ThrowIfDisposed();
            _toRemove.Clear();
            foreach (var pair in _bones)
            {
                if (!pair.Value.UsedInPipeline) _toRemove.Add(pair.Key);
            }

            foreach (var original in _toRemove)
            {
                if (_bones.TryGetValue(original, out var state) && state.Proxy != null)
                {
                    Object.DestroyImmediate(state.Proxy.gameObject);
                }

                _bones.Remove(original);
            }

            if (_toRemove.Count > 0) _transferDataDirty = true;
            _toRemove.Clear();
            foreach (var stage in _replacements.Values)
            {
                foreach (var state in stage.Values)
                {
                    ApplyImplicitReplacementsToUnreplacedTransforms(state);
                }
            }

            ApplyImplicitReplacements(_setupProxies);
            _setupProxies.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            unchecked
            {
                LeaseId++;
            }

            _disposed = true;
            DisposeTransferData();

            foreach (var state in _bones.Values)
            {
                if (state.Proxy != null) Object.DestroyImmediate(state.Proxy.gameObject);
            }

            _bones.Clear();
            _stateList.Clear();
            _replacements.Clear();
            _implicitReplacements.Clear();
            _setupProxies.Clear();
            _firstRendererStages.Clear();
            _transferDataDirty = true;
        }

        internal void UpdateForTesting()
        {
            ThrowIfDisposed();
            UpdateAllBones();
        }

        private Transform GetBoneInternal(Transform source)
        {
            if (source == null) return null;
            if (_bones.TryGetValue(source, out var existing))
            {
                if (existing.Proxy == null)
                {
                    _unexpectedDestroy = true;
                    _bones.Remove(source);
                }
                else
                {
                    // Set the initial pose
                    if (!existing.UsedInPipeline) CopyState(existing);
                    existing.UsedInPipeline = true;
                    CheckParent(source.parent, existing);
                    RegisterImplicitReplacement(source, existing.Proxy);
                    return existing.Proxy;
                }
            }

            var proxyObject = new GameObject(source.name);
            SceneManager.MoveGameObjectToScene(proxyObject, NDMFPreviewSceneManager.GetPreviewScene());
            proxyObject.AddComponent<SelfDestructComponent>().KeepAlive = this;
            proxyObject.hideFlags = HideFlags.DontSave;

            var state = new BoneState
            {
                Original = source,
                Proxy = proxyObject.transform,
                UsedInPipeline = true
            };
            _bones.Add(source, state);
            _transferDataDirty = true;

            CopyState(state);
            CheckParent(source.parent, state);
            RegisterImplicitReplacement(source, state.Proxy);
            return state.Proxy;
        }

        private void RegisterImplicitReplacement(Transform source, Transform proxy)
        {
            _implicitReplacements[source] = proxy;
            RegisterReplacement(source, proxy);
            ApplyImplicitReplacements(_setupProxies);
        }

        private static void RegisterReplacement(Transform source, Transform proxy)
        {
            ObjectRegistry.TryRegisterReplacedObject(ObjectRegistry.GetReference(source.gameObject), proxy.gameObject);
        }

        private void UpdateAllBones()
        {
            using (UpdateMarker.Auto())
            {
                _toRemove.Clear();
                _stateList.Clear();
                _stateList.AddRange(_bones.Values);

                foreach (var entry in _stateList)
                {
                    if (entry.Original == null || entry.Proxy == null)
                    {
                        if (entry.Original != null)
                        {
                            _unexpectedDestroy = true;
                        }
                        else if (entry.Proxy != null)
                        {
                            Object.DestroyImmediate(entry.Proxy.gameObject);
                        }
                        _toRemove.Add(entry.Original);
                        continue;
                    }

                    CheckParent(entry.Original.parent, entry);
                }

                foreach (var remove in _toRemove) _bones.Remove(remove);
                if (_toRemove.Count > 0) _transferDataDirty = true;

                TransferBoneStates();
            }
        }

        private void CheckParent(Transform parent, BoneState entry)
        {
            // Note that we look up in _bones directly to avoid revisiting the same parent multiple times
            BoneState? parentState = null;
            if (parent != null) _bones.TryGetValue(parent, out parentState);
            // If UsedInPipeline isn't set, we need to recurse to set it for all parents
            if (parent == entry.ParentHint?.Original && parentState?.UsedInPipeline != false) return;

            // We now need to potentially create a new bone entry, so call the real GetBoneInterrnal
            var parentProxy = GetBoneInternal(parent);

            entry.ParentHint = parentProxy != null ? _bones[parent] : null;
            entry.Proxy.SetParent(parentProxy, false);
        }

        private static void CopyState(BoneState entry)
        {
            var source = entry.Original;
            entry.Proxy.localPosition = source.localPosition;
            entry.Proxy.localRotation = source.localRotation;
            entry.Proxy.localScale = source.localScale;
        }

        private void ReplayStage(int stageIndex, IReadOnlyDictionary<Renderer, Renderer> activeProxies)
        {
            if (!_replacements.TryGetValue(stageIndex, out var renderers)) return;

            _renderersToRemove.Clear();
            foreach (var pair in renderers)
            {
                if (!activeProxies.TryGetValue(pair.Key, out var proxy) || proxy == null)
                {
                    _renderersToRemove.Add(pair.Key);
                    continue;
                }

                ApplyRendererState(proxy, pair.Value);
            }

            foreach (var renderer in _renderersToRemove) renderers.Remove(renderer);
        }

        private void ApplyImplicitReplacements(IReadOnlyDictionary<Renderer, Renderer> proxies)
        {
            if (proxies == null) return;
            foreach (var proxy in proxies.Values)
            {
                ApplyImplicitReplacements(proxy);
            }
        }

        /// <summary>
        ///     During processing of one filter node, another may be processing a different render group.
        ///     However, GetBone when called from one must eventually apply to both groups' proxies.
        ///     To avoid interference during construction, we defer this replacement until we seal the pipeline.
        /// </summary>
        /// <param name="state"></param>
        private void ApplyImplicitReplacementsToUnreplacedTransforms(RendererReplacementState state)
        {
            for (var i = 0; i < state.OriginalBones.Length; i++)
            {
                var original = state.OriginalBones[i];
                if (original == null || state.Mappings.ContainsKey(original)) continue;
                if (_implicitReplacements.TryGetValue(original, out var replacement))
                {
                    state.Bones[i] = replacement;
                }
            }

            if (state.OriginalRootBone != null
                && !state.Mappings.ContainsKey(state.OriginalRootBone)
                && _implicitReplacements.TryGetValue(state.OriginalRootBone, out var rootBone))
            {
                state.RootBone = rootBone;
            }

            if (state.OriginalProbeAnchor != null
                && !state.Mappings.ContainsKey(state.OriginalProbeAnchor)
                && _implicitReplacements.TryGetValue(state.OriginalProbeAnchor, out var probeAnchor))
            {
                state.ProbeAnchor = probeAnchor;
            }

            if (state.OriginalParent != null
                && !state.Mappings.ContainsKey(state.OriginalParent)
                && _implicitReplacements.TryGetValue(state.OriginalParent, out var parent))
            {
                state.Parent = parent;
            }
        }

        private void ApplyImplicitReplacements(Renderer renderer)
        {
            if (renderer == null || _implicitReplacements.Count == 0) return;

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                var bones = skinnedRenderer.bones;
                var bonesChanged = false;
                for (var i = 0; i < bones.Length; i++)
                {
                    if (bones[i] == null || !_implicitReplacements.TryGetValue(bones[i], out var replacement)) continue;
                    bones[i] = replacement;
                    bonesChanged = true;
                }

                if (bonesChanged) skinnedRenderer.bones = bones;

                if (skinnedRenderer.rootBone != null
                    && _implicitReplacements.TryGetValue(skinnedRenderer.rootBone, out var rootBone))
                {
                    skinnedRenderer.rootBone = rootBone;
                }
            }

            if (renderer.probeAnchor != null
                && _implicitReplacements.TryGetValue(renderer.probeAnchor, out var probeAnchor))
            {
                renderer.probeAnchor = probeAnchor;
            }

            if (renderer.transform.parent != null
                && _implicitReplacements.TryGetValue(renderer.transform.parent, out var parent)
                && renderer.transform.parent != parent)
            {
                renderer.transform.SetParent(parent, false);
            }
        }

        private static void ReplaceReferences(
            RendererReplacementState state,
            Transform original,
            Transform replacement
        )
        {
            for (var i = 0; i < state.Bones.Length; i++)
            {
                if (state.OriginalBones[i] == original) state.Bones[i] = replacement;
            }

            if (state.OriginalRootBone == original) state.RootBone = replacement;
            if (state.OriginalProbeAnchor == original) state.ProbeAnchor = replacement;
            if (state.OriginalParent == original) state.Parent = replacement;
        }

        private static void ApplyRendererState(Renderer renderer, RendererReplacementState state)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                skinnedRenderer.bones = state.Bones;
                skinnedRenderer.rootBone = state.RootBone;
            }

            renderer.probeAnchor = state.ProbeAnchor;
            if (renderer.transform.parent != state.Parent) renderer.transform.SetParent(state.Parent, false);
        }

        private void TransferBoneStates()
        {
            if (_transferDataDirty) RebuildTransferData();
            if (!_sourceBones.isCreated || _sourceBones.length == 0) return;

            JobHandle readTransforms;
            using (ReadScheduleMarker.Auto())
            {
                readTransforms = new ReadTransformsJob
                {
                    BoneStates = _boneStates,
                    BoneIsValid = _boneIsValid
                }.Schedule(_sourceBones);
            }

            JobHandle writeTransforms;
            using (WriteScheduleMarker.Auto())
            {
                writeTransforms = new WriteBoneStatesJob
                {
                    BoneStates = _boneStates,
                    BoneIsValid = _boneIsValid
                }.Schedule(_proxyBones, readTransforms);
            }

            using (CompleteMarker.Auto())
            {
                writeTransforms.Complete();
            }
        }

        private void RebuildTransferData()
        {
            DisposeTransferData();
            if (_bones.Count == 0)
            {
                _transferDataDirty = false;
                return;
            }

            var sourceBones = new Transform[_bones.Count];
            var proxyBones = new Transform[_bones.Count];
            var index = 0;
            foreach (var entry in _bones.Values)
            {
                sourceBones[index] = entry.Original;
                proxyBones[index] = entry.Proxy;
                index++;
            }

            _sourceBones = new TransformAccessArray(sourceBones);
            _proxyBones = new TransformAccessArray(proxyBones);
            _boneIsValid = new NativeArray<bool>(sourceBones.Length, Allocator.Persistent);
            _boneStates = new NativeArray<TransformState>(sourceBones.Length, Allocator.Persistent);
            _transferDataDirty = false;
        }

        private void DisposeTransferData()
        {
            if (_sourceBones.isCreated) _sourceBones.Dispose();
            if (_proxyBones.isCreated) _proxyBones.Dispose();
            if (_boneIsValid.IsCreated) _boneIsValid.Dispose();
            if (_boneStates.IsCreated) _boneStates.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShadowBoneManager));
        }

        private struct TransformState
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        [BurstCompile]
        private struct ReadTransformsJob : IJobParallelForTransform
        {
            [WriteOnly] public NativeArray<TransformState> BoneStates;
            [WriteOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                BoneIsValid[index] = transform.isValid;
                if (!transform.isValid) return;
                BoneStates[index] = new TransformState
                {
                    LocalPosition = transform.localPosition,
                    LocalRotation = transform.localRotation,
                    LocalScale = transform.localScale
                };
            }
        }

        [BurstCompile]
        private struct WriteBoneStatesJob : IJobParallelForTransform
        {
            private const float Epsilon = 0.00001f;
            private const float SqrEpsilon = Epsilon * Epsilon;

            [ReadOnly] public NativeArray<TransformState> BoneStates;
            [ReadOnly] public NativeArray<bool> BoneIsValid;

            public void Execute(int index, TransformAccess transform)
            {
                if (!BoneIsValid[index]) return;
                var state = BoneStates[index];
                if (Vector3.SqrMagnitude(transform.localPosition - state.LocalPosition) <= SqrEpsilon
                    && !RotationsDiffer(transform.localRotation, state.LocalRotation)
                    && Vector3.SqrMagnitude(transform.localScale - state.LocalScale) <= SqrEpsilon) return;

                transform.localPosition = state.LocalPosition;
                transform.localRotation = state.LocalRotation;
                transform.localScale = state.LocalScale;
            }

            private static bool RotationsDiffer(Quaternion lhs, Quaternion rhs)
            {
                var sameX = lhs.x - rhs.x;
                var sameY = lhs.y - rhs.y;
                var sameZ = lhs.z - rhs.z;
                var sameW = lhs.w - rhs.w;
                var sameDistanceSquared = sameX * sameX + sameY * sameY + sameZ * sameZ + sameW * sameW;

                var negatedX = lhs.x + rhs.x;
                var negatedY = lhs.y + rhs.y;
                var negatedZ = lhs.z + rhs.z;
                var negatedW = lhs.w + rhs.w;
                var negatedDistanceSquared = negatedX * negatedX + negatedY * negatedY + negatedZ * negatedZ +
                                             negatedW * negatedW;

                return Mathf.Min(sameDistanceSquared, negatedDistanceSquared) > 2 * SqrEpsilon;
            }
        }
    }
}
