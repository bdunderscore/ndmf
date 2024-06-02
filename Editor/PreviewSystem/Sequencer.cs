#region

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace nadena.dev.ndmf.preview
{
    internal sealed class Sequencer
    {
        private readonly HashSet<SequencePoint> _discovered = new();
        private readonly List<SequencePoint> _discoveryOrder = new();
        private readonly Dictionary<SequencePoint, int> _resolvedOrder = new();

        public IEnumerable<SequencePoint> Sequence => _resolvedOrder
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToImmutableList();

        public Sequencer()
        {
        }

        public void SetSequence(IEnumerable<SequencePoint> points)
        {
            _resolvedOrder.Clear();

            foreach (var point in points)
            {
                if (_discovered.Add(point))
                {
                    _discoveryOrder.Add(point);
                }

                _resolvedOrder[point] = _resolvedOrder.Count;
            }

            foreach (var point in _discoveryOrder)
            {
                if (!_resolvedOrder.ContainsKey(point))
                {
                    _resolvedOrder[point] = _resolvedOrder.Count;
                }
            }
        }

        public void AddPoint(SequencePoint point)
        {
            if (_discovered.Add(point))
            {
                _discoveryOrder.Add(point);
                _resolvedOrder[point] = _resolvedOrder.Count;
            }
        }
    }
}