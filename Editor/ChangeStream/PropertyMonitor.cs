using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.cs
{
    internal class PropertyMonitor
    {
        private static readonly long RECHECK_TIMESLICE = 2 * (Stopwatch.Frequency / 1000);
        private Task _activeRefreshTask = Task.CompletedTask;
        private Task _pendingRefreshTask = Task.CompletedTask;

        private bool _isEnabled;
        internal bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                
                _isEnabled = value;

                if (_isEnabled)
                {
                    using (var scope = NDMFSyncContext.Scope())
                    {

                        var curRefreshTask = _activeRefreshTask;
                        _activeRefreshTask = Task.Factory.StartNew(
                            async () =>
                            {
                                await curRefreshTask;
                                await CheckAllObjectsLoop();
                            },
                            CancellationToken.None,
                            TaskCreationOptions.None,
                            TaskScheduler.FromCurrentSynchronizationContext()
                        ).Unwrap();
                    }
                }
            }
        }

        internal void MaybeStartRefreshTimer()
        {
            IsEnabled = true;
        }

        public enum PropertyMonitorEvent
        {
            PropsUpdated
        }

        private class Registration
        {
            internal readonly ListenerSet<PropertyMonitorEvent> _listeners = new();
            internal readonly Object _obj;

            public Registration(Object obj)
            {
                _obj = obj;
            }
        }

        private readonly SortedDictionary<int, Registration> _registeredObjects = new();

        public ListenerSet<PropertyMonitorEvent> MonitorObjectProps(Object obj)
        {
            if (_registeredObjects.TryGetValue(obj.GetInstanceID(), out var reg)) return reg._listeners;

            reg = new Registration(obj);

            _registeredObjects.Add(obj.GetInstanceID(), reg);

            return reg._listeners;
        }

        private async Task CheckAllObjectsLoop()
        {
            while (_isEnabled)
            {
                await CheckAllObjects();
                await NextFrame();
            }
        }

        public async Task CheckAllObjects()
        {
            try
            {
                Profiler.BeginSample("PropertyMonitor.CheckAllObjects");
                var toRemove = new List<int>();
                var sw = new Stopwatch();
                sw.Start();


                foreach (var pair in _registeredObjects.ToList())
                {
                    if (!_isEnabled) break;
                    
                    var (instanceId, reg) = pair;

                    if (reg._obj is GameObject)
                    {
                        ObjectWatcher.Instance.Hierarchy.RequestComponentStructureCheck(instanceId);
                    }

                    // Wake up all listeners to see if their monitored value has changed
                    Profiler.BeginSample("FirePropsUpdated", EditorUtility.InstanceIDToObject(instanceId));
                    reg._listeners.Fire(PropertyMonitorEvent.PropsUpdated);
                    Profiler.EndSample();

                    if (!reg._listeners.HasListeners() || reg._obj == null) toRemove.Add(instanceId);

                    if (sw.ElapsedTicks > RECHECK_TIMESLICE)
                    {
                        Profiler.EndSample();
                        await Yield();

                        Profiler.BeginSample("PropertyMonitor.CheckAllObjects.Continued");
                        sw.Restart();
                    }
                }

                foreach (var id in toRemove) _registeredObjects.Remove(id);

                Profiler.EndSample();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static async Task Yield()
        {
            var tcs = new TaskCompletionSource<bool>();
            NDMFSyncContext.Context.Post(tcs_ => ((TaskCompletionSource<bool>)tcs_).TrySetResult(true), tcs);

            await tcs.Task;
        }

        private static async Task NextFrame()
        {
            var tcs = new TaskCompletionSource<bool>();

            // Waking up the editor application every frame (forcing frame processing) can be heavyweight, so
            // only do it if the editor is focused (so the user is actively interacting) or we're in animation mode
            // (which could change things without triggering change events)
            if (EditorApplication.isFocused || AnimationMode.InAnimationMode())
            {
                EditorApplication.CallbackFunction cf = default;

                cf = () =>
                {
                    tcs.TrySetResult(true);
                    EditorApplication.update -= cf;
                };

                EditorApplication.update += cf;
            }
            else
            {
                // Wait for focus
                Action<bool> focusFunc = default;
                focusFunc = _ =>
                {
                    tcs.TrySetResult(true);
                    EditorApplication.focusChanged -= focusFunc;
                };
                EditorApplication.focusChanged += focusFunc;
            }

            await tcs.Task;
        }
    }
}