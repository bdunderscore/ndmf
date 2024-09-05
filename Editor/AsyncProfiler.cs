using System;
using System.Threading;
using UnityEditor;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    public static class AsyncProfiler
    {
        private static int _mainThreadId;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }
        
        private class ProfilerFrame
        {
            public ProfilerFrame Parent;
            public ProfilerFrame Root;
            public int Depth;

            public string Context;
            public Object Object;
        }

        private static readonly AsyncLocal<ProfilerFrame> _currentFrame = new(OnFrameChange);

        private static void OnFrameChange(AsyncLocalValueChangedArgs<ProfilerFrame> obj)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) return;
            
            var currentFrame = obj.PreviousValue;
            var newFrame = obj.CurrentValue;

            while (currentFrame != null && (newFrame == null || newFrame.Root != currentFrame.Root ||
                                            newFrame.Depth <= currentFrame.Depth))
            {
                Profiler.EndSample();
                currentFrame = currentFrame.Parent;
            }

            EnterFrameRecursive(newFrame, currentFrame?.Depth ?? -1);
        }

        private static void EnterFrameRecursive(ProfilerFrame newFrame, int currentFrameDepth)
        {
            if (newFrame == null || newFrame.Depth <= currentFrameDepth) return;

            if (newFrame.Parent != null) EnterFrameRecursive(newFrame.Parent, currentFrameDepth);

            Profiler.BeginSample(newFrame.Context, newFrame.Object);
        }

        public static IDisposable PushProfilerContext(string context, Object obj = null)
        {
            var currentFrame = _currentFrame.Value;

            var newFrame = new ProfilerFrame
            {
                Parent = currentFrame,
                Root = currentFrame?.Root,
                Depth = currentFrame?.Depth + 1 ?? 0,
                Context = context,
                Object = obj
            };

            if (newFrame.Root == null) newFrame.Root = newFrame;

            _currentFrame.Value = newFrame;

            return new PopFrame(newFrame);
        }

        private class PopFrame : IDisposable
        {
            private readonly ProfilerFrame _targetFrame;

            public PopFrame(ProfilerFrame targetFrame)
            {
                _targetFrame = targetFrame;
            }

            public void Dispose()
            {
                var currentFrame = _currentFrame.Value;
                if (currentFrame.Root != _targetFrame.Root || currentFrame.Depth < _targetFrame.Depth) return;

                _currentFrame.Value = _targetFrame;
            }
        }
    }
}