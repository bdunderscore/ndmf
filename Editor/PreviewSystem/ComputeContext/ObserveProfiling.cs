#region

using System;
using System.Collections.Concurrent;
using System.IO;
using Unity.Profiling;

#endregion

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     Wraps the extract/compare delegates handed to ComputeContext.Observe in profiler markers naming the
    ///     Observe call site, so that change detection cost can be attributed back to the code which asked for it.
    ///
    ///     This is only active when NDMF_PROFILING is defined (Project Settings -> Player -> Scripting Define
    ///     Symbols); otherwise every method here is an identity function which the JIT inlines away.
    /// </summary>
    internal static class ObserveProfiling
    {
#if NDMF_PROFILING
        // Keyed by (file, line, kind); building the marker name is far too expensive to do per Observe call.
        private static readonly ConcurrentDictionary<(string, int, string), ProfilerMarker> _markers = new();

        private static ProfilerMarker GetMarker(string callerPath, int callerLine, string kind)
        {
            return _markers.GetOrAdd((callerPath, callerLine, kind), key =>
            {
                var (path, line, k) = key;
                var file = string.IsNullOrEmpty(path) ? "???" : Path.GetFileName(path);

                return new ProfilerMarker($"ComputeContext.Observe.{k} ({file}:{line})");
            });
        }
#endif

        internal static Func<T, R> Extract<T, R>(Func<T, R> extract, string callerPath, int callerLine)
        {
#if NDMF_PROFILING
            if (extract == null) return null;

            var marker = GetMarker(callerPath, callerLine, "Extract");

            return obj =>
            {
                using (marker.Auto())
                {
                    return extract(obj);
                }
            };
#else
            return extract;
#endif
        }

        internal static Func<R, R, bool> Compare<R>(Func<R, R, bool> compare, string callerPath, int callerLine)
        {
#if NDMF_PROFILING
            // Left null so the caller's own default comparer selection still applies.
            if (compare == null) return null;

            var marker = GetMarker(callerPath, callerLine, "Compare");

            return (a, b) =>
            {
                using (marker.Auto())
                {
                    return compare(a, b);
                }
            };
#else
            return compare;
#endif
        }
    }
}
