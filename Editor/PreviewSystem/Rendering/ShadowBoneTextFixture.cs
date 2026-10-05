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

        /// <summary>
        ///     Gets the shadow-bone manager handle for use by the preview context.
        /// </summary>
        public IShadowBoneManagerHandle Handle { get; }

        /// <summary>
        ///     Initializes a shadow-bone manager for preview tests.
        /// </summary>
        public ShadowBoneTextFixture()
        {
            _manager = new ShadowBoneManager();
            _manager.ResetPipeline();
            Handle = _manager.Handle;
        }

        /// <summary>
        ///     Runs one synchronous operation in a fresh stage emulating a render-filter instantation
        ///     task and commits its shadow-bone replacements.
        /// </summary>
        public T ExecuteInStageSync<T>(
            IReadOnlyDictionary<Renderer, Renderer> rendererMap,
            Func<T> operation
        ) where T : class
        {
            if (rendererMap == null) throw new ArgumentNullException(nameof(rendererMap));
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            using var stageScope = _manager.EnterStage(0, rendererMap);
            var result = operation();
            if (result != null) stageScope.Commit();
            return result;
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
            _manager.Dispose();
        }
    }
}