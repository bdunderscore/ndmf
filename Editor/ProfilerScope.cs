using System;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf
{
    internal struct ProfilerScope : IDisposable
    {
        public ProfilerScope(string name, Object refObject = null)
        {
            Profiler.BeginSample(name, refObject);
        }

        public void Dispose()
        {
            Profiler.EndSample();
        }
    }
}