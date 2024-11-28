#nullable enable

using System;
using JetBrains.Annotations;
using UnityEngine.Profiling;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// Performs a `Profiler.BeginSample` call on construction, and `Profiler.EndSample` on disposal.
    /// </summary>
    [PublicAPI]
    public struct ProfilerScope : IDisposable
    {
        public ProfilerScope(string name, UnityEngine.Object? target = null)
        {
            Profiler.BeginSample(name, target);
        }

        public void Dispose()
        {
            Profiler.EndSample();
        }
    }
}