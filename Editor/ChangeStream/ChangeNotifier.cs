#region

using System.Collections.Generic;
using JetBrains.Annotations;
using nadena.dev.ndmf.cs;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    [PublicAPI]
    public static class ChangeNotifier
    {
        private static Dictionary<string, HashSet<int>> pathToInstanceIds = new();

        internal static void RecordObjectOfInterest(Object obj)
        {
            if (!AssetDatabase.Contains(obj)) return;

            var path = AssetDatabase.GetAssetPath(obj);
            if (!pathToInstanceIds.TryGetValue(path, out var set))
            {
                set = new HashSet<int>();
                pathToInstanceIds[path] = set;
            }

            set.Add(obj.GetInstanceID());
        }
        
        /// <summary>
        /// Notifies the reactive query and NDMF preview system of a change in an object that isn't tracked by the normal
        /// unity ObjectchangeEventStream system.
        /// </summary>
        /// <param name="obj"></param>
        public static void NotifyObjectUpdate(Object obj)
        {
            if (obj != null) ObjectWatcher.Instance.Hierarchy.InvalidateTree(obj.GetInstanceID());
        }

        /// <summary>
        /// Notifies the reactive query and NDMF preview system of a change in an object that isn't tracked by the normal
        /// unity ObjectchangeEventStream system.
        /// </summary>
        /// <param name="instanceId"></param>
        public static void NotifyObjectUpdate(int instanceId)
        {
            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
        }

        private static void NotifyAssetFileChange(string path)
        {
            if (pathToInstanceIds.TryGetValue(path, out var set))
            {
                foreach (var instanceId in set)
                {
                    NotifyObjectUpdate(instanceId);
                }
            }
        }

        private class Processor : AssetPostprocessor
        {
            [UsedImplicitly]
            static void OnPostprocessAllAssets(
                // ReSharper disable once Unity.IncorrectMethodSignature
                string[] importedAssets,
                // ReSharper disable once UnusedParameter.Local
                string[] deletedAssets,
                // ReSharper disable once UnusedParameter.Local
                string[] movedAssets,
                // ReSharper disable once UnusedParameter.Local
                string[] movedFromAssetPaths,
                // ReSharper disable once UnusedParameter.Local
                bool didDomainReload
            )
            {
                using var _ = ObjectWatcher.Instance.Hierarchy.SuspendEvents();

                foreach (var path in importedAssets)
                {
                    NotifyAssetFileChange(path);
                }

                foreach (var path in deletedAssets)
                {
                    NotifyAssetFileChange(path);
                }
            }
        }
    }
}