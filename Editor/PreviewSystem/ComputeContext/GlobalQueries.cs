#region

using System;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.runtime;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.preview
{
    public static partial class ComputeContextQueries
    {
        private static PropCache<object, ImmutableList<GameObject>> AVATAR_ROOTS =
            new(
                "ComputeContextQueries.GetAvatarRoots",
                (ctx, _ignored) =>
                {
                    var roots = ctx.GetSceneRoots();

                    var components = roots.SelectMany(root =>
                    {
                        // We are iterating scene roots, so it is okay to monitor just child components
                        // (parent components may affect avatar rootness) 
                        ObjectWatcher.Instance.MonitorGetComponents(root, ctx, true);
                        return RuntimeUtil.FindAvatarRoots(root, true);
                    });

                    return components.Where(c => ctx.ActiveInHierarchy(c)).ToImmutableList();
                },
                Enumerable.SequenceEqual
            );
        
        /// <summary>
        /// Returns a list of all root game objects in all loaded scenes. Excludes objects with
        /// nonzero hide flags.
        /// </summary>
        public static ImmutableList<GameObject> GetSceneRoots(this ComputeContext ctx)
        {
            return ObjectWatcher.Instance.MonitorSceneRoots(ctx);
        }

        /// <summary>
        /// Returns a reactive value that evaluates to a list of all components of the given type in the scene.
        /// Excludes components found on a hidden game object, or under a hidden scene root (where hidden means
        /// hideFlags are nonzero)
        /// </summary>
        /// <typeparam name="T">The type to search for</typeparam>
        /// <returns></returns>
        public static ImmutableList<T> GetComponentsByType<T>(this ComputeContext ctx) where T : class
        {
            var roots = ctx.GetSceneRoots();

            var components =
                roots.SelectMany(root => ctx.GetComponentsInChildren<T>(root, true));

            return components.ToImmutableList();
        }

        public static ImmutableList<Component> GetComponentsByType(this ComputeContext ctx, Type type)
        {
            var roots = ctx.GetSceneRoots();

            var components =
                roots.SelectMany(root => ctx.GetComponentsInChildren(root, type, true));

            return components.ToImmutableList();
        }

        public static ImmutableList<GameObject> GetAvatarRoots(this ComputeContext ctx)
        {
            return AVATAR_ROOTS.Get(ctx, AVATAR_ROOTS);
        }
    }
}