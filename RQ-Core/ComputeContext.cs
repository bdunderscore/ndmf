#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    internal sealed class BlockingNode
    {
        private Lazy<string> _description;
        private string Description => _description.Value;

        public BlockingNode(Lazy<string> description)
        {
            _description = description;
        }

        void SetBlockingOn(BlockingNode waitingOn, bool isWaiting = true)
        {
            // TODO
        }
    }

    /// <summary>
    /// Tracks dependencies around a single computation. Generally, this object should be retained as long as we need to
    /// receive invalidation events (GCing this object may deregister invalidation events).
    /// </summary>
    public sealed class ComputeContext
    {
        internal BlockingNode BlockingOn { get; }

        private TaskCompletionSource<object> _invalidater = new TaskCompletionSource<object>();

        /// <summary>
        /// An Action which can be used to invalidate this compute context (possibly triggering a recompute).
        /// </summary>
        public Action Invalidate { get; }

        /// <summary>
        /// A Task which completes when this compute context is invalidated. Note that completing this task does not
        /// guarantee that the underlying computation (e.g. ReactiveValue) is going to be recomputed.
        /// </summary>
        public Task OnInvalidate { get; }
        
        public CancellationToken CancellationToken { get; internal set; } = CancellationToken.None;

        internal ComputeContext(Func<string> description)
        {
            BlockingOn = new BlockingNode(new Lazy<string>(description));
            Invalidate = () => _invalidater.TrySetResult(null);
            OnInvalidate = _invalidater.Task;
        }

        public async Task<T> Observe<T>(ReactiveValue<T> q)
        {
            // capture the current invalidate function immediately, to avoid infinite invalidate loops
            var invalidate = Invalidate;
            var ct = CancellationToken;

            await TaskThrottle.MaybeThrottle();
            var (cur, next) = await q.GetCurrentAndNext();
            
            // Propagate the invalidation to any listeners synchronously on Invalidate.
            _ = next.ContinueWith(
                _ => invalidate(), ct,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            ct.ThrowIfCancellationRequested();
            return await cur;
        }

        public bool TryObserve<T>(ReactiveValue<T> q, out T value)
        {
            // capture the current invalidate function immediately, to avoid infinite invalidate loops
            var invalidate = Invalidate;
            var ct = CancellationToken;
            
            // Propagate the invalidation to any listeners synchronously on Invalidate.
            _ = q.Changed.ContinueWith(
                _ => invalidate(), ct,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            var result = q.TryGetValue(out value);
            if (!result)
            {
                q.GetValueAsync().ContinueWith(_ => invalidate());
            }

            return result;
        }
    }
}