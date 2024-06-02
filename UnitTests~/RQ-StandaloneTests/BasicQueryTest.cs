using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace nadena.dev.ndmf.rq.StandaloneTests
{
    public class BasicQueryTest : StandaloneTestBase
    {
        [Test]
        [Timeout(5000)]
        public async Task TrivialQuery()
        {
            var q = new TestQuery<int>(_ => Task.FromResult(42));

            Assert.IsFalse(q.TryGetValue(out var _));
            var task = q.GetValueAsync();

            Assert.AreEqual(42, await task.Timeout());
            Assert.IsTrue(q.TryGetValue(out var result));
            Assert.AreEqual(42, result);
        }

        [Test]
        [Timeout(5000)]
        public async Task CacheAndInvalidate()
        {
            int value = 1;
            
            var q = new TestQuery<int>(_ => Task.FromResult(value));

            Assert.AreEqual(1, await q.GetValueAsync().Timeout());
            value = 2;
            Assert.AreEqual(1, await q.GetValueAsync().Timeout());
            q.Invalidate();
            Assert.AreEqual(2, await q.GetValueAsync().Timeout());
        }
        
        [Test]
        [Timeout(5000)]
        public async Task ChainedInvalidation()
        {
            int value = 1;
            
            var q1 = new TestQuery<int>(_ => Task.FromResult(value));
            var q2 = new TestQuery<int>(async ctx => await ctx.Observe(q1));
            
            Assert.AreEqual(1, await q2.GetValueAsync().Timeout());
            
            value = 2;
            q1.Invalidate();
            
            Assert.AreEqual(2, await q2.GetValueAsync().Timeout());
        }
        
        [Test]
        [Timeout(5000)]
        public async Task TaskDelay()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            var q = new TestQuery<int>(_ => tcs.Task);
            var q2 = new TestQuery<int>(async ctx => await ctx.Observe(q));

            var t2 = q2.GetValueAsync();
            await Task.Delay(100);
            Assert.IsFalse(t2.IsCompleted);
            
            tcs.SetResult(42);
            Assert.AreEqual(42, await t2.Timeout());
        }

        [Test]
        [Timeout(5000)]
        public async Task CancellationAwaitsPreviousExecution()
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            var q = new TestQuery<int>(_ => tcs.Task);

            var task = q.GetValueAsync();
            await Task.Delay(100);
            
            q.Value = _ => Task.FromResult(42);

            var task2 = q.GetValueAsync();
            await Task.Delay(100);
            Assert.IsFalse(task2.IsCompleted);
            
            tcs.SetResult(123);
            Assert.AreEqual(42, await task2.Timeout(100));
        }

        [Test]
        [Timeout(5000)]
        public async Task ObserveMultipleQueries()
        {
            var q1 = new TestQuery<int>(_ => Task.FromResult(1));
            var q2 = new TestQuery<int>(_ => Task.FromResult(2));
            var q3 = new TestQuery<int>(async ctx => await ctx.Observe(q1) + await ctx.Observe(q2));
            
            Assert.AreEqual(3, await q3.GetValueAsync().Timeout());
            
            q2.Value = _ => Task.FromResult(30);
            Assert.AreEqual(31, await q3.GetValueAsync().Timeout());
            
            q1.Value = _ => Task.FromResult(10);
            Assert.AreEqual(40, await q3.GetValueAsync().Timeout());
        }

        [Test]
        //[Timeout(5000)]
        public async Task StopObserving()
        {
            var counter = 1;
            var q1 = new TestQuery<int>(_ => Task.FromResult(counter++));

            var shouldCheck = new TestQuery<bool>(_ => Task.FromResult(true));
            var q2 = new TestQuery<int>(async ctx =>
            {
                Console.WriteLine("L1");
                var check = await ctx.Observe(shouldCheck);
                Console.WriteLine("L2 " + check);
                if (check)
                {
                    var observe = await ctx.Observe(q1);
                    Console.WriteLine("L3 " + observe);
                    return observe;
                }
                else
                {
                    Console.WriteLine("L4");
                    return 0;
                }
            });
            
            Assert.AreEqual(1, await q2.GetValueAsync().Timeout());
            q1.Invalidate();
            Assert.AreEqual(2, await q2.GetValueAsync().Timeout());
            shouldCheck.Value = _ => Task.FromResult(false);
            Assert.AreEqual(0, await q2.GetValueAsync().Timeout());
            q1.Invalidate();
            Assert.AreEqual(0, await q2.GetValueAsync().Timeout());
        }
    }
}