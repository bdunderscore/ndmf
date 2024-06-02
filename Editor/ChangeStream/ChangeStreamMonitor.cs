#region

using System;
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    internal class ChangeStreamMonitor
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            ObjectChangeEvents.changesPublished += OnChange;
        }

        private static void OnChange(ref ObjectChangeEventStream stream)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int length = stream.length;
            for (int i = 0; i < length; i++)
            {
                try
                {
                    HandleEvent(stream, i);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error handling event {i}: {e}");
                }
            }

            sw.Stop();
            Debug.Log($"Handled {length} events in {sw.ElapsedMilliseconds}ms");
        }

        private static void HandleEvent(ObjectChangeEventStream stream, int i)
        {
            Debug.Log("HandleEvent: " + stream.GetEventType(i));
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.None: break;

                case ObjectChangeKind.ChangeScene:
                {
                    ObjectWatcher.Instance.Hierarchy.InvalidateAll();

                    break;
                }

                case ObjectChangeKind.CreateGameObjectHierarchy:
                {
                    stream.GetCreateGameObjectHierarchyEvent(i, out var data);

                    ObjectWatcher.Instance.Hierarchy.FireGameObjectCreate(data.instanceId);
                    break;
                }

                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                {
                    stream.GetChangeGameObjectStructureHierarchyEvent(i, out var data);

                    OnChangeGameObjectStructureHierarchy(data);

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectStructure: // add/remove components
                {
                    stream.GetChangeGameObjectStructureEvent(i, out var data);
                    OnChangeGameObjectStructure(data);

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectParent:
                {
                    stream.GetChangeGameObjectParentEvent(i, out var data);
                    OnChangeGameObjectParent(data);

                    break;
                }

                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                {
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var data);
                    OnChangeGameObjectOrComponentProperties(data);

                    break;
                }

                case ObjectChangeKind.DestroyGameObjectHierarchy:
                {
                    stream.GetDestroyGameObjectHierarchyEvent(i, out var data);
                    OnDestroyGameObjectHierarchy(data);

                    break;
                }

                case ObjectChangeKind.CreateAssetObject: break;
                case ObjectChangeKind.DestroyAssetObject:
                {
                    stream.GetDestroyAssetObjectEvent(i, out var data);
                    OnDestroyAssetObject(data);

                    break;
                }

                case ObjectChangeKind.ChangeAssetObjectProperties:
                {
                    stream.GetChangeAssetObjectPropertiesEvent(i, out var data);
                    OnChangeAssetObjectProperties(data);

                    break;
                }

                case ObjectChangeKind.UpdatePrefabInstances:
                {
                    stream.GetUpdatePrefabInstancesEvent(i, out var data);
                    OnUpdatePrefabInstances(data);

                    break;
                }

                case ObjectChangeKind.ChangeChildrenOrder:
                {
                    stream.GetChangeChildrenOrderEvent(i, out var data);
                    OnChangeChildrenOrder(data);

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

            ObjectWatcher.Instance.Hierarchy.FireStructureChangeEvent(instanceId);
        }

        private static void OnChangeGameObjectStructureHierarchy(ChangeGameObjectStructureHierarchyEventArgs data)
        {
            // TODO - we need to record original parent/child relationships so we can fire off notifications
            // for everything that was previously present.

            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }

        private void OnCreateGameObjectHierarchy(CreateGameObjectHierarchyEventArgs data)
        {
            var instanceId = data.instanceId;

            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }
    }
}