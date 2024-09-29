#region

using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;

#endregion

namespace nadena.dev.ndmf.preview
{
    using Stopwatch = Stopwatch;
    
    internal static class TaskThrottle // TODO: make this a public API?
    {
        private const int TASK_TIME_LIMIT_MS = 200;
        private static readonly Stopwatch _taskTime = new();

        private static TaskCompletionSource<bool> _nextFrame { get; set; }

        private static Task RequestFrame()
        {
            lock (_taskTime)
            {
                if (_nextFrame == null || _nextFrame.Task.IsCompleted) _nextFrame = new TaskCompletionSource<bool>();

                return _nextFrame.Task;
            }
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.update += () =>
            {
                lock (_taskTime)
                {
                    _taskTime.Reset();
                    if (_nextFrame != null)
                    {
                        var frameReleaser = _nextFrame;
                        _nextFrame = null;

                        EditorApplication.delayCall += () => frameReleaser.TrySetResult(true);
                    }
                }
            };
        }

        private static int index;

        public static bool ShouldThrottle
        {
            get
            {
                lock (_taskTime)
                {
                    if (!_taskTime.IsRunning)
                    {
                        _taskTime.Start();
                        return false;
                    }

                    return _taskTime.ElapsedMilliseconds > TASK_TIME_LIMIT_MS;
                }
            }
        }
        
        public static async ValueTask MaybeThrottle()
        {
            lock (_taskTime)
            {
                if (!_taskTime.IsRunning)
                {
                    _taskTime.Start();
                    return;
                }

                if (_taskTime.ElapsedMilliseconds < TASK_TIME_LIMIT_MS) return;
            }

            do
            {
                lock (_taskTime)
                {
                    if (!_taskTime.IsRunning)
                    {
                        _taskTime.Start();
                        break;
                    }

                    if (_taskTime.ElapsedMilliseconds < TASK_TIME_LIMIT_MS) break;
                }

                await RequestFrame();
            } while (true);
        }
    }
}