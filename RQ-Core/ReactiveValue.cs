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
        [PublicAPI] protected virtual TaskScheduler TaskScheduler { get; } = ReactiveQueryScheduler.TaskScheduler;

        #region State

        private object _lock = new();

        // Locked by _lock
        private long _invalidationCount = 0;

        private CancellationToken _cancellationToken = CancellationToken.None;
        private Task _cancelledTask = null;
        private TaskCompletionSource<object> _invalidated = null;

        private bool _currentValueIsValid = false;

        private Task<T> _valueTask = null;

        // Used to drive DestroyObsoleteValue
        private T _currentValue = default;
        private Exception _currentValueException = null;

        #endregion

        #region Public API

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
                value = _currentValue;
                if (_currentValueException != null)
                {
                    throw _currentValueException;
                }

                if (!_currentValueIsValid)
                {
                    RequestCompute();
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Returns a Task which will resolve to the query's latest value.
        /// </summary>
        /// <returns></returns>
        public async Task<T> GetValueAsync()
        {
            while (true)
            {
                try
                {
                    return await RequestCompute();
                }
                catch (TaskCanceledException e)
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Returns a task which will complete the next time this task is invalidated. 
        /// </summary>
        public Task Invalidated
        {
            get
            {
                lock (_lock)
                {
                    if (_invalidated == null)
                    {
                        _invalidated = new TaskCompletionSource<object>();
                    }

                    return _invalidated.Task;
                }
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

                if (_currentValueIsValid)
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
                    RequestCompute();
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
            Invalidate(-1);
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

        internal void Invalidate(long expectedSeq)
        {
            using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
            {
                TaskCompletionSource<object> invalidationToken = null;

                lock (_lock)
                {
                    if (expectedSeq == _invalidationCount || expectedSeq == -1)
                    {
                        if (_valueTask != null && !_valueTask.IsCompleted)
                        {
                            _cancelledTask = _valueTask;
                        }

                        invalidationToken = _invalidated;
                        _invalidated = null;
                        _invalidationCount++;
                        _valueTask = null;

                        _currentValueIsValid = false;
                    }

                    if (_observers.Count > 0)
                    {
                        RequestCompute();

                        foreach (var observer in _observers)
                        {
                            observer.Invoke(o => (o as IInvalidationObserver)?.OnInvalidate());
                        }
                    }
                }

                // This triggers invalidation of downstream queries (as well as potentially other user code), so drop the
                // lock before invoking it...
                invalidationToken?.SetResult(null);
            }
        }

        internal async Task<T> ComputeInternal(ComputeContext context)
        {
            await TaskThrottle.MaybeThrottle();

            long seq = _invalidationCount;

            Task cancelledTask;
            lock (_lock)
            {
                cancelledTask = _cancelledTask;
                _cancelledTask = null;

                context.OnInvalidate = Invalidated;
            }

            // Ensure we don't ever have multiple instances of the same RQ computation running in parallel
            if (cancelledTask != null)
            {
                await cancelledTask.ContinueWith(_ => { }); // swallow exceptions
            }

            T result;
            ExceptionDispatchInfo e;
            try
            {
                result = await Compute(context);
                e = null;
            }
            catch (Exception ex)
            {
                result = default;
                e = ExceptionDispatchInfo.Capture(ex);
            }

            Console.WriteLine("ComputeInternal: before lock");
            lock (_lock)
            {
                if (_invalidationCount == seq)
                {
                    if (e == null && !ReferenceEquals(result, _currentValue))
                    {
                        DestroyObsoleteValue(_currentValue);
                    }

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

                    Console.WriteLine("ComputeInternal: before observers");
                    foreach (var observer in _observers)
                    {
                        observer.Invoke(op);
                    }
                }
            }

            Console.WriteLine("ComputeInternal: before exit");
            e?.Throw();
            return result;
        }

        internal Task<T> RequestCompute()
        {
            lock (_lock)
            {
                if (_valueTask == null)
                {
                    var context = new ComputeContext(() => ToString());

                    var invalidateSeq = _invalidationCount;
                    context.Invalidate = () => Invalidate(invalidateSeq);
                    // TODO: arrange for cancellation when we invalidate the task
                    context.CancellationToken = new CancellationToken();

                    using (new SyncContextScope(ReactiveQueryScheduler.SynchronizationContext))
                    {
                        // _context.Activate();
                        _valueTask = Task.Factory.StartNew(
                            () => ComputeInternal(context),
                            context.CancellationToken,
                            TaskCreationOptions.None,
                            TaskScheduler
                        ).Unwrap();
                    }
                }

                return _valueTask;
            }
        }

        #endregion
    }
}