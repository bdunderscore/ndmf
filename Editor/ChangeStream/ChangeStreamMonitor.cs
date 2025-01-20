#region

using System;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.trace;
using UnityEditor;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

#endregion

namespace nadena.dev.ndmf.cs
{
    internal class ChangeStreamMonitor
    {
        private static readonly CustomSampler _handleEventSampler =
            CustomSampler.Create("ChangeStreamMonitor.HandleEvent");
        
        [InitializeOnLoadMethod]
        static void Init()
        {
            ObjectChangeEvents.changesPublished += OnChange;
        }

        private static void OnChange(ref ObjectChangeEventStream stream)
        {
            Profiler.BeginSample("ChangeStreamMonitor.OnChange");

            int length = stream.length;

            using (ObjectWatcher.Instance.Hierarchy.SuspendEvents())
            {
                for (int i = 0; i < length; i++)
                {
                    try
                    {
                        _handleEventSampler.Begin();
                    
                        HandleEvent(stream, i);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error handling event {i}: {e}");
                    }
                    finally
                    {
                        _handleEventSampler.End();
                    }
                }
            }

            Profiler.BeginSample("ComputeContext.FlushInvalidates");
            ComputeContext.FlushInvalidates();
            Profiler.EndSample();

            Profiler.EndSample();
        }

        private static TraceScope OpenTrace(ObjectChangeEventStream stream, int i)
        {
            return TraceBuffer.RecordTraceEvent(
                "ChangeStreamMonitor.HandleEvent",
                ev => $"Handling event {ev.Arg0}",
                stream.GetEventType(i),
                level: TraceEventLevel.Trace
            ).Scope();
        }
        
        private static void HandleEvent(ObjectChangeEventStream stream, int i)
        {
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.None: break;

                case ObjectChangeKind.ChangeScene:
                {
                    /*
                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("ChangeScene"))
                    {
                        ObjectWatcher.Instance.Hierarchy.InvalidateAll();
                    }
                    */

                    // Unfortunately there are too many spurious sources for ChangeScene - we'll have to hope that
                    // PropertyMonitor can catch unreported changes. 

                    break;
                }

                case ObjectChangeKind.CreateGameObjectHierarchy:
                {
                    stream.GetCreateGameObjectHierarchyEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("CreateGameObjectHierarchy"))
                    {
                        ObjectWatcher.Instance.Hierarchy.FireGameObjectCreate(data.instanceId);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                {
                    stream.GetChangeGameObjectStructureHierarchyEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeGameObjectStructHierarchy"))
                    {
                        OnChangeGameObjectStructureHierarchy(data);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectStructure: // add/remove components
                {
                    stream.GetChangeGameObjectStructureEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeGameObjectStructure"))
                    {
                        OnChangeGameObjectStructure(data);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectParent:
                {
                    stream.GetChangeGameObjectParentEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeGameObjectParent"))
                    {
                        OnChangeGameObjectParent(data);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                {
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeGameObjectOrComponentProperties"))
                    {
                        OnChangeGameObjectOrComponentProperties(data);
                    }

                    break;
                }

                case ObjectChangeKind.DestroyGameObjectHierarchy:
                {
                    stream.GetDestroyGameObjectHierarchyEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnDestroyGameObjectHierarchy"))
                    {
                        OnDestroyGameObjectHierarchy(data);
                    }

                    break;
                }

                case ObjectChangeKind.CreateAssetObject: break;
                case ObjectChangeKind.DestroyAssetObject:
                {
                    stream.GetDestroyAssetObjectEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnDestroyAssetObject"))
                    {
                        OnDestroyAssetObject(data);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeAssetObjectProperties:
                {
                    stream.GetChangeAssetObjectPropertiesEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeAssetObjectProperties"))
                    {
                        OnChangeAssetObjectProperties(data);
                    }

                    break;
                }

                case ObjectChangeKind.UpdatePrefabInstances:
                {
                    stream.GetUpdatePrefabInstancesEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnUpdatePrefabInstances"))
                    {
                        OnUpdatePrefabInstances(data);
                    }

                    break;
                }

                case ObjectChangeKind.ChangeChildrenOrder:
                {
                    stream.GetChangeChildrenOrderEvent(i, out var data);

                    using (OpenTrace(stream, i))
                    using (new ProfilerScope("OnChangeChildrenOrder"))
                    {
                        OnChangeChildrenOrder(data);
                    }

                    break;
                }
            }
        }

        private static void OnChangeChildrenOrder(ChangeChildrenOrderEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.FireReorderNotification(instanceId);
        }

        private static void OnUpdatePrefabInstances(UpdatePrefabInstancesEventArgs data)
        {
            foreach (var iid in data.instanceIds)
            {
                ObjectWatcher.Instance.Hierarchy.InvalidateTree(iid);
            }
        }

        private static void OnChangeAssetObjectProperties(ChangeAssetObjectPropertiesEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(instanceId);
        }

        private static void OnDestroyAssetObject(DestroyAssetObjectEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }

        private static void OnDestroyGameObjectHierarchy(DestroyGameObjectHierarchyEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }

        private static void OnChangeGameObjectOrComponentProperties(ChangeGameObjectOrComponentPropertiesEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.FireObjectChangeNotification(instanceId);
        }

        private static void OnChangeGameObjectParent(ChangeGameObjectParentEventArgs data)
        {
            var instanceId = data.instanceId;
            var priorParentId = data.previousParentInstanceId;
            var newParentId = data.newParentInstanceId;

            ObjectWatcher.Instance.Hierarchy.FireReparentNotification(instanceId);
        }

        private static void OnChangeGameObjectStructure(ChangeGameObjectStructureEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.MaybeFireStructureChangeEvent(instanceId);
        }

        private static void OnChangeGameObjectStructureHierarchy(ChangeGameObjectStructureHierarchyEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }
    }
}