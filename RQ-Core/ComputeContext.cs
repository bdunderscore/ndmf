#region

using System;
using System.Threading;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    public sealed class BlockingNode
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

    public sealed class ComputeContext
    {
        public BlockingNode BlockingOn { get; }
        public Action Invalidate { get; internal set; } = () => { };
        public Task OnInvalidate { get; internal set; }
        public CancellationToken CancellationToken { get; internal set; } = CancellationToken.None;

        internal ComputeContext(Func<string> description)
        {
            BlockingOn = new BlockingNode(new Lazy<string>(description));
        }

        public async Task<T> Observe<T>(ReactiveValue<T> q)
        {
            // capture the current invalidate function immediately, to avoid infinite invalidate loops
            var invalidate = Invalidate;
            var ct = CancellationToken;
            // Propagate the invalidation to any listeners synchronously on Invalidate.
            _ = q.Invalidated.ContinueWith(
                _ => invalidate(), ct,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            var compute = q.RequestCompute();

            await TaskThrottle.MaybeThrottle();
            await Task.WhenAny(compute, q.Invalidated.ContinueWith(_ => Task.FromCanceled(ct)));

            ct.ThrowIfCancellationRequested();
            return await compute;
        }
    }
}