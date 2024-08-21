#region

using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     Tracks dependencies around a single computation. Generally, this object should be retained as long as we need to
    ///     receive invalidation events (GCing this object may deregister invalidation events).
    /// </summary>
    public sealed class ComputeContext
    {
        [PublicAPI] public static ComputeContext NullContext { get; }
        private readonly TaskCompletionSource<object> _invalidater = new();

        static ComputeContext()
        {
            NullContext = new ComputeContext("null", null);
        }

        internal string Description { get; }
        
        /// <summary>
        ///     An Action which can be used to invalidate this compute context (possibly triggering a recompute).
        /// </summary>
        internal Action Invalidate { get; }

        /// <summary>
        ///     A Task which completes when this compute context is invalidated. Note that completing this task does not
        ///     guarantee that the underlying computation (e.g. ReactiveValue) is going to be recomputed.
        /// </summary>
        internal Task OnInvalidate { get; }

        public bool IsInvalidated => OnInvalidate.IsCompleted;

        internal ComputeContext(string description)
        {
            Invalidate = () =>
            {
#if NDMF_TRACE
                Debug.Log("Invalidating " + Description);
#endif
                _invalidater.TrySetResult(null);
            };
            OnInvalidate = _invalidater.Task;
            Description = description;
        }

        private ComputeContext(string description, object nullToken)
        {
            Invalidate = () => { };
            OnInvalidate = Task.CompletedTask;
            Description = description;
        }

        /// <summary>
        ///     Invalidate the `other` compute context when this compute context is invalidated.
        /// </summary>
        /// <param name="other"></param>
        internal void Invalidates(ComputeContext other)
        {
            OnInvalidate.ContinueWith(_ => other.Invalidate());
        }

        public override string ToString()
        {
            return "<context: " + Description + ">";
        }
    }
}