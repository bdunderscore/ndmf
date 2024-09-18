using System;
using System.Diagnostics;
using System.Linq;
using Debug = UnityEngine.Debug;

namespace nadena.dev.ndmf.preview
{
    internal static class FrameTimeLimiter
    {
        private const int MAX_SINGLE_FRAME_TIME_MS = 2500;
        private const int MAX_AVG_FRAME_TIME_MS = 500;
        private const int FRAME_TIME_WINDOW = 10;

        private static int[] _frameTimes = new int[FRAME_TIME_WINDOW];
        private static int _frameTimeIndex;

        private static readonly Stopwatch _frameTimer = new();

        internal class Scope : IDisposable
        {
            private readonly bool _forceStop;

            public Scope()
            {
                _forceStop = !NDMFPreview.EnablePreviewsUI;
                _frameTimer.Restart();
            }

            public bool ShouldContinue()
            {
                return Debugger.IsAttached ||
                       (!_forceStop && _frameTimer.ElapsedMilliseconds < MAX_SINGLE_FRAME_TIME_MS);
            }

            public void Dispose()
            {
                _frameTimer.Stop();
                _frameTimes[_frameTimeIndex] = (int)_frameTimer.ElapsedMilliseconds;
                _frameTimeIndex = (_frameTimeIndex + 1) % FRAME_TIME_WINDOW;

                if (!Debugger.IsAttached && _frameTimes.Sum() > MAX_AVG_FRAME_TIME_MS * FRAME_TIME_WINDOW)
                {
                    Debug.LogError(
                        "[NDMF Preview] Disabled previews due to performance issues. / プレビューが重すぎるため自動敵に無効化しました。");
                    NDMFPreview.EnablePreviewsUI = false;
                    _frameTimes = new int[FRAME_TIME_WINDOW];
                }
            }
        }


        public static Scope OpenFrameScope()
        {
            return new Scope();
        }
    }
}