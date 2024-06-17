#region

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    public static class ReactiveQueryScheduler
    {
        public static TaskFactory DefaultTaskFactory { get; set; } = new TaskFactory(TaskScheduler.Default);

        public static ThreadLocal<SynchronizationContext> SynchronizationContextOverride { get; } = new(() => null);

        public static SynchronizationContext SynchronizationContext =>
            SynchronizationContextOverride.Value ?? SynchronizationContext.Current;


        public static TaskScheduler TaskScheduler
        {
            get
            {
                var oldContext = SynchronizationContext.Current;
                TaskScheduler scheduler;
                if (SynchronizationContextOverride.Value != null)
                {
                    SynchronizationContext.SetSynchronizationContext(SynchronizationContextOverride.Value);
                    scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    SynchronizationContext.SetSynchronizationContext(oldContext);
                }
                else
                {
                    scheduler = TaskScheduler.Default;
                }

                return scheduler;
            }
        }
    }
}