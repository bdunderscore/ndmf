using System.Threading.Tasks;
using NUnit.Framework;

namespace nadena.dev.ndmf.rq.StandaloneTests
{
    public class StandaloneTestBase
    {
        private TaskFactory priorFactory = ReactiveQueryScheduler.DefaultTaskFactory;
        
        [OneTimeSetUp]
        public void SetTaskFactory()
        {
            ReactiveQueryScheduler.DefaultTaskFactory = new TaskFactory(TaskScheduler.Default);
        }
        
        [OneTimeTearDown]
        public void ResetTaskFactory()
        {
            ReactiveQueryScheduler.DefaultTaskFactory = priorFactory;
        }
    }
}