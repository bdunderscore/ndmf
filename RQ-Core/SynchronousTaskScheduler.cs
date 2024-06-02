#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    internal sealed class SynchronousTaskScheduler : TaskScheduler
    {
        internal static SynchronousTaskScheduler Instance { get; } = new SynchronousTaskScheduler();

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return Array.Empty<Task>();
        }

        protected override void QueueTask(Task task)
        {
            TryExecuteTask(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            TryExecuteTask(task);
            return true;
        }
    }
}