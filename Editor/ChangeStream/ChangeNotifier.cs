#region

using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    public static class ChangeNotifier
    {
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
        /// <param name="obj"></param>
        public static void NotifyObjectUpdate(int instanceId)
        {
            ObjectWatcher.Instance.Hierarchy.InvalidateTree(instanceId);
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
                foreach (var asset in importedAssets)
                {
                    if (asset.EndsWith(".unity")) continue;
                    var subassets = AssetDatabase.LoadAllAssetsAtPath(asset);
                    foreach (var subasset in subassets)
                    {
                        NotifyObjectUpdate(subasset);
                    }
                }
            }
        }
    }
}