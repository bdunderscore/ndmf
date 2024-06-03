using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace nadena.dev.ndmf.rq.StandaloneTests
{
    public class ObservableInterfaceTest : StandaloneTestBase
    {
        class TestObserver<T> : IObserver<T>
        {
            public volatile TaskCompletionSource<object> TaskCompletionSource = new TaskCompletionSource<object>();
            public List<object> Sequence = new List<object>();

            public void OnCompleted()
            {
                if (TaskCompletionSource?.Task.IsCompleted != true) TaskCompletionSource?.SetResult(null);
                Sequence.Add(null);
            }

            public void OnError(Exception error)
            {
                if (TaskCompletionSource?.Task.IsCompleted != true) TaskCompletionSource?.SetResult(null);
                Sequence.Add(error);
            }

            public void OnNext(T value)
            {
                if (TaskCompletionSource?.Task.IsCompleted != true) TaskCompletionSource?.SetResult(null);
                Sequence.Add(value);
            }
        }

        // We don't want things to be running on the unity main thread as that'll be busy in a blocking wait...
        
        private class ThreadPoolSyncContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                ThreadPool.QueueUserWorkItem(_ => d(state));
            }
            
            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }
        }
        
        private SynchronizationContext _priorSyncContext, _priorOverrideSyncContext;
        [OneTimeSetUp]
        public void Setup()
        {
            SynchronizationContext threadPoolSyncContext = new ThreadPoolSyncContext();
    
            _priorSyncContext = SynchronizationContext.Current;
            _priorOverrideSyncContext = ReactiveQueryScheduler.SynchronizationContextOverride.Value;
        
            SynchronizationContext.SetSynchronizationContext(threadPoolSyncContext);
            ReactiveQueryScheduler.SynchronizationContextOverride.Value = threadPoolSyncContext;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_priorSyncContext);
            ReactiveQueryScheduler.SynchronizationContextOverride.Value = _priorOverrideSyncContext;
        }
        
        [Test]
        [Timeout(2000)]
        public void ObservableBasicTest()
        {
            var rq = new TestQuery<int>(_ => Task.FromResult(42));
            var observer = new TestObserver<int>();
            observer.TaskCompletionSource = new TaskCompletionSource<object>();
            
            var remover = rq.Subscribe(observer);
            Task.WaitAll(new Task[] { observer.TaskCompletionSource.Task }, 100);
            Assert.AreEqual(new List<object>() { 42 }, observer.Sequence);
            
            observer.TaskCompletionSource = new TaskCompletionSource<object>();
            rq.Value = _ => Task.FromResult(43);
            Task.WaitAll(new Task[] { observer.TaskCompletionSource.Task }, 100);
            Assert.AreEqual(new List<object>() { 42, 43 }, observer.Sequence);

            remover.Dispose();
            
            observer.TaskCompletionSource = new TaskCompletionSource<object>();
            rq.Value = _ => Task.FromResult(44);
            Task.WaitAll(new Task[] { observer.TaskCompletionSource.Task }, 100);
            Assert.AreEqual(new List<object>() { 42, 43, null }, observer.Sequence);
        }

        class BarrierObserver : IObserver<int>
        {
            public volatile int ObservationCount = 0;
            public volatile bool Broken = false;
            public Barrier Barrier = new Barrier(2);

            private int _isActive;
            private int _index;
            
            public BarrierObserver(int index)
            {
                _isActive = 0;
                _index = index;
            }
            
            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(int value)
            {
                System.Console.Error.WriteLine($"Observer {_index} got value {value}");
                Interlocked.Increment(ref ObservationCount);
                
                var wasActive = Interlocked.Exchange(ref _isActive, 1);
                if (wasActive == 1)
                {
                    Broken = true;
                    Barrier.Dispose();
                }
                
                Barrier.SignalAndWait();
                Barrier.SignalAndWait();

                _isActive = 0;
            }
        }
        
        [Test]
        [Timeout(2000)]
        public void LimitsParallelismInObserverScope()
        {
            Barrier barrier = new Barrier(2);

            var rq = new TestQuery<int>(_ => Task.FromResult(42));

            var o1 = new BarrierObserver(1);
            var o2 = new BarrierObserver(2);

            IDisposable d1, d2;

            d1 = rq.Subscribe(o1);
            d2 = rq.Subscribe(o2);

            Thread.Sleep(100);

            try
            {
                Assert.IsTrue(o1.Barrier.SignalAndWait(100));
                Assert.IsFalse(o1.Broken);
                Assert.AreEqual(1, o1.ObservationCount);
                Assert.IsTrue(o1.Barrier.SignalAndWait(100));

                rq.Invalidate();

                Assert.IsTrue(o1.Barrier.SignalAndWait(100));
                Assert.IsFalse(o1.Broken);
                Assert.AreEqual(2, o1.ObservationCount);
                Assert.IsTrue(o1.Barrier.SignalAndWait(100));

                rq.Invalidate();

                Assert.IsTrue(o1.Barrier.SignalAndWait(100));
                Assert.IsFalse(o1.Broken);
                Assert.AreEqual(3, o1.ObservationCount);
                Assert.IsTrue(o1.Barrier.SignalAndWait(100));

                // o2 was blocked all this time, let's go check on it now
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));
                Assert.IsFalse(o2.Broken);
                Assert.AreEqual(1, o2.ObservationCount);
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));
                Assert.IsFalse(o2.Broken);
                Assert.AreEqual(2, o2.ObservationCount);
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));
                Assert.IsFalse(o2.Broken);
                Assert.AreEqual(3, o2.ObservationCount);
                Assert.IsTrue(o2.Barrier.SignalAndWait(100));

                d1.Dispose();
                d2.Dispose();
            }
            finally
            {
                o1.Barrier.Dispose();
                o2.Barrier.Dispose();
            }
        }

        [Test]
        [Timeout(2000)]
        public void ErrorReportingTest()
        {
            var ex = new Exception("Test exception");
            var rq = new TestQuery<int>(_ => Task.FromException<int>(ex));
            
            var observer = new TestObserver<int>();
            observer.TaskCompletionSource = new TaskCompletionSource<object>();
            
            rq.Subscribe(observer);
            
            Task.WaitAll(new Task[] { observer.TaskCompletionSource.Task }, 100);
            Assert.AreEqual(new List<object>() { ex }, observer.Sequence);
        }
    }
}