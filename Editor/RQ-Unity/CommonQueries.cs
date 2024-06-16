#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    public static class CommonQueries
    {
        /// <summary>
        /// Reactive value evaluating to a list of all root game objects in all loaded scenes. Excludes objects with
        /// nonzero hide flags.
        /// </summary>
        public static ReactiveValue<ImmutableList<GameObject>> SceneRoots { get; }
            = ReactiveValue<ImmutableList<GameObject>>.Create("SceneRoots",
                ctx =>
                {
                    var invalidate = ctx.Invalidate;
                    var onInvalidate = ctx.OnInvalidate;

                    var roots = ObjectWatcher.Instance.MonitorSceneRoots(out var dispose, _ => invalidate(),
                        onInvalidate);
                    onInvalidate.ContinueWith(_ => dispose.Dispose());

                    return Task.FromResult(roots);
                });

        private static Dictionary<Type, object /* ReactiveQuery<T> */> _builderCache = new();
        
        private static ReactiveQuery<Type, ImmutableList<Component>> _componentsByType
            = new("ComponentsByType",
                async (ctx, type) =>
                {
                    var roots = await ctx.Observe(SceneRoots);

                    IEnumerable<Component> components =
                        roots.SelectMany(root => ctx.GetComponentsInChildren(root, type, true));

                    return components.ToImmutableList();
                });

        /// <summary>
        /// Returns a reactive value that evaluates to a list of all components of the given type in the scene.
        /// Excludes components found on a hidden game object, or under a hidden scene root (where hidden means
        /// hideFlags are nonzero)
        /// </summary>
        /// <typeparam name="T">The type to search for</typeparam>
        /// <returns></returns>
        public static ReactiveValue<ImmutableList<T>> GetComponentsByType<T>() where T : Component
        {
            if (!_builderCache.TryGetValue(typeof(T), out var builder))
            {
                _builderCache[typeof(T)] = builder = ReactiveValue<ImmutableList<T>>.Create(
                    "ComponentsByType: " + typeof(T),
                    async ctx =>
                    {
                        var roots = await ctx.Observe(SceneRoots);

                        IEnumerable<T> components =
                            roots.SelectMany(root => ctx.GetComponentsInChildren<T>(root, true));

                        return components.ToImmutableList();
                    });
            }

            return (ReactiveValue<ImmutableList<T>>)builder;
        }

        public static ReactiveValue<ImmutableList<Component>> GetComponentsByType(Type type)
        {
            return _componentsByType.Get(type);
        }
    }
}