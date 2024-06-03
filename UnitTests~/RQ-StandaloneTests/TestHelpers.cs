using System;
using System.Threading.Tasks;

namespace nadena.dev.ndmf.rq.StandaloneTests
{
    public static class TestHelpers
    {
        public static async Task<T> Timeout<T>(this Task<T> task, int timeout = 1000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                return task.Result; 
            }
            else
            {
                throw new TimeoutException("Task did not complete in time");
            }
        }
        
        public static async Task Timeout(this Task task, int timeout = 1000)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {
                await task;
            }
            else
            {
                throw new TimeoutException("Task did not complete in time");
            }
        }
    }
}