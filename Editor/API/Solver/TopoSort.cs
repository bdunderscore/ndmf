#region

using System;
using System.Collections.Generic;
using nadena.dev.ndmf.model;

#endregion

namespace nadena.dev.ndmf
{
    internal static class TopoSort
    {
        public static List<T> DoSort<T>(
            IEnumerable<T> Values,
            IEnumerable<(T, T, ConstraintType)> OrderingConstraints
        )
        {
            Dictionary<T, TopoPass<T>> passes = new Dictionary<T, TopoPass<T>>();

            int fallbackSort = 0;
            foreach (var value in Values)
            {
                passes[value] = new TopoPass<T>(value, fallbackSort++);
            }

            foreach (var (first, second, type) in OrderingConstraints)
            {
                passes[first].AddConstraint(passes[second], type);
            }

            HashSet<TopoPass<T>> notExecuted = new HashSet<TopoPass<T>>(passes.Values);
            SortedSet<TopoPass<T>> ready = new SortedSet<TopoPass<T>>(new TopoPassComparer<T>());
            Stack<TopoPass<T>> priorityStack = new Stack<TopoPass<T>>();

            foreach (var pass in passes)
            {
                if (pass.Value.IsReady)
                {
                    ready.Add(pass.Value);
                }
            }

            var order = new List<T>();

            while (ready.Count > 0)
            {
                TopoPass<T> nextPass = null;

                while (priorityStack.Count > 0 && priorityStack.Peek().CanRetire)
                {
                    priorityStack.Pop();
                }

                if (priorityStack.Count > 0)
                {
                    nextPass = priorityStack.Peek().NextPriorityPass();
                }

                nextPass = nextPass ?? ready.Min;

                ready.Remove(nextPass);
                notExecuted.Remove(nextPass);
                order.Add(nextPass.Pass);

                foreach (var nowReady in nextPass.Schedule())
                {
                    ready.Add(nowReady);
                }

                priorityStack.Push(nextPass);
            }

            if (notExecuted.Count > 0)
            {
                throw new Exception("Constraint loop detected");
            }

            return order;
        }
    }

    internal class TopoPassComparer<T> : IComparer<TopoPass<T>>
    {
        public int Compare(TopoPass<T> x, TopoPass<T> y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return x.FallbackOrder.CompareTo(y.FallbackOrder);
        }
    }
}