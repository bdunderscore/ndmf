#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using nadena.dev.ndmf.cs;
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
                EditorApplication.delayCall += FlushInvalidates;
            }
        }

        internal static void FlushInvalidates()
        {
            var list = _pendingInvalidates;
            _pendingInvalidates = new List<ComputeContext>();
            
            System.Diagnostics.Debug.WriteLine("Flushing invalidates: " + list.Count);
            
            foreach (var ctx in list)
            {
                InvalidateInternal(ctx);
            }
        }

        static ComputeContext()
        {
            NullContext = new ComputeContext("null", null);
        }
        
        #if NDMF_TRACE
        
        ~ComputeContext()
        {
            if (!IsInvalidated)
                Debug.LogError("ComputeContext " + Description + " was GCed without being invalidated!");
        }
        
        #endif

        internal string Description { get; }
        
        /// <summary>
        ///     An Action which can be used to invalidate this compute context (possibly triggering a recompute).
        /// </summary>
        public Action Invalidate { get; }

        /// <summary>
        ///     A Task which completes when this compute context is invalidated. Note that completing this task does not
        ///     guarantee that the underlying computation (e.g. ReactiveValue) is going to be recomputed.
        /// </summary>
        internal Task OnInvalidate { get; }

        private ListenerSet<object> _onInvalidateListeners = new();

        public bool IsInvalidated => _invalidatePending || OnInvalidate.IsCompleted;
        private bool _invalidatePending;

        public ComputeContext(string description)
        {
            Invalidate = () =>
            {
#if NDMF_TRACE
                Debug.Log("Invalidating " + Description);
#endif
                TaskUtil.OnMainThread(this, DoInvalidate);
            };
            OnInvalidate = _invalidater.Task;
            Description = description;
        }

        private static void DoInvalidate(ComputeContext ctx)
        {
            lock (ctx)
            {
                if (ctx._invalidatePending) return;
                ctx._invalidatePending = true;

                ScheduleInvalidate(ctx);
            }
        }

        private static void InvalidateInternal(ComputeContext ctx)
        {
            ctx._invalidater.TrySetResult(null);
            ctx._onInvalidateListeners.FireAll();
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
        public void Invalidates(ComputeContext other)
        {
            OnInvalidate.ContinueWith(_ => InvalidateInternal(other));
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