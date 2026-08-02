#region

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Jobs;

#endregion

namespace nadena.dev.ndmf.preview
{
    public static partial class ComputeContextQueries
    {
        /// <summary>
        /// Observes the world-space position, rotation, and scale of a given transform.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="t"></param>
        public static Transform ObserveTransformPosition(this ComputeContext ctx, Transform t)
        {
            TransformPositionMonitor.Monitor(ctx, t);
            return t;
        }
    }

    [InitializeOnLoad]
    internal static class TransformPositionMonitor
    {
        private const int InitialCapacity = 1024;
        private const float TransformEpsilon = 0.0001f;
        private const float TransformEpsilonSqr = TransformEpsilon * TransformEpsilon;
        private static readonly ProfilerMarker UpdateMarker = new("TransformPositionMonitor.Update");

        private sealed class MonitoredTransform
        {
            internal readonly Transform Transform;
            internal readonly ComputeContext Context;

            internal MonitoredTransform(Transform transform)
            {
                Transform = transform;
                Context = new ComputeContext($"ObserveTransformPosition {transform.name}");
            }
        }

        // Unity's comparer delegates to the object's current identity representation (instance or entity ID).
        private static readonly Dictionary<Transform, MonitoredTransform> Monitors = new();
        private static readonly List<(MonitoredTransform, float4x4)> PendingMonitors = new();
        private static readonly List<MonitoredTransform> Slots = new();
        private static readonly Stack<int> FreeSlots = new();

