#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview.trace;
using UnityEditor;
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

        private static object _pendingInvalidatesLock = new();
        private static bool _pendingInvalidatesScheduled;
        private static List<ComputeContext> _pendingInvalidates = new();

        private static void ScheduleInvalidate(ComputeContext ctx)
        {
            lock (_pendingInvalidatesLock)
            {
                _pendingInvalidates.Add(ctx);
                
                if (_pendingInvalidatesScheduled) return;
                
                _pendingInvalidatesScheduled = true;
                NDMFSyncContext.Context.Post(_ => FlushInvalidates(), null);
            }
        }

        /// <summary>
        /// ComputeContext deferres some processing until EditorApplication.delayCall on invalidate. Invoke this function
        /// to flush and execute all pending invalidates immediately.
        /// </summary>
        public static void FlushInvalidates()
        {
            _pendingInvalidatesScheduled = false;
            
            List<ComputeContext> list;

            lock (_pendingInvalidatesLock)
            {
                list = new(_pendingInvalidates.Count);
                list.AddRange(_pendingInvalidates);
                _pendingInvalidates.Clear();
            }

            while (list.Count > 0)
            {
                //System.Diagnostics.Debug.WriteLine("Flushing invalidates: " + list.Count);
                foreach (var ctx in list)
                {
                    // We need to take the lock here to ensure that any final registrations complete
                    lock (ctx)
                    {
                        ctx._onInvalidateListeners.FireAll();
                        ctx._onInvalidateTask.TrySetResult(true);
                    }
                }

                lock (_pendingInvalidatesLock)
                {
                    list.Clear();
                    
                    list.AddRange(_pendingInvalidates);
                    _pendingInvalidates.Clear();
                }
            }
        }

        static ComputeContext()
        {
            NullContext = new ComputeContext("null", null);
        }

        ~ComputeContext()
        {
            if (!IsInvalidated)
            {
                TraceBuffer.RecordTraceEvent(
                    "ComputeContext.Leak",
                    (ev) => "Leaked context: " + ev.Arg0,
                    arg0: this
                );
            }
        }
        

        internal string Description { get; }
        
        /// <summary>
        ///     An Action which can be used to invalidate this compute context (possibly triggering a recompute).
        /// </summary>
        public Action Invalidate { get; }

        /// <summary>
        ///     A Task which completes when this compute context is invalidated. Note that completing this task does not
        ///     guarantee that the underlying computation (e.g. ReactiveValue) is going to be recomputed.
        /// </summary>
        //
        // Note: This is different from the _invalidator task; we add a delay to ensure that we defer any outside
        // callbacks until after all change stream events are processed.
        internal Task OnInvalidate
        {
            get => _onInvalidateTask.Task;
        }
        private TaskCompletionSource<bool> _onInvalidateTask = new();

        private ListenerSet<object> _onInvalidateListeners = new();

        public bool IsInvalidated => _invalidatePending || _invalidater.Task.IsCompleted;
        private bool _invalidatePending;

        private long? _invalidateTriggerEvent;

        public ComputeContext(string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                Debug.LogWarning("ComputeContext created with empty description");
            }
            
            Invalidate = () =>
            {
#if NDMF_TRACE
                Debug.Log("Invalidating " + Description);
#endif

                if (_invalidatePending || IsInvalidated) return;
                
                _invalidateTriggerEvent = TraceBuffer.RecordTraceEvent(
                    "ComputeContext.Invalidate",
                    (ev) => "Invalidate: " + ev.Arg0,
                    arg0: this
                ).EventId;
                
                NDMFSyncContext.RunOnMainThread(target => DoInvalidate((ComputeContext)target), this);
            };
            Description = description;

            _invalidater.Task.ContinueWith(_ => ScheduleInvalidate(this));
        }

        private static void DoInvalidate(ComputeContext ctx)
        {
            lock (ctx)
            {
                if (ctx._invalidatePending) return;
                ctx._invalidatePending = true;

                ctx._invalidater.TrySetResult(null);
            }
        }

        private ComputeContext(string description, object nullToken)
        {
            Invalidate = () => { };
            _onInvalidateTask.TrySetResult(true);
            _invalidater.TrySetResult(null);
            Description = description;
        }

        /// <summary>
        ///     Invalidate the `other` compute context when this compute context is invalidated.
        /// </summary>
        /// <param name="other"></param>
        public void Invalidates(ComputeContext other)
        {
            if (other.IsInvalidated) return;

            InvokeOnInvalidate(other, ForwardInvalidation);
        }

        private void ForwardInvalidation(ComputeContext obj)
        {
            obj.Invalidate();
        }

        /// <summary>
        /// Invokes the given receiver function when this context is invalidated.
        /// The target object is passed to the receiver.
        ///
        /// This function does not maintain a strong reference to the target object;
        /// if the target object is GCed, the receiver will not be invoked.
        /// You may also cancel this subscription by disposing the returned IDisposable.
        ///
        /// Must be invoked on the unity main thread.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="receiver"></param>
        [PublicAPI]
        public IDisposable InvokeOnInvalidate<T>(T target, Action<T> receiver)
        {
            lock (this)
            {
                if (IsInvalidated)
                {
                    receiver(target);
                    return new EmptyDisposable();
                }
                return _onInvalidateListeners.Register(target, t => receiver((T) t));    
            }
        }

        public override string ToString()
        {
            return "<context: " + Description + ">";
        }
    }
}