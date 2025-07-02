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

        public static IEnumerable<Type> CompatibleContexts(this Type ty, bool recurse)
        {
            var visited = new HashSet<Type>();
            var queue = new Queue<Type>();

            queue.Enqueue(ty);

            foreach (var attr in ty.GetCustomAttributes(typeof(CompatibleWithContext), true))
            {
                if (attr is CompatibleWithContext compatible && compatible.ExtensionContext != null)
                {
                    if (visited.Add(compatible.ExtensionContext))
                    {
                        yield return compatible.ExtensionContext;
                        if (recurse) queue.Enqueue(compatible.ExtensionContext);
                    }
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var attr in current.GetCustomAttributes(typeof(DependsOnContext), true))
                {
                    if (attr is DependsOnContext dependsOn && dependsOn.ExtensionContext != null)
                    {
                        if (visited.Add(dependsOn.ExtensionContext))
                        {
                            yield return dependsOn.ExtensionContext;
                            if (recurse) queue.Enqueue(dependsOn.ExtensionContext);
                        }
                    }
                }
            }
        }

        public static IEnumerable<Type> RequiredContexts(this Type ty, bool recurse)
        {
            if (recurse)
            {
                return RecursiveContextDependencies(ty);
            }

            return RequiredContexts(ty);
        }

        public static IEnumerable<Type> RequiredContexts(this Type ty)
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

                foreach (var dep in RequiredContexts(current))
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