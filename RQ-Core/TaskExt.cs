#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    public static class TaskExt
    {
        /// <summary>
        /// Prevents deep recursion by ensuring that this task, upon completion, returns to the thread pool instead of
        /// immediately calling its continuation. This should be used carefully, as it can negatively impact performance.
        ///
        /// This differs from simply using TaskContinuationOptions.RunContinuationsAsynchronously in that it will
        /// ensure that the correct synchronization context is used for the continuation.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Task PreventRecursion(this Task t)
        {
            return t.ContinueWith(
                t2 => t2,
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }

        /// <summary>
        /// Prevents deep recursion by ensuring that this task, upon completion, returns to the thread pool instead of
        /// immediately calling its continuation. This should be used carefully, as it can negatively impact performance.
        ///
        /// This differs from simply using TaskContinuationOptions.RunContinuationsAsynchronously in that it will
        /// ensure that the correct synchronization context is used for the continuation.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Task<T> PreventRecursion<T>(this Task<T> t)
        {
            return t.ContinueWith(
                t2 => t2.Result,
                CancellationToken.None,
                TaskContinuationOptions.RunContinuationsAsynchronously,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
        }
    }
}