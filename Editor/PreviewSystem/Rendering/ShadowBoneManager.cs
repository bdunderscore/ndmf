#region

using System;
using System.Collections.Generic;
using System.Linq;
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
            internal readonly bool[] ExplicitBones;
            internal Transform RootBone, OriginalRootBone;
            internal bool ExplicitRootBone;
            internal Transform ProbeAnchor, OriginalProbeAnchor;
            internal bool ExplicitProbeAnchor;
            internal Transform Parent, OriginalParent;
            internal bool ExplicitParent;

            internal RendererReplacementState(Renderer renderer)
            {
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    var bones = skinnedRenderer.bones;
                    OriginalBones = (Transform[])bones.Clone();
                    Bones = (Transform[])OriginalBones.Clone();
                    ExplicitBones = new bool[Bones.Length];
                    OriginalRootBone = RootBone = skinnedRenderer.rootBone;
                }
                else
                {
                    OriginalBones = Bones = Array.Empty<Transform>();
                    ExplicitBones = Array.Empty<bool>();
                }

                OriginalProbeAnchor = ProbeAnchor = renderer.probeAnchor;
                OriginalParent = Parent = renderer.transform.parent;
            }

            internal RendererReplacementState(RendererReplacementState source)
            {
                OriginalBones = source.OriginalBones;
                Bones = (Transform[])source.Bones.Clone();
                ExplicitBones = (bool[])source.ExplicitBones.Clone();
                OriginalParent = source.OriginalParent;
                Parent = source.Parent;
                ExplicitParent = source.ExplicitParent;
                OriginalProbeAnchor = source.OriginalProbeAnchor;
                ExplicitProbeAnchor = source.ExplicitProbeAnchor;
                OriginalRootBone = source.OriginalRootBone;
                RootBone = source.RootBone;
                ExplicitRootBone = source.ExplicitRootBone;
                ProbeAnchor = source.ProbeAnchor;
                Mappings = new Dictionary<Transform, Transform>(source.Mappings);
            }

            public bool IsUnchanged =>
                Bones.SequenceEqual(OriginalBones)
                && RootBone == OriginalRootBone
                && ProbeAnchor == OriginalProbeAnchor
                && Parent == OriginalParent
                && Mappings.Count == 0;

            public bool StateEquals(RendererReplacementState other)
            {
                return Bones.SequenceEqual(other.Bones)
                       && ExplicitBones.SequenceEqual(other.ExplicitBones)
                       && RootBone == other.RootBone
                       && ExplicitRootBone == other.ExplicitRootBone
                       && ProbeAnchor == other.ProbeAnchor
                       && ExplicitProbeAnchor == other.ExplicitProbeAnchor
                       && Parent == other.Parent
                       && ExplicitParent == other.ExplicitParent
                       && Mappings.Count == other.Mappings.Count
                       && Mappings.All(pair =>
                           other.Mappings.TryGetValue(pair.Key, out var value) && value == pair.Value);
            }

            public void ApplyReplacements(Dictionary<Transform, Transform> implicitReplacements)
            {
                for (var i = 0; i < Bones.Length; i++)
                {
                    if (ExplicitBones[i]) continue;
                    var tmp = Bones[i];
                    ReplaceOne(ref tmp);
                    Bones[i] = tmp;
                }

                if (!ExplicitRootBone) ReplaceOne(ref RootBone);
                if (!ExplicitProbeAnchor) ReplaceOne(ref ProbeAnchor);
                if (!ExplicitParent) ReplaceOne(ref Parent);

                void ReplaceOne(ref Transform bone)
                {
                    if (bone != null && implicitReplacements.TryGetValue(bone, out var replacement))
                    {
                        bone = replacement;
                    }
                }
            }

            public void PreserveExplicitFlags(RendererReplacementState previous)
            {
                var length = Math.Min(Bones.Length, previous.Bones.Length);
                for (var i = 0; i < length; i++)
                {
                    ExplicitBones[i] = previous.ExplicitBones[i] && Bones[i] == previous.Bones[i];
                }

                ExplicitRootBone = previous.ExplicitRootBone && RootBone == previous.RootBone;
                ExplicitProbeAnchor = previous.ExplicitProbeAnchor && ProbeAnchor == previous.ProbeAnchor;
                ExplicitParent = previous.ExplicitParent && Parent == previous.Parent;
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
            private readonly Dictionary<Renderer, RendererReplacementState> _previousLatestStates = new();
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
                if (_committed)
                {
                    CaptureFinalState();
                }
                else
                {
                    Rollback();
                }
                
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

                if (!_stateProxies.ContainsKey(r))
                {
                    throw new InvalidOperationException("Renderer " + r.gameObject.name +
                                                        " is not registered for this stage");
                }

                if (_pendingState.TryGetValue(r, out var state)) return state;

                // We initialize from the setup proxy so we can capture any non-SBM manipulation
                // happening.
                var setupProxy = _manager._setupProxies[r];
                _manager._latestStates.TryGetValue(r, out var previousLatest);
                _previousLatestStates[r] = previousLatest;

                state = new RendererReplacementState(setupProxy);
                if (previousLatest != null) state.PreserveExplicitFlags(previousLatest);
                _rollbackState[setupProxy] = new RendererReplacementState(state);
                _pendingState[r] = state;
                _manager._latestStates[r] = state;

                return state;
            }

            private void CaptureFinalState()
            {
                var stageState = _manager._replacements[_stage];
                foreach (var (renderer, proxy) in _stateProxies)
                {
                    var captured = new RendererReplacementState(proxy);
                    RendererReplacementState previous = null;
                    if (_pendingState.TryGetValue(renderer, out var pending))
                    {
                        previous = pending;
                    }
                    else if (stageState.TryGetValue(renderer, out var previousStageState))
                    {
                        previous = previousStageState;
                    }
                    else if (_manager._latestStates.TryGetValue(renderer, out var previousLatest))
                    {
                        previous = previousLatest;
                    }

                    if (previous != null)
                    {
                        foreach (var (original, replacement) in previous.Mappings)
                        {
                            captured.Mappings[original] = replacement;
                        }

                        captured.PreserveExplicitFlags(previous);
                    }

                    stageState[renderer] = captured;
                    _manager._latestStates[renderer] = captured;
                }
            }


            private void Rollback()
            {
                foreach (var (proxy, state) in _rollbackState)
                {
                    ApplyRendererState(proxy, state);
                }

                foreach (var (renderer, previous) in _previousLatestStates)
                {
                    if (previous != null)
                    {
                        _manager._latestStates[renderer] = previous;
                    }
                    else
                    {
                        _manager._latestStates.Remove(renderer);
                    }
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
        private readonly Dictionary<Renderer, RendererReplacementState> _latestStates = new();

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

            _implicitReplacements.TryGetValue(original, out var implicitReplacement);
            state.Mappings.TryGetValue(original, out var previousReplacement);
            ReplaceReferences(state, original, implicitReplacement, previousReplacement, replacement);

            state.Mappings[original] = replacement;
            ApplyRendererState(proxy, state);
        }

        internal StageScope EnterStage(
            int stageIndex,
            IReadOnlyDictionary<Renderer, Renderer> proxies
        )
        {
            ThrowIfDisposed();

            // Ensure stage 0 has all proxies registered
            if (!_replacements.TryGetValue(0, out var stageZero))
            {
                stageZero = new Dictionary<Renderer, RendererReplacementState>();
                _replacements[0] = stageZero;
            }

            foreach (var (renderer, proxy) in proxies)
            {
                if (!stageZero.ContainsKey(renderer))
                {
                    SetInitialParent(renderer, proxy);
                    stageZero[renderer] = new RendererReplacementState(proxy);
                }
            }
            
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
            _latestStates.Clear();
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

            // Prune any redundant replacements
            Dictionary<Renderer, (int, RendererReplacementState)> previousState = new();
            foreach (var (stage, entries) in _replacements.OrderBy(pair => pair.Key))
            {
                foreach (var (renderer, state) in entries.ToList())
                {
                    state.ApplyReplacements(_implicitReplacements);

                    if (previousState.TryGetValue(renderer, out var pair) && pair.Item2.StateEquals(state))
                    {
                        entries.Remove(renderer);
                    }

                    previousState[renderer] = (stage, state);
                }
            }

            // If we never manipulated the renderer, prune it entirely
            foreach (var (renderer, (stage, state)) in previousState)
            {
                if (stage == 0 && state.IsUnchanged)
                {
                    _replacements[0].Remove(renderer);
                }

                ApplyRendererState(_setupProxies[renderer], state);
            }
            
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
            _latestStates.Clear();
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
            foreach (var (original, proxy) in proxies)
            {
                ApplyImplicitReplacements(original, proxy);
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
                Replace(state.OriginalBones[i], state.ExplicitBones[i], ref state.Bones[i]);
            }

            Replace(state.OriginalRootBone, state.ExplicitRootBone, ref state.RootBone);
            Replace(state.OriginalProbeAnchor, state.ExplicitProbeAnchor, ref state.ProbeAnchor);
            Replace(state.OriginalParent, state.ExplicitParent, ref state.Parent);

            void Replace(Transform original, bool isExplicit, ref Transform target)
            {
                if (original != null && !isExplicit
                                     && _implicitReplacements.TryGetValue(original, out var replacement))
                {
                    target = replacement;
                }
            }
        }

        private void ApplyImplicitReplacements(Renderer original, Renderer renderer)
        {
            if (renderer == null || _implicitReplacements.Count == 0) return;
            _latestStates.TryGetValue(original, out var state);

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                var bones = skinnedRenderer.bones;
                var bonesChanged = false;
                for (var i = 0; i < bones.Length; i++)
                {
                    var isExplicit = state != null && state.ExplicitBones.Length > i && state.ExplicitBones[i];
                    var explicitValue = isExplicit ? state.Bones[i] : null;
                    if (!TryGetReplacement(bones[i], isExplicit, explicitValue, out var replacement)) continue;
                    bones[i] = replacement;
                    bonesChanged = true;
                }

                if (bonesChanged) skinnedRenderer.bones = bones;

                if (TryGetReplacement(skinnedRenderer.rootBone, state?.ExplicitRootBone ?? false,
                        state?.RootBone, out var rootBone))
                {
                    skinnedRenderer.rootBone = rootBone;
                }
            }

            if (TryGetReplacement(renderer.probeAnchor, state?.ExplicitProbeAnchor ?? false,
                    state?.ProbeAnchor, out var probeAnchor))
            {
                renderer.probeAnchor = probeAnchor;
            }

            if (TryGetReplacement(renderer.transform.parent, state?.ExplicitParent ?? false,
                    state?.Parent, out var parent) && renderer.transform.parent != parent)
            {
                renderer.transform.SetParent(parent, false);
            }

            bool TryGetReplacement(Transform current, bool isExplicit, Transform explicitValue,
                out Transform replacement)
            {
                replacement = null;
                return current != null
                       && !(isExplicit && explicitValue == current)
                       && _implicitReplacements.TryGetValue(current, out replacement);
            }
        }


        private static void ReplaceReferences(
            RendererReplacementState state,
            Transform original,
            Transform implicitReplacement,
            Transform previousReplacement,
            Transform replacement
        )
        {
            for (var i = 0; i < state.Bones.Length; i++)
            {
                if (Matches(state.OriginalBones[i], state.Bones[i], state.ExplicitBones[i]))
                {
                    state.Bones[i] = replacement;
                    state.ExplicitBones[i] = true;
                }
            }

            Replace(ref state.RootBone, ref state.ExplicitRootBone, state.OriginalRootBone);
            Replace(ref state.ProbeAnchor, ref state.ExplicitProbeAnchor, state.OriginalProbeAnchor);
            Replace(ref state.Parent, ref state.ExplicitParent, state.OriginalParent);

            void Replace(ref Transform current, ref bool isExplicit, Transform actualOriginal)
            {
                if (!Matches(actualOriginal, current, isExplicit)) return;
                current = replacement;
                isExplicit = true;
            }

            bool Matches(Transform actualOriginal, Transform current, bool isExplicit)
            {
                if (actualOriginal != original
                    && (implicitReplacement == null || actualOriginal != implicitReplacement)) return false;
                if (isExplicit) return previousReplacement != null && current == previousReplacement;
                return current == original || (implicitReplacement != null && current == implicitReplacement);
            }
        }

        private static void ApplyRendererState(Renderer renderer, RendererReplacementState state)
        {
            if (renderer == null) return;
            
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