        private static TransformAccessArray _transforms = new(InitialCapacity);
        private static NativeArray<float4x4> _worldTransforms =
            new(InitialCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        private static NativeArray<byte> _activeSlots =
            new(InitialCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        private static NativeQueue<int> _changedSlots = new(Allocator.Persistent);
        private static JobHandle _pendingJob;
        private static bool _hasPendingJob;
        private static int _activeMonitorCount;
        private static int _capacity = InitialCapacity;

        static TransformPositionMonitor()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        internal static void Monitor(ComputeContext downstreamContext, Transform transform)
        {
            if (transform == null)
            {
                downstreamContext.Invalidate();
                return;
            }

            if (!Monitors.TryGetValue(transform, out var monitored))
            {
                monitored = new MonitoredTransform(transform);
                Monitors.Add(transform, monitored);
                PendingMonitors.Add((monitored, CheckWorldTransformsJob.ToFloat4x4(transform.localToWorldMatrix)));
            }

            // Lean on context chaining; ComputeContext has a bunch of handling in place to help deal with dropped
            // or already-invalidated downstream context that we don't want to have to reimplement.
            monitored.Context.Invalidates(downstreamContext);
        }

        private static void Update()
        {
            using (UpdateMarker.Auto())
            {
                CompletePendingCheck();
                AddPendingMonitors();

                if (_activeMonitorCount == 0) return;

                _changedSlots.Clear();
                _pendingJob = new CheckWorldTransformsJob
                {
                    PreviousTransforms = _worldTransforms,
                    ActiveSlots = _activeSlots,
                    ChangedSlots = _changedSlots.AsParallelWriter(),
                }.Schedule(_transforms);
                _hasPendingJob = true;
            }
        }

        // Tests call this rather than relying on an editor repaint to advance the monitor two frames.
        internal static void UpdateForTesting()
        {
            Update();
        }

        private static void CompletePendingCheck()
        {
            if (!_hasPendingJob) return;

            _pendingJob.Complete();
            _hasPendingJob = false;
            ProcessResults();
        }

        private static void AddPendingMonitors()
        {
            foreach (var (monitored, observedPose) in PendingMonitors)
            {
                if (monitored.Transform == null
                    || CheckWorldTransformsJob.HasChanged(
                        observedPose, CheckWorldTransformsJob.ToFloat4x4(monitored.Transform.localToWorldMatrix)
                    ))
                {
                    RemoveMonitorFromLookup(monitored);
                    monitored.Context.Invalidate();
                    continue;
                }

                int slot;
                if (FreeSlots.Count > 0)
                {
                    slot = FreeSlots.Pop();
                    Slots[slot] = monitored;
                    _transforms[slot] = monitored.Transform;
                }
                else
                {
                    slot = Slots.Count;
                    EnsureCapacity(slot + 1);
                    Slots.Add(monitored);
                    _transforms.Add(monitored.Transform);
                }

                _worldTransforms[slot] = observedPose;
                _activeSlots[slot] = 1;
                _activeMonitorCount++;
            }

            PendingMonitors.Clear();
        }

        private static void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _capacity) return;

            var newCapacity = _capacity;
            while (newCapacity < requiredCapacity) newCapacity *= 2;

            _transforms.capacity = newCapacity;

            var replacementWorldTransforms =
                new NativeArray<float4x4>(newCapacity, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            var replacementActiveSlots =
                new NativeArray<byte>(newCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeArray<float4x4>.Copy(_worldTransforms, replacementWorldTransforms, Slots.Count);
            NativeArray<byte>.Copy(_activeSlots, replacementActiveSlots, Slots.Count);

            _worldTransforms.Dispose();
            _activeSlots.Dispose();
            _worldTransforms = replacementWorldTransforms;
            _activeSlots = replacementActiveSlots;
            _capacity = newCapacity;
        }

        private static void ProcessResults()
        {
            // The parallel job only queues transforms that actually changed or became invalid.
            while (_changedSlots.TryDequeue(out var slot))
            {
                Deregister(slot);
            }
        }

        private static void Deregister(int slot)
        {
            var monitored = Slots[slot];
            if (monitored == null) return;

            RemoveMonitorFromLookup(monitored);
            // Preserve slot indexes used by the native arrays and TransformAccessArray. The stale transform is
            // ignored through ActiveSlots until a later registration overwrites this slot.
            Slots[slot] = null;
            _activeSlots[slot] = 0;
            FreeSlots.Push(slot);
            _activeMonitorCount--;
            monitored.Context.Invalidate();
        }

        private static void RemoveMonitorFromLookup(MonitoredTransform monitored)
        {
            if (Monitors.TryGetValue(monitored.Transform, out var current) && ReferenceEquals(current, monitored))
            {
                Monitors.Remove(monitored.Transform);
            }
        }

        private static void Dispose()
        {
            // Processing results is harmless during teardown and keeps completion behavior identical to Update.
            CompletePendingCheck();

            if (_transforms.isCreated) _transforms.Dispose();
            if (_worldTransforms.IsCreated) _worldTransforms.Dispose();
            if (_activeSlots.IsCreated) _activeSlots.Dispose();
            if (_changedSlots.IsCreated) _changedSlots.Dispose();

            Slots.Clear();
            FreeSlots.Clear();
            PendingMonitors.Clear();
            Monitors.Clear();
            _activeMonitorCount = 0;
        }

        [BurstCompile]
        private struct CheckWorldTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float4x4> PreviousTransforms;
            [ReadOnly] public NativeArray<byte> ActiveSlots;
            public NativeQueue<int>.ParallelWriter ChangedSlots;

            public void Execute(int index, TransformAccess transform)
            {
                if (ActiveSlots[index] != 0)
                {
                    if (!transform.isValid || HasChanged(PreviousTransforms[index],
                            ToFloat4x4(transform.localToWorldMatrix)))
                    {
                        ChangedSlots.Enqueue(index);
                    }
                }
            }

            public static float4x4 ToFloat4x4(Matrix4x4 matrix)
            {
                return new float4x4(
                    new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                    new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                    new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                    new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33)
                );
            }

            public static bool HasChanged(float4x4 previous, float4x4 current)
            {
                var c0 = current.c0 - previous.c0;
                var c1 = current.c1 - previous.c1;
                var c2 = current.c2 - previous.c2;
                var c3 = current.c3 - previous.c3;
                return math.lengthsq(c0) + math.lengthsq(c1) + math.lengthsq(c2) + math.lengthsq(c3) >
                       TransformEpsilonSqr;
            }
        }
    }
}
