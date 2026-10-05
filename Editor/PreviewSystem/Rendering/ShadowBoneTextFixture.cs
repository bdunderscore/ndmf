#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     Provides a real shadow-bone manager for preview tests.
    /// </summary>
    public sealed class ShadowBoneTextFixture : IDisposable
    {
        private readonly ShadowBoneManager _manager;
        private readonly IDisposable _stageScope;

        /// <summary>
        ///     Gets the shadow-bone manager handle for use by the preview context.
        /// </summary>
        public IShadowBoneManagerHandle Handle { get; }

        /// <summary>
        ///     Initializes a shadow-bone manager for stage zero using <paramref name="rendererMap" />.
        /// </summary>
        /// <param name="rendererMap">The mutable source-to-proxy renderer map for the test stage.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rendererMap" /> is <c>null</c>.</exception>
        public ShadowBoneTextFixture(IReadOnlyDictionary<Renderer, Renderer> rendererMap)
        {
            if (rendererMap == null) throw new ArgumentNullException(nameof(rendererMap));

            _manager = new ShadowBoneManager();
            _manager.ResetPipeline();
            _stageScope = _manager.EnterStage(0, rendererMap);
            Handle = _manager.Handle;
        }

        /// <summary>
        ///     Synchronizes all shadow-bone poses with their source transforms.
        /// </summary>
        public void SyncPoses()
        {
            _manager.UpdateForTesting();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _stageScope.Dispose();
            _manager.Dispose();
        }
    }
}