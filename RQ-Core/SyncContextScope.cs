#region

using System;
using System.Threading;

#endregion

namespace nadena.dev.ndmf.rq
{
    public sealed class SyncContextScope : IDisposable
    {
        SynchronizationContext _old = SynchronizationContext.Current;

        public SyncContextScope(SynchronizationContext context)
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }

        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(_old);
        }
    }
}