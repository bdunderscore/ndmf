#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    internal sealed class ThrottledSynchronizationContext : SynchronizationContext
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            ReactiveQueryScheduler.SynchronizationContextOverride.Value
                = new ThrottledSynchronizationContext(Current);
        }

        private static CustomSampler _tscontext = CustomSampler.Create("ThrottledSynchronizationContext");
        private static CustomSampler _executingTask = CustomSampler.Create("TaskRunning");
        private readonly object _lock = new object();
        private readonly SynchronizationContext _parent;
        private Queue<PendingWork> _pendingWork = new Queue<PendingWork>();
        private int _owningThreadId;

        public int OwningThreadId => _owningThreadId;

        // locked:
        private List<PendingWork> _remoteWork = new List<PendingWork>();
        private bool _isQueued = false;

        private bool IsRunning { get; set; } = false;

        public bool InContext => _owningThreadId == Thread.CurrentThread.ManagedThreadId && IsRunning;

        private bool IsQueued
        {
            get => _isQueued;
            set
            {
                if (value == _isQueued)
                {
                    return;
                }

                _isQueued = value;
                if (_isQueued)
                {
                    _parent.Post(RunWithTimeLimit, this);
                }
            }
        }

        public ThrottledSynchronizationContext(SynchronizationContext parent)
        {
            _parent = parent;
            parent.Send(InitThreadId, this);
        }

        private static void InitThreadId(object state)
        {
            var self = (ThrottledSynchronizationContext)state;
            self._owningThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private static void RunWithTimeLimit(object state)
        {
            var self = (ThrottledSynchronizationContext)state;

            lock (self._lock)
            {
                self.IsQueued = false;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            self.RunUntil(() => sw.ElapsedMilliseconds >= 100);
        }

        public void RunUntil(Func<bool> terminationCondition)
        {
            if (_owningThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException("Can only be called from the owning thread");
            }

            _tscontext.Begin();
            lock (_lock)
            {
                IsRunning = true;
                _remoteWork.ForEach(_pendingWork.Enqueue);
                _remoteWork.Clear();
            }

            using (TaskThrottle.WithThrottleCondition(terminationCondition))
            {
                int n = 0;
                do
                {
                    _executingTask.Begin();
                    _pendingWork.Dequeue().Run();
                    _executingTask.End();
                    n++;
                } while (_pendingWork.Count > 0 && !terminationCondition());

                /*
                if (_pendingWork.Count > 0)
                {
                    Debug.Log("Throttling SynchronizationContext: " + n + " tasks processed, " + _pendingWork.Count +
                              " remaining");
                }
                */
            }

            lock (_lock)
            {
                IsRunning = false;
                if (_pendingWork.Count > 0)
                {
                    IsQueued = true;
                }
            }

            _tscontext.End();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (_owningThreadId == Thread.CurrentThread.ManagedThreadId && IsRunning)
            {
                _pendingWork.Enqueue(new PendingWork(d, state, null));
            }
            else
            {
                lock (_lock)
                {
                    CheckInvocation(d, state);
                    _remoteWork.Add(new PendingWork(d, state, null));
                    IsQueued = true;
                }
            }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (_owningThreadId == Thread.CurrentThread.ManagedThreadId && IsRunning)
            {
                d(state);
            }
            else
            {
                CheckInvocation(d, state);
                var waitHandle = new ManualResetEvent(false);
                lock (_lock)
                {
                    _remoteWork.Add(new PendingWork(d, state, waitHandle));
                    IsQueued = true;
                }

                waitHandle.WaitOne();
            }
        }

        private void CheckInvocation(SendOrPostCallback d, object state)
        {
            /*
            if (Thread.CurrentThread.ManagedThreadId != _owningThreadId) return;
            if (Current == this) return;

            Debug.LogWarning(
                "Work was enqueued into ThrottledSynchronizationContext from a foreign task. This can result in deadlocks! callback=" + d + " state=" + state);
                */
        }

        private class PendingWork
        {
            public SendOrPostCallback Callback;
            public object State;
            public ManualResetEvent WaitHandle;

            public PendingWork(SendOrPostCallback callback, object state, ManualResetEvent waitHandle)
            {
                Callback = callback;
                State = state;
                WaitHandle = waitHandle;
            }

            public void Run()
            {
                try
                {
                    Callback(State);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    WaitHandle?.Set();
                }
            }
        }
    }
}