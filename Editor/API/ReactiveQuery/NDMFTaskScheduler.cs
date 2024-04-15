using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.Profiling;
using VRC.SDKBase;

namespace nadena.dev.ndmf.ReactiveQuery
{
    internal sealed class NDMFTaskScheduler : TaskScheduler
    {
        internal static NDMFTaskScheduler Instance { get; } = new NDMFTaskScheduler();
        
        private Thread _unityMainThread = null;
        private const int MaxFrameTime = 50;

        [InitializeOnLoadMethod]
        static void InitScheduler()
        {
            Instance._unityMainThread = Thread.CurrentThread;
        }
        
        private object _lock = new object();
        private Queue<Task> _tasks = new Queue<Task>();
        private bool _updateEnabled = false;
        
        private bool _inUpdate = false;
        private Stopwatch _updateStopwatch = new Stopwatch();

        private bool UpdateEnabled
        {
            get => _updateEnabled;
            set {
                if (_updateEnabled == value) return;
                
                _updateEnabled = value;
                if (_updateEnabled)
                {
                    EditorApplication.update += Update;
                } else {
                    EditorApplication.update -= Update;
                }
            }
        }

        // Visible for testing
        internal void Update()
        {
            Profiler.BeginSample("NDMFTaskScheduler.Update");
            _inUpdate = true;
            _updateStopwatch.Reset();
            _updateStopwatch.Start();

            try
            {
                lock (_lock)
                {
                    if (_tasks.Count == 0)
                    {
                        UpdateEnabled = false;
                        return;
                    }
                    
                    do
                    {
                        var nextTask = _tasks.Dequeue();
                        if (!nextTask.IsCompleted)
                        {
                            base.TryExecuteTask(nextTask);
                        }
                    } while (_tasks.Count > 0 && _updateStopwatch.ElapsedMilliseconds < MaxFrameTime);

                    UpdateEnabled = _tasks.Count > 0;
                }
            }
            finally
            {
                _inUpdate = false;
                _updateStopwatch.Stop();
                Profiler.EndSample();
            }
        }

        internal void SynchronousWait(Task t)
        {
            lock (_lock)
            {
                if (_unityMainThread == null)
                {
                    throw new ArgumentException("Cannot perform SynchronousWait in static initializers");
                }
                
                if (Thread.CurrentThread.ManagedThreadId != _unityMainThread.ManagedThreadId)
                {
                    throw new InvalidOperationException("Cannot perform SynchronousWait when not on the main thread");
                }
                
                if (_inUpdate)
                {
                    throw new InvalidOperationException("Cannot perform SynchronousWait while in executing in an async context");
                }

                _inUpdate = true;
                _updateStopwatch.Reset();
                // Do not start the stopwatch; we want to run until the task is completed

                try
                {
                    t.Start(this);
                    TryExecuteTaskInline(t, true);
                    while (!t.IsCompleted)
                    {
                        var nextTask = _tasks.Dequeue();
                        if (!nextTask.IsCompleted)
                        {
                            base.TryExecuteTask(nextTask);
                        }
                    }
                }
                finally
                {
                    _inUpdate = false;
                }
            }
        }
        
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_lock)
            {
                return new List<Task>(_tasks);
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_lock)
            {
                _tasks.Enqueue(task);
                UpdateEnabled = true;
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            lock (_lock)
            {
                if (Thread.CurrentThread.ManagedThreadId == _unityMainThread.ManagedThreadId 
                    && _inUpdate && _updateStopwatch.ElapsedMilliseconds < MaxFrameTime)
                {
                    base.TryExecuteTask(task);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}