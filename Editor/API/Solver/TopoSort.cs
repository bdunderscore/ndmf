using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Graphs;

namespace nadena.dev.ndmf
{
    public static class TopoSort
    {
        private class Node<T, O> : IComparable<Node<T, O>> where O: IComparable<O>
        {
            public T obj;
            public O FallbackOrder;
            public HashSet<T> Awaiting = new HashSet<T>();
            public List<Node<T, O>> Blocking = new List<Node<T, O>>();

            public Node(T obj, O index)
            {
                this.obj = obj;
                FallbackOrder = index;
            }

            public int CompareTo(Node<T, O> other)
            {
                if (ReferenceEquals(this, other)) return 0;
                if (ReferenceEquals(null, other)) return 1;
                return FallbackOrder.CompareTo(other.FallbackOrder);
            }
        }

        public static List<T> DoSort<T>(
            IEnumerable<T> Values,
            IEnumerable<(T, T)> OrderingConstraints
        )
        {
            SortedSet<Node<T, int>> Ready = new SortedSet<Node<T, int>>();
            Dictionary<T, Node<T, int>> Nodes = new Dictionary<T, Node<T, int>>();
            int i = 0;
            foreach (var val in Values)
            {
                var node = new Node<T, int>(val, i);
                Nodes[val] = node;
                Ready.Add(node);
                i++;
            }

            foreach (var (before, after) in OrderingConstraints)
            {
                if (!Nodes.ContainsKey(before))
                {
                    throw new Exception($"No 'before' node for constraint ({before}, {after})");
                }

                if (!Nodes.ContainsKey(after))
                {
                    throw new Exception($"No 'after' node for constraint ({before}, {after})");
                }

                Nodes[before].Blocking.Add(Nodes[after]);
                Nodes[after].Awaiting.Add(before);
                Ready.Remove(Nodes[after]);
            }

            List<T> Sorted = new List<T>();
            while (Ready.Count > 0)
            {
                var next = Ready.First();
                Ready.Remove(next);

                Sorted.Add(next.obj);
                foreach (var successor in next.Blocking)
                {
                    successor.Awaiting.Remove(next.obj);
                    if (successor.Awaiting.Count == 0)
                    {
                        Ready.Add(successor);
                    }
                }
            }

            if (Sorted.Count < Nodes.Count)
            {
                // TODO more useful error message
                throw new Exception("Cycle detected");
            }

            return Sorted;
        }
    }
}