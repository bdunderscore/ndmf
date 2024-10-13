using System;
using UnityEngine.Profiling;

namespace nadena.dev.ndmf
{
    internal struct ProfilerScope : IDisposable
    {
        public ProfilerScope(string name)
        {
            Profiler.BeginSample(name);
        }

        public void Dispose()
        {
            Profiler.EndSample();
        }
    }
}