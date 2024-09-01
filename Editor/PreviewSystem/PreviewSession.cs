#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// TODO: Document
    ///
    /// (For now, this isn't very useful; use  `DeclaringPass.PreviewingWith` instead)
    /// </summary>
    internal class PreviewSession // : IDisposable
    {
        #region Static State

        /// <summary>
        /// The PreviewSession used for any cameras not overriden using `OverrideCamera`.
        /// </summary>
        public static PreviewSession Current { get; set; }

#if FUTURE_API
        /// <summary>
        /// Applies this PreviewSession to the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public void OverrideCamera(Camera target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all camera overrides from the `target` camera.
        /// </summary>
        /// <param name="target"></param>
        public static void ClearCameraOverride(Camera target)
        {
            throw new NotImplementedException();
        }
#endif

        #endregion

        internal IEnumerable<(Renderer, Renderer)> OnPreCull(bool isSceneCamera)
        {
            return _proxySession?.OnPreCull(isSceneCamera) ?? Enumerable.Empty<(Renderer, Renderer)>();
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
        
        public PreviewSession()
        {
            _proxySession = new ProxySession(ImmutableList<IRenderFilter>.Empty);
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
        }

#if FUTURE_API
        /// <summary>
        /// Returns a new PreviewSession which inherits all mutators from the parent session. Any mutators added to this
        /// new session run after the parent session's mutators.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public PreviewSession Fork()
        {
            throw new NotImplementedException();
        }
#endif
        
        public void Dispose()
        {
            _proxySession.Dispose();
        }
    }
}