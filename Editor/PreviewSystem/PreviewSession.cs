#region

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    public delegate ImmutableHashSet<Renderer> HiddenRenderersDelegate(ComputeContext ctx);
    
    /// <summary>
    /// The PreviewSession class allows you to override preview behavior for a particular camera.
    /// This in particularly allows you to display only specific renderers, or apply additional
    /// transformations to the preview.
    /// </summary>
    public class PreviewSession : IDisposable
    {
        #region Static State

        /// <summary>
        /// The PreviewSession used for any cameras not overriden using `OverrideCamera`.
        /// </summary>
        public static PreviewSession? Current { get; set; }

        private static readonly Dictionary<Camera, PreviewSession> _cameraOverrides = new();

        internal static PreviewSession? ForCamera(Camera camera)
        {
            return _cameraOverrides.GetValueOrDefault(camera) ?? Current;
        }
        
        /// <summary>
        /// Applies this PreviewSession to the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public void OverrideCamera(Camera target)
        {
            _cameraOverrides[target] = this;
        }

        /// <summary>
        /// Removes all camera overrides from the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public static void ClearCameraOverride(Camera target)
        {
            _cameraOverrides.Remove(target);
        }
        
        #endregion

        internal IEnumerable<(Renderer, Renderer?)> OnPreCull(bool isSceneCamera)
        {
            return _proxySession?.OnPreCull(isSceneCamera) ?? Enumerable.Empty<(Renderer, Renderer?)>();
        }
        
        internal ImmutableDictionary<Renderer, Renderer> OriginalToProxyRenderer =>
            _proxySession?.OriginalToProxyRenderer ?? ImmutableDictionary<Renderer, Renderer>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> OriginalToProxyObject =>
            _proxySession?.OriginalToProxyObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;

        internal ImmutableDictionary<GameObject, GameObject> ProxyToOriginalObject =>
            _proxySession?.ProxyToOriginalObject ?? ImmutableDictionary<GameObject, GameObject>.Empty;
        
        private readonly Sequencer _sequence = new Sequencer();

        private Dictionary<SequencePoint, IRenderFilter> _filters = new();

        private ProxySession _proxySession;

        [UsedImplicitly] // primarily for debugger usage
        private readonly string _name;

        private HiddenRenderersDelegate? _hiddenRenderers;

        /// <summary>
        /// This delegate is invoked to obtain a list of renderers to hide in cameras bound to this session.
        /// </summary>
        public HiddenRenderersDelegate? HiddenRenderers
        {
            get => _hiddenRenderers;
            set
            {
                _hiddenRenderers = value;
                RebuildSequence();
            }
        }

        internal GameObject GetOriginalObjectForProxy(GameObject proxy)
        {
            return _proxySession?.GetOriginalObjectForProxy(proxy);
        }

        public PreviewSession()
        {
            _proxySession = new ProxySession(ImmutableList<IRenderFilter>.Empty);
            _name = "Default";
        }

        private PreviewSession(PreviewSession source, string name)
        {
            _proxySession = new ProxySession(ImmutableList<IRenderFilter>.Empty);
            _sequence = source._sequence.Clone();
            _filters = source._filters.ToDictionary(kv => kv.Key, kv => kv.Value);
            _name = name;
            ForceRebuild();
        }

        public void ForceRebuild()
        {
            _proxySession.Dispose();
            _proxySession = new ProxySession(ImmutableList<IRenderFilter>.Empty);

            RebuildSequence();
        }

        /// <summary>
        /// Sets the order in which mesh mutations are executed. Any sequence points not listed in this sequence will
        /// be executed after these registered points, in `AddMutator` invocation order.
        /// </summary>
        /// <param name="sequencePoints"></param>
        public void SetSequence(IEnumerable<SequencePoint> sequencePoints)
        {
            _sequence.SetSequence(sequencePoints);

            RebuildSequence();
        }

        public IDisposable AddMutator(SequencePoint sequencePoint, IRenderFilter filter)
        {
            _sequence.AddPoint(sequencePoint);

            _filters.Add(sequencePoint, filter);

            RebuildSequence();

            return new RemovalDisposable(this, sequencePoint);
        }

        private class RemovalDisposable : IDisposable
        {
            private PreviewSession _session;
            private SequencePoint _point;

            public RemovalDisposable(PreviewSession session, SequencePoint point)
            {
                _session = session;
                _point = point;
            }

            public void Dispose()
            {
                _session._filters.Remove(_point);
                _session.RebuildSequence();
            }
        }

        void RebuildSequence()
        {
            var sequence = _sequence.Sequence;
            var filters = sequence.Select(p => _filters.GetValueOrDefault(p)).Where(f => f != null).ToImmutableList();

            _proxySession.Filters = filters;
            _proxySession.HideRenderers = HiddenRenderers;
        }

        /// <summary>
        /// Returns a new PreviewSession which inherits all mutators from the parent session. Any mutators added to this
        /// new session run after the parent session's mutators.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PreviewSession Fork(string name = "Preview session")
        {
            return new PreviewSession(this, name);
        }
        
        public void Dispose()
        {
            _proxySession.Dispose();

            foreach (var (k, _) in _cameraOverrides.Where(kv => kv.Key == null || kv.Value == this).ToList())
            {
                _cameraOverrides.Remove(k);
            }
        }
    }
}