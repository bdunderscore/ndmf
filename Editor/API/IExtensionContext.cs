using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// The IExtensionContext is declared by custom extension contexts.
    /// </summary>
    public interface IExtensionContext
    {
        /// <summary>
        /// Invoked when the extension is activated.
        /// </summary>
        /// <param name="context"></param>
        void OnActivate(BuildContext context);

        /// <summary>
        /// Invoked when the extension is deactivated.
        /// </summary>
        /// <param name="context"></param>
        void OnDeactivate(BuildContext context);
    }

    internal static class ExtensionContextUtil
    {
        private static readonly Dictionary<Type, ImmutableList<Type>> RecursiveDependenciesCache = new();
        
        public static IEnumerable<Type> ContextDependencies(this Type ty, bool recurse)
        {
            if (recurse)
            {
                return RecursiveContextDependencies(ty);
            }

            return ContextDependencies(ty);
        }

        public static IEnumerable<Type> ContextDependencies(this Type ty)
        {
            foreach (var attr in ty.GetCustomAttributes(typeof(DependsOnContext), true))
            {
                if (attr is DependsOnContext dependsOn && dependsOn.ExtensionContext != null)
                {
                    yield return dependsOn.ExtensionContext;
                }
            }
        }

        private static ImmutableList<Type> RecursiveContextDependencies(Type ty)
        {
            if (RecursiveDependenciesCache.TryGetValue(ty, out var cached))
            {
                return cached;
            }

            var result = RecursiveContextDependencies0(ty).ToImmutableList();
            RecursiveDependenciesCache[ty] = result;

            return result;
        }

        private static IEnumerable<Type> RecursiveContextDependencies0(Type ty)
        {
            HashSet<Type> enqueued = new();
            Queue<Type> queue = new();

            queue.Enqueue(ty);
            enqueued.Add(ty);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                yield return current;

                foreach (var dep in ContextDependencies(current))
                {
                    if (enqueued.Add(dep))
                    {
                        queue.Enqueue(dep);
                    }
                }
            }
        }
    }
}