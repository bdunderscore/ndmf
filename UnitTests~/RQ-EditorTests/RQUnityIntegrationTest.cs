using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.rq;
using nadena.dev.ndmf.rq.StandaloneTests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTests.EditorTests
{
    public class RQUnityIntegrationTest
    {
        private static int mainThreadId;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [Test]
        public async Task ReactiveQueriesRunInMainThread()
        {
            ReactiveValue<int> rq =
                ReactiveValue<int>.Create("", _ => Task.FromResult(Thread.CurrentThread.ManagedThreadId));

            Assert.AreEqual(mainThreadId, await rq.GetValueAsync().Timeout());
        }

        [Test]
        public async Task ReactiveQueriesThrottle()
        {
            int startedCount = 0;
            List<Task<int>> queries = new List<Task<int>>();

            var pq = ReactiveValue<int>.Create("", _ => Task.FromResult(1));

            for (int i = 0; i < 10; i++)
            {
                queries.Insert(0, ReactiveValue<int>.Create("", async ctx =>
                {
                    Interlocked.Increment(ref startedCount);
                    //.Debug.Log("sleep start");
                    Thread.Sleep(50);
                    //Debug.Log("sleep end");
                    return 1;
                }).GetValueAsync().ContinueWith(t =>
                {
                    //Debug.Log("Task complete");
                    return t.Result;
                }));
            }
            
            await Task.Delay(25);//.ContinueWith(_ => Debug.Log("Delay complete"));
            
            var completedCount = queries.Count(q => q.IsCompleted);
            Debug.Log("Completed: " + completedCount);
            Assert.IsTrue(completedCount > 0);
            Assert.IsTrue(completedCount < queries.Count);

            var allDone = Task.WhenAll(queries);
            Assert.AreSame(allDone, await Task.WhenAny(allDone, Task.Delay(2000)));
        }
    }
}