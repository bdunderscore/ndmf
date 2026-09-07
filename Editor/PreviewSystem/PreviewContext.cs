#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     Provides pipeline-scoped preview services to render filters while a preview pipeline is executing.
    /// </summary>
    [PublicAPI]
    public sealed class PreviewContext
    {
        private static readonly AsyncLocal<PreviewContext?> CurrentContext = new();

        private IShadowBoneManagerHandle? _shadowBoneManager;
        private ShadowBoneManager? _leasedShadowBoneManager;
        private long _shadowBoneManagerLeaseId;
        private bool _sealed;
        private readonly AsyncLocal<AccessTrackingScope> _shadowBoneManagerAccessScope = new();

        /// <summary>
        ///     Creates a mutable preview context for an externally controlled preview scope.
        /// </summary>
        public PreviewContext()
        {
        }

        /// <summary>
        ///     Gets the context active in the current asynchronous task scope.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when no preview context is active in the current asynchronous task scope.
        /// </exception>
        public static PreviewContext Instance => CurrentContext.Value ?? throw new InvalidOperationException(
            "PreviewContext is only available while executing a preview pipeline.");

        /// <summary>
        ///     Gets or sets the shadow-bone manager handle available to this context.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the handle has not been configured, or when setting a sealed context.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when assigning <c>null</c>.</exception>
        public IShadowBoneManagerHandle ShadowBoneManager
        {
            get
            {
                var manager = _shadowBoneManager ?? throw new InvalidOperationException(
                    "ShadowBoneManager has not been configured for this PreviewContext.");
                _shadowBoneManagerAccessScope.Value?.RecordAccess();
                return manager;
            }
            set
            {
                if (_sealed)
                {
                    throw new InvalidOperationException("This PreviewContext is sealed and cannot be modified.");
                }

                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _shadowBoneManager = value;
                if (value is ShadowBoneManager manager)
                {
                    _leasedShadowBoneManager = manager;
                    _shadowBoneManagerLeaseId = manager.LeaseId;
                }
                else
                {
                    _leasedShadowBoneManager = null;
                    _shadowBoneManagerLeaseId = default;
                }
            }
        }

        /// <summary>
        ///     Makes this context current until the returned scope is disposed.
        /// </summary>
        /// <returns>A scope which restores the previously active context when disposed.</returns>
        public IDisposable Activate()
        {
            var previous = CurrentContext.Value;
            CurrentContext.Value = this;
            return new ActivationScope(previous);
        }

        /// <summary>
        ///     Prevents subsequent mutation of fields owned by a proxy pipeline.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the shadow-bone manager has not been configured.</exception>
        internal void Seal()
        {
            _ = ShadowBoneManager;
            _sealed = true;
        }

        internal void VerifyShadowBoneManagerLease(ShadowBoneManager manager)
        {
            if (!ReferenceEquals(_leasedShadowBoneManager, manager)
                || _shadowBoneManagerLeaseId != manager.LeaseId)
            {
                throw new InvalidOperationException(
                    "The ShadowBoneManager handle does not belong to the active PreviewContext lease.");
            }
        }

        internal AccessTrackingScope TrackAccess()
        {
            var scope = new AccessTrackingScope(this, _shadowBoneManagerAccessScope.Value);
            _shadowBoneManagerAccessScope.Value = scope;
            return scope;
        }

        internal ShadowBoneManager.StageScope EnterShadowBoneStage(
            int stageIndex,
            IReadOnlyDictionary<Renderer, Renderer> proxies
        )
        {
            var manager = _leasedShadowBoneManager ?? throw new InvalidOperationException(
                "This PreviewContext does not have an active ShadowBoneManager lease.");
            VerifyShadowBoneManagerLease(manager);
            return manager.EnterStage(stageIndex, proxies);
        }

        internal sealed class AccessTrackingScope : IDisposable
        {
            private readonly PreviewContext _context;
            private readonly AccessTrackingScope _previous;
            private bool _disposed;

            internal bool ShadowBoneManagerWasAccessed { get; private set; }

            internal AccessTrackingScope(PreviewContext context, AccessTrackingScope previous)
            {
                _context = context;
                _previous = previous;
            }

            internal void RecordAccess()
            {
                ShadowBoneManagerWasAccessed = true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (ReferenceEquals(_context._shadowBoneManagerAccessScope.Value, this))
                {
                    _context._shadowBoneManagerAccessScope.Value = _previous;
                }
            }
        }

        private sealed class ActivationScope : IDisposable
        {
            private readonly PreviewContext? _previous;
            private bool _disposed;

            internal ActivationScope(PreviewContext? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed) return;

                _disposed = true;
                CurrentContext.Value = _previous;
            }
        }
    }

    /// <summary>
    ///     Allows the current preview stage to replace a bone transform with one that can be manipulated
    ///     or overridden as part of preview processing.
    /// </summary>
    [PublicAPI]
    public interface IShadowBoneManagerHandle
    {
        /// <summary>
        ///     Returns a shadow transform whose pose is synchronized with <paramref name="bone" /> at preview frame start.
        ///     Requesting a non-null transform also replaces exact matching references in renderer proxies with that
        ///     shadow, including skinned bones and root bones, probe anchors, and proxy parents.
        /// </summary>
        /// <param name="bone">The source transform to shadow.</param>
        /// <returns>The corresponding shadow transform, or <c>null</c> when <paramref name="bone" /> is <c>null</c>.</returns>
        Transform GetBone(Transform bone);

        /// <summary>
        ///     Records an exact replacement of <paramref name="original" /> with <paramref name="replacement" /> for
        ///     <paramref name="originalRenderer" />. The replacement affects matching entries in a skinned renderer's
        ///     bones and root bone, a renderer's probe anchor, and reparents a proxy for a renderer on the original
        ///     to the replacement bone. It does not replace descendant references or cascade through prior replacements.
        ///     This method may only be called from <c>IRenderFilter.Instantiate</c> or <c>IRenderFilterNode.Refresh</c>.
        /// </summary>
        /// <param name="originalRenderer">The source renderer whose proxy references should be replaced.</param>
        /// <param name="original">The exact source transform reference to replace.</param>
        /// <param name="replacement">The transform to use in place of <paramref name="original" />.</param>
        void ReplaceBone(Renderer originalRenderer, Transform original, Transform replacement);
    }
}