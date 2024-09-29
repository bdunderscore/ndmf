#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.preview.trace;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.cs
{
    internal class ListenerSet<T>
    {
        public delegate bool Filter(T info);

        private static readonly Action<object> InvalidateContextAction = InvalidateContext;

        private static void InvalidateContext(object ctx)
        {
            ((ComputeContext)ctx).Invalidate();
        }

        private sealed class Listener : IDisposable
        {
            private readonly ListenerSet<T> _owner;
            private readonly WeakReference<object> _targetRef;
            private readonly Action<object> _receiver;
            private readonly Filter _filter;
            private readonly int _targetIdentityHashCode;

            public Listener(ListenerSet<T> owner, object target, Action<object> receiver, Filter filter)
            {
                _owner = owner;
                _targetRef = new WeakReference<object>(target);
                _receiver = receiver;
                _filter = filter;
                _targetIdentityHashCode = RuntimeHelpers.GetHashCode(target);
            }

            public void Dispose()
            {
                _owner.Deregister(this);
            }

            /// <summary>
            ///     Attempts to fire this listener. Returns true if the listener has been expended.
            /// </summary>
            /// <param name="ev"></param>
            /// <returns></returns>
            public bool TryFire(T ev)
            {
                if (_targetRef.TryGetTarget(out var target))
                {
                    if (TargetIsExpended(target))
                    {
                        TraceBuffer.RecordTraceEvent(
                            eventType: "ListenerSet.Expired",
                            formatEvent: e => $"Listener for {e.Arg0} expired",
                            arg0: target
                        );
                        return true;
                    }

                    try
                    {
                        if (!_filter(ev))
                        {
                            return false;
                        }

                        var tev = TraceBuffer.RecordTraceEvent(
                            eventType: "ListenerSet.Fire",
                            formatEvent: e => $"Listener for {e.Arg0} fired with {e.Arg1}",
                            arg0: target,
                            arg1: ev
                        );
                        using (tev.Scope())
                        {
                            _receiver(target);
                        }

                        RepaintTrigger.RequestRepaint();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        return true;
                    }

                    return true;
                }
                else
                {
                    TraceBuffer.RecordTraceEvent(
                        eventType: "ListenerSet.GC",
                        formatEvent: e => "Listener expired"
                    );
                }

                return true;
            }

            public bool TryPrune()
            {
                return !_targetRef.TryGetTarget(out var target) || TargetIsExpended(target);
            }

            public void ForceFire()
            {
                if (_targetRef.TryGetTarget(out var target))
                {
                    _receiver(target);

                    RepaintTrigger.RequestRepaint();
                }
            }

            private bool TargetIsExpended(object target)
            {
                return target is ComputeContext ctx && _receiver == InvalidateContext && ctx.IsInvalidated;
            }

            public override string ToString()
            {
                if (_targetRef.TryGetTarget(out var target)) return $"Listener for {target}";

                return "Listener (GC'd)";
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_targetIdentityHashCode, _receiver.GetHashCode(), _filter.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj)) return true;
                if (!(obj is Listener other)) return false;
                if (_targetIdentityHashCode != other._targetIdentityHashCode) return false;
                if (_receiver != other._receiver) return false;
                if (_filter != other._filter) return false;

                if (_targetRef.TryGetTarget(out var target) && other._targetRef.TryGetTarget(out var otherTarget))
                    return ReferenceEquals(target, otherTarget);
                return false;
            }
        }

        private int highWater;
        private HashSet<Listener> _listeners;

        private void Deregister(Listener l)
        {
            lock (this)
            {
                _listeners.Remove(l);
            }
        }
        
        public ListenerSet()
        {
            highWater = 4;
        }

        public bool HasListeners()
        {
            lock (this)
            {
                return _listeners != null && _listeners.Count > 0;
            }
        }

        public IDisposable Register(Filter filter, ComputeContext ctx)
        {
            var listener = new Listener(this, ctx, InvalidateContextAction, filter);

            lock (this)
            {
                if (_listeners == null) _listeners = new HashSet<Listener>();

                _listeners.Add(listener);
                MaybePrune();
            }

            return listener;
        }
        
        public IDisposable Register(Filter filter, object target, Action<object> receiver)
        {
            var listener = new Listener(this, target, receiver, filter);

            lock (this)
            {
                if (_listeners == null) _listeners = new HashSet<Listener>();

                _listeners.Add(listener);
                MaybePrune();
            }

            return listener;
        }

        public IDisposable Register(ComputeContext ctx)
        {
            return Register(PassAll, ctx);
        }
        
        public IDisposable Register(object target, Action<object> receiver)
        {
            return Register(PassAll, target, receiver);
        }
        
        private static bool PassAll(T _) => true;

        public void Fire(T info)
        {
            lock (this)
            {
                if (_listeners == null || _listeners.Count == 0) return;

                // It's possible we might recurse back and try to register listeners while this is running.
                // To help avoid issues here, we create a new set, swap the two, and remove on the old set. If we
                // find there were listeners registered, we merge them after the Fire call completes.

                var tmp = _listeners;
                _listeners = null;

                tmp.RemoveWhere(l => l.TryFire(info));

                if (_listeners != null)
                    _listeners.UnionWith(tmp);
                else
                    _listeners = tmp;
            }
        }

        private void MaybePrune()
        {
            lock (this)
            {
                if (_listeners == null) return;
                if (_listeners.Count < highWater) return;

                _listeners.RemoveWhere(l => l.TryPrune());

                highWater = Math.Max(highWater, _listeners.Count * 2);
            }
        }
        
        internal IEnumerable<string> GetListeners()
        {
            lock (this)
            {
                if (_listeners == null) return Enumerable.Empty<string>();

                return _listeners.Select(l => l.ToString()).ToList();
            }
        }

        public void FireAll()
        {
            lock (this)
            {
                if (_listeners == null) return;

                var tmp = _listeners;
                _listeners = null;

                foreach (var listener in tmp) listener.ForceFire();

                if (_listeners == null)
                {
                    tmp.Clear();
                    _listeners = tmp;
                }
            }
        }
    }
}