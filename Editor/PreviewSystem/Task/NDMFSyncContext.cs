using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     The default unity synchronization context runs in the context of the Player Loop, which blocks access to R/O
    ///     mesh data. As such, NDMF provides a synchronization context which runs in the context of
    ///     `EditorApplication.delayCall` instead.
    /// </summary>
    public static class NDMFSyncContext
    {
        public static SynchronizationContext Context = new Impl();
        internal static Impl InternalContext = (Impl)Context;
        
        /// <summary>
        ///    Runs the given function on the unity main thread, passing the given target as an argument.
        ///    The function will be invoked synchronously if called from the main thread; otherwise, it will be
        ///    run asynchronously.
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="target"></param>
        internal static void RunOnMainThread(SendOrPostCallback receiver, object target)
        {
            if (Thread.CurrentThread.ManagedThreadId == InternalContext.unityMainThreadId)
            {
                receiver(target);
            }
            else
            {
                Context.Post(receiver, target);
            }
        }
        
        /// <summary>
        ///     Switches to the NDMF synchronization context, and returns an IDisposable which will restore the prior
        ///     synchronization context.
        /// </summary>
        /// <returns></returns>
        public static IDisposable Scope()
        {
            return new SyncContextScope();
        }

        private class SyncContextScope : IDisposable
        {
            private readonly SynchronizationContext _prior = SynchronizationContext.Current;

            public SyncContextScope()
            {
                SynchronizationContext.SetSynchronizationContext(Context);
            }

            public void Dispose()
            {
                SynchronizationContext.SetSynchronizationContext(_prior);
            }
        }

        internal class Impl : SynchronizationContext
        {
            private SynchronizationContext _unityMainThreadContext;
            
            private readonly object _lock = new();
            private readonly EditorApplication.CallbackFunction _turnDelegate;
            internal int unityMainThreadId = -1;
            private readonly List<WorkRequest> asyncQueue = new();
            private readonly List<WorkRequest> localQueue = new();
            private bool isRegistered, isTurning;

            internal Impl()
            {
                _turnDelegate = Turn;
                EditorApplication.delayCall += () =>
                {
                    // Obtain the unity synchronization context
                    _unityMainThreadContext = Current;
                    unityMainThreadId = Thread.CurrentThread.ManagedThreadId;

                    Turn(); // process anything that was queued before we had a chance to initialize
                };
            }

            // invoked under _lock
            private void RegisterCallback()
            {
                if (isRegistered || _unityMainThreadContext == null) return;
                isRegistered = true;

                _unityMainThreadContext.Post(InvokeTurn, this);
            }

            private void InvokeTurn(object state)
            {
                ((Impl)state).Turn();
            }

            // visible for tests
            internal void Turn()
            {
                lock (_lock)
                {
                    isRegistered = false;
                    EditorApplication.update -= _turnDelegate;
                    try
                    {
                        unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
                        localQueue.AddRange(asyncQueue);
                        asyncQueue.Clear();
                        isTurning = true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                while (localQueue.Count > 0 && !TaskThrottle.ShouldThrottle)
                {
                    foreach (var ev in localQueue)
                    {
                        ev.Run();
                    }

                    localQueue.Clear();

                    if (!TaskThrottle.ShouldThrottle)
                    {
                        ComputeContext.FlushInvalidates();
                        
                        lock (_lock)
                        {
                            localQueue.AddRange(asyncQueue);
                            asyncQueue.Clear();
                        }
                    }
                }

                lock (_lock)
                {
                    if (localQueue.Count > 0)
                    {
                        RegisterCallback();
                    }

                    isTurning = false;
                }
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (_lock)
                {
                    asyncQueue.Add(new WorkRequest { callback = d, state = state });
                    RegisterCallback();
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                ManualResetEvent wait = null;
                var runLocally = false;
                lock (_lock)
                {
                    runLocally = unityMainThreadId == Thread.CurrentThread.ManagedThreadId && isTurning;
                    if (!runLocally)
                    {
                        wait = new ManualResetEvent(false);
                        asyncQueue.Add(new WorkRequest { callback = d, state = state, waitHandle = wait });
                        RegisterCallback();
                    }
                }

                if (runLocally)
                {
                    try
                    {
                        d(state);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
                else
                {
                    wait.WaitOne();
                }
            }
        }

        private class WorkRequest
        {
            public SendOrPostCallback callback;
            public object state;
            public ManualResetEvent waitHandle;

            public void Run()
            {
                try
                {
                    callback(state);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    waitHandle?.Set();
                }
            }
        }
    }
}