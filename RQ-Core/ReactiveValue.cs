#region

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

#endregion

namespace nadena.dev.ndmf.rq
{
    /// <summary>
    /// A ReactiveQuery represents a cached computation, which can be automatically invalidated and recomputed when
    /// something it relied upon changes.
    ///
    /// ## Obtaining values from a ReactiveQuery
    ///
    /// There are four ways to obtain a value from a ReactiveQuery:
    /// 1. Continually, by subscribing to the query using `Subscribe`. This option will trigger the computation of the
    ///    value if it is not already available, and will also trigger the computation of the value whenever the query is
    ///    invalidated.
    /// 2. Speculatively, using `TryGetValue` (which will return `false` if the value is not yet available). This option
    ///    does not trigger the computation of the value in most cases.
    /// 2. Asynchronously, using `GetValueAsync` (which will return a `Task<T>` that will complete when the value is
    ///    available). This option will trigger the computation of the value if it is not already available.
    /// 4. From within another ReactiveQuery, by using `ComputeContext.Observe`. This will record the dependency on the
    ///    other query, and will trigger the computation of the value if it is not already available. It will then
    ///    arrange for the calling query to be re-computed whenever the observed query is invalidated.
    ///
    /// ## Writing a ReactiveQuery
    ///
    /// To write a ReactiveQuery, you must implement the `Compute` and `ToString` methods. `Compute` will be invoked
    /// when the query is invalidated, and should return the new value of the computation. Note that, if something
    /// is invalidated while the query is updating, the computation will be cancelled (throwing `TaskCancelledException`s
    /// when observing sub-queries) and restarted.
    ///
    /// Generally, it is important to cache the ReactiveQuery itself somehow (you won't get a lot of benefit from caching
    /// if you create a new ReactiveQuery every time!)
    ///
    /// ## Threading notes
    ///
    /// ReactiveQuery will avoid invoking Compute more than once in parallel; even if invalidated, the last execution
    /// will run to completion (and its result will be ignored), then the new computation will be started.
    ///
    /// Likewise, when using the IObservable interface to observe the state of a query, ReactiveQuery will not invoke
    /// methods on any particular observer in parallel. It is not guaranteed that observers will see every computed
    /// value; we only guarantee that eventually, if invalidations stop, all observers will see the last value.
    ///
    /// ## Using ReactiveQuery in Unity
    ///
    /// When using RQ in Unity, generally you'll want to use the `Subscribe` method to drive various unity editor UI
    /// bits. If you need to access a ReactiveQuery _synchronously_ (for example, in a NDMF plugin), refer to
    /// `ReactiveQueryUnityExt.GetSync`.
    ///
    /// The default TaskFactory for RQ on Unity projects will be one that runs on the Unity main thread. You can
    /// override this by overriding the `Scheduler` property with an appropriate `TaskFactory`.
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ReactiveValue<T> : IObservable<T>
    {
        /*
         * RV works by having a permanent task looping to update the value on invalidation.
         * This works as follows:
         *
         * loop {
         *   waitAll(invalidated, updateRequested);
         *   lock { updatedRequested, invalidated reset }
         *   compute value
         *   lock { set new changed; notify changed }
         * }
         *
         * If nothing holds a reference to the RV or its tasks, this loop will be GC'd as well.
         */


        [PublicAPI] protected virtual TaskScheduler TaskScheduler { get; }

        #region State

        private object _lock = new();

        // Locked by _lock
        private Task _invalidated = Task.CompletedTask;
        private Action _forceInvalidate = () => { };
        private TaskCompletionSource<object> _updateRequested = new();
        private TaskCompletionSource<T> _changed = new();

        // Used to drive DestroyObsoleteValue and TryGetValue
        private T _currentValue = default;
        private bool _currentValueIsValid = false;
        private Exception _currentValueException = null;

        #endregion

        #region Public API

        protected ReactiveValue()
        {
            Task.Factory.StartNew(UpdateLoop);

            using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
            {
                TaskScheduler = ReactiveQueryScheduler.TaskScheduler;
            }
        }

        private class SimpleValue<T> : ReactiveValue<T>
        {
            private string _description;
            private Func<ComputeContext, Task<T>> _compute;

            public SimpleValue(string description, Func<ComputeContext, Task<T>> compute)
            {
                _description = description;
                _compute = compute;
            }

            protected override Task<T> Compute(ComputeContext context)
            {
                return _compute(context);
            }

            public override string ToString()
            {
                return _description;
            }
        }

        /// <summary>
        /// Creates a ReactiveQuery based on a computation delegate.
        /// </summary>
        /// <param name="description">The description of the query, used in error messages</param>
        /// <param name="compute">The function to invoke to compute the query</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ReactiveValue<T> Create(string description, Func<ComputeContext, Task<T>> compute)
        {
            return new SimpleValue<T>(description, compute);
        }

        public ReactiveValue<U> Map<U>(Func<T, U> map)
        {
            return ReactiveValue<U>.Create(ToString(),
                async context => { return map(await context.Observe(this)); });
        }

        /// <summary>
        /// Attempts to get the current value, but only if it is available immediately.
        ///
        /// If the value is unavailable immediately:
        /// * If a value has been computed previously, a stale value will be placed in `value`, and the function will
        ///   return false.
        /// * If no value has been computed yet, `value` will contain `default`.
        /// * In either case, an asynchronous computation will be initiated.
        /// </summary>
        /// <param name="value">The value, if available</param>
        /// <returns>True if the value was available, false if not</returns>
        /// <exception cref="Exception">If the last computation failed</exception>
        public bool TryGetValue(out T value)
        {
            lock (_lock)
            {
                if (!_currentValueIsValid || _invalidated.IsCompleted)
                {
                    _updateRequested.TrySetResult(null);
                    value = default;
                    return false;
                }
                
                value = _currentValue;
                if (_currentValueException != null)
                {
                    throw _currentValueException;
                }

                return true;
            }
        }

        /// <summary>
        /// Returns a Task which will resolve to the query's latest value.
        /// </summary>
        /// <returns></returns>
        public Task<T> GetValueAsync()
        {
            Task<T> next;
            lock (_lock)
            {
                if (_currentValueIsValid && !_invalidated.IsCompleted)
                {
                    if (_currentValueException != null)
                    {
                        return Task.FromException<T>(_currentValueException);
                    }

                    return Task.FromResult(_currentValue);
                }

                next = Changed;
            }

            return next;
        }

        /// <summary>
        /// Returns a task which will complete (with the updated value) the next time this value is computed or updated.
        /// Reading this property ensures that the value will be recomputed on the next invalidation (or ASAP, if it is
        /// currently invalidated).  
        /// </summary>
        public Task<T> Changed
        {
            get
            {
                lock (_lock)
                {
                    if (_changed == null)
                    {
                        _changed = new TaskCompletionSource<T>();
                    }

                    _updateRequested.TrySetResult(null);

                    return _changed.Task;
                }
            }
        }

        /// <summary>
        /// Waits for a value to be available; returns it (wrapped in a task), as well as another task which represents
        /// the subsequent value update.
        /// </summary>
        /// <returns></returns>
        public async Task<(Task<T>, Task<T>)> GetCurrentAndNext()
        {
            while (true)
            {
                Task to_wait;
                lock (_lock)
                {
                    if (_currentValueIsValid && !_invalidated.IsCompleted)
                    {
                        return (GetValueAsync(), Changed);
                    }

                    to_wait = ((Task)Changed)
                        // suppress exceptions, we just want to wait for the task to complete
                        .ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously);
                }

                await to_wait;
            }
        }
        
        #endregion

        #region IObservable<T> API

        private class ObserverContext<T>
        {
            private readonly TaskScheduler _scheduler;
            private IObserver<T> _observer;
            private Task _priorInvocation = Task.CompletedTask;

            public ObserverContext(IObserver<T> observer, TaskScheduler scheduler)
            {
                _observer = observer;
                _scheduler = scheduler;
            }

            public void Invoke(Action<IObserver<T>> action)
            {
                _priorInvocation = _priorInvocation.ContinueWith(_ => action(_observer),
                    CancellationToken.None,
                    // Ensure that we don't invoke an observation while holding our lock
                    TaskContinuationOptions.RunContinuationsAsynchronously,
                    _scheduler
                );
            }
        }

        private HashSet<ObserverContext<T>> _observers = new(new ObjectIdentityComparer<ObserverContext<T>>());

        /// <summary>
        /// Subscribes an observer to this query. The observer will be executed on the TaskScheduler associated with the
        /// current synchronization context.
        /// </summary>
        /// <param name="observer"></param>
        /// <returns>A disposable which will deregister the observer, once disposed</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return Subscribe(observer, null);
        }

        /// <summary>
        /// Subscribes an observer to this query. The observer will be executed on the provided TaskScheduler.
        /// If the provided scheduler is null, the observer will be executed on the TaskScheduler associated with the
        /// current synchronization context.
        /// </summary>
        /// <param name="observer"></param>
        /// <param name="scheduler"></param>
        /// <returns></returns>
        [PublicAPI]
        public IDisposable Subscribe(IObserver<T> observer, TaskScheduler scheduler)
        {
            scheduler = scheduler ?? TaskScheduler.FromCurrentSynchronizationContext();

            var observerContext = new ObserverContext<T>(observer, scheduler);

            lock (_lock)
            {
                _observers.Add(observerContext);

                if (_currentValueIsValid && !_invalidated.IsCompleted)
                {
                    var cv = _currentValue;
                    var ex = _currentValueException;

                    observerContext.Invoke(o =>
                    {
                        if (ex != null)
                        {
                            o.OnError(ex);
                        }
                        else
                        {
                            o.OnNext(cv);
                        }
                    });
                }
                else
                {
                    _updateRequested.TrySetResult(null);
                }
            }

            return new RemoveObserver(this, observerContext);
        }

        private class RemoveObserver : IDisposable
        {
            private readonly ReactiveValue<T> _parent;
            private readonly ObserverContext<T> _observer;

            public RemoveObserver(ReactiveValue<T> parent, ObserverContext<T> observer)
            {
                _parent = parent;
                _observer = observer;
            }

            public void Dispose()
            {
                lock (_parent._lock)
                {
                    _parent._observers.Remove(_observer);
                    _observer.Invoke(o => o.OnCompleted());
                }
            }
        }

        /// <summary>
        /// Immediately invalidates the query. If there are downstream computations or observers, the query will be
        /// recomputed.
        /// </summary>
        public void Invalidate()
        {
            lock (_lock)
            {
                _forceInvalidate();
            }
        }

        #endregion

        #region Subclass API

        /// <summary>
        /// Invoked when the query needs to be recomputed. This method should return the new value of the computation.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected abstract Task<T> Compute(ComputeContext context);

        /// <summary>
        /// Invoked when the query is invalidated and the current value is no longer needed. This method should clean up
        /// any resources associated with the passed value.
        /// </summary>
        /// <param name="value"></param>
        protected virtual void DestroyObsoleteValue(T value)
        {
            // no-op
        }

        // Implementing ToString is mandatory for all subclasses
        public abstract override string ToString();

        #endregion

        #region Internal API

        private async Task<T> Compute0(ComputeContext context)
        {
            await TaskThrottle.MaybeThrottle();
            return await Compute(context);
        }

        private async Task UpdateLoop0()
        {
            Task barrier;
            lock (_lock)
            {
                barrier = Task.WhenAll(_invalidated, _updateRequested.Task);
            }

            await barrier;

            var context = new ComputeContext(ToString);
            lock (_lock)
            {
                _invalidated = context.OnInvalidate;
                _forceInvalidate = context.Invalidate;
                // We keep running updates as long as we have observers; otherwise,
                // we need to see someone query OnChanged before we bother updating.
                if (_observers.Count == 0)
                {
                    _updateRequested = new TaskCompletionSource<object>();
                }
            }

            T result;
            ExceptionDispatchInfo e;
            try
            {
                using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
                {
                    result = await Task.Factory.StartNew(
                        () => Compute0(context),
                        context.CancellationToken,
                        TaskCreationOptions.None,
                        TaskScheduler
                    ).Unwrap();
                }

                e = null;
            }
            catch (Exception ex)
            {
                result = default;
                e = ExceptionDispatchInfo.Capture(ex);
            }
            
            lock (_lock)
            {
                if (e != null)
                {
                    _changed?.TrySetException(e.SourceException);
                }
                else
                {
                    _changed?.TrySetResult(result);
                }

                _changed = null;

                _currentValue = result;
                _currentValueException = e?.SourceException;
                _currentValueIsValid = true;

                Action<IObserver<T>> op = observer =>
                {
                    if (e != null)
                    {
                        observer.OnError(e.SourceException);
                    }
                    else
                    {
                        observer.OnNext(result);
                    }
                };

                foreach (var observer in _observers)
                {
                    observer.Invoke(op);
                }
            }
        }

        private async Task UpdateLoop()
        {
            while (true)
            {
                await UpdateLoop0();
            }
        }
        
        #endregion
    }
}