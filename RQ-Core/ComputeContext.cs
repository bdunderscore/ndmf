#region

using System;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    /// Tracks dependencies around a single computation. Generally, this object should be retained as long as we need to
    /// receive invalidation events (GCing this object may deregister invalidation events).
    /// </summary>
    public sealed class ComputeContext
    {
        private TaskCompletionSource<object> _invalidater = new();

        /// <summary>
        /// An Action which can be used to invalidate this compute context (possibly triggering a recompute).
        /// </summary>
        internal Action Invalidate { get; }

        /// <summary>
        /// A Task which completes when this compute context is invalidated. Note that completing this task does not
        /// guarantee that the underlying computation (e.g. ReactiveValue) is going to be recomputed.
        /// </summary>
        internal Task OnInvalidate { get; }

        public bool IsInvalidated => OnInvalidate.IsCompleted;

        internal ComputeContext()
        {
            Invalidate = () => _invalidater.TrySetResult(null);
            OnInvalidate = _invalidater.Task;
        }

        /// <summary>
        ///     Invalidate the `other` compute context when this compute context is invalidated.
        /// </summary>
        /// <param name="other"></param>
        internal void Invalidates(ComputeContext other)
        {
            OnInvalidate.ContinueWith(_ => other.Invalidate());
        }
    }
}