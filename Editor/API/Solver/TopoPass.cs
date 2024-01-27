#region

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.model;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// State information for the topological sort phase
    /// </summary>
    internal class TopoPass<T>
    {
        internal T Pass { get; }
        internal int FallbackOrder;

        private HashSet<TopoPass<T>> _predecessors = new HashSet<TopoPass<T>>();
        private HashSet<TopoPass<T>> _remainingPredecessors = new HashSet<TopoPass<T>>();

        private Dictionary<TopoPass<T>, ConstraintType> _remainingSuccessors =
            new Dictionary<TopoPass<T>, ConstraintType>();

        internal bool IsReady => _remainingPredecessors.Count == 0;
        internal bool CanRetire => _remainingSuccessors.Values.All(ty => ty == ConstraintType.WeakOrder);

        public TopoPass(T pass, int fallbackOrder)
        {
            Pass = pass;
            FallbackOrder = fallbackOrder;
        }

        internal void AddConstraint(TopoPass<T> after, ConstraintType type)
        {
            after._predecessors.Add(this);
            after._remainingPredecessors.Add(this);

            if (_remainingSuccessors.TryGetValue(after, out var curType))
            {
                if (curType > type) return;
            }

            _remainingSuccessors[after] = type;
        }

        public TopoPass<T> NextPriorityPass()
        {
            TopoPass<T> bestPass = null;
            ConstraintType bestType = ConstraintType.WeakOrder;
            int bestFallback = Int32.MaxValue;
            ;

            foreach (var kvp in _remainingSuccessors)
            {
                if (kvp.Value != ConstraintType.WeakOrder && kvp.Key.IsReady)
                {
                    if (bestPass == null || kvp.Value > bestType ||
                        (kvp.Value == bestType && kvp.Key.FallbackOrder < bestFallback))
                    {
                        bestPass = kvp.Key;
                        bestType = kvp.Value;
                        bestFallback = kvp.Key.FallbackOrder;
                    }
                }
            }

            return bestPass;
        }

        public IEnumerable<TopoPass<T>> Schedule()
        {
            // Upon scheduling, we delete ourselves from our successors' remainingPredecessors sets and our predecessors'
            // remainingSuccessors, and yield anything that is now ready to execute

            foreach (var predecessor in _predecessors)
            {
                predecessor._remainingSuccessors.Remove(this);
            }

            foreach (var successor in _remainingSuccessors.Keys)
            {
                var wasReady = successor.IsReady;
                successor._remainingPredecessors.Remove(this);
                if (successor.IsReady && !wasReady) yield return successor;
            }
        }
    }
}