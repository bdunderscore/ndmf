using System.Collections.Generic;
using System.Linq;

namespace nadena.dev.ndmf.ReactiveQuery
{
    internal class ReactionGraph
    {
        private HashSet<Node> _nodes = new HashSet<Node>();
        
        internal class Node
        {
            private HashSet<Node> Awaits = new HashSet<Node>();
            private HashSet<Node> Invalidates = new HashSet<Node>();

            public void Invalidate(Queue<Node> toInvalidate)
            {
                foreach (var node in Awaits)
                {
                    node.Invalidates.Remove(this);
                }

                foreach (var node in Invalidates)
                {
                    toInvalidate.Enqueue(node);
                }
            }
        }

        internal void Invalidate(Node node)
        {
            var toInvalidate = new Queue<Node>();
            
            toInvalidate.Enqueue(node);
            
            while (toInvalidate.Any())
            {
                toInvalidate.Dequeue().Invalidate(toInvalidate);
            }
        }
    }
}