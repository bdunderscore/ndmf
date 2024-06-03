#region

using System;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    internal class Listener<T> : IDisposable
    {
        private ListenerSet<T> _owner;
        internal Listener<T> _next, _prev;

        private readonly ListenerSet<T>.Invokee _callback;
        private readonly WeakReference<object> _param;

        internal Listener(
            ListenerSet<T> owner,
            ListenerSet<T>.Invokee callback,
            object param
        )
        {
            _owner = owner;
            _next = _prev = this;
            _callback = callback;
            _param = new WeakReference<object>(param);
        }

        public void Dispose()
        {
            if (_next != null)
            {
                _next._prev = _prev;
                _prev._next = _next;
            }

            _next = _prev = null;
            _param.SetTarget(null);
        }

        internal void MaybePrune()
        {
            if (!_param.TryGetTarget(out _))
            {
                Dispose();
            }
        }

        // Invoked under lock(_owner)
        internal void MaybeFire(T info)
        {
            if (!_param.TryGetTarget(out var target) || _callback(target, info))
            {
                Dispose();
            }
        }
    }

    internal class ListenerSet<T>
    {
        public delegate bool Invokee(object target, T info);

        private Listener<T> _head;

        public ListenerSet()
        {
            _head = new Listener<T>(this, (object _, T _) => false, null);
            _head._next = _head._prev = _head;
        }

        public bool HasListeners()
        {
            return _head._next != _head;
        }

        public IDisposable Register(Invokee callback, object param)
        {
            var listener = new Listener<T>(this, callback, param);

            listener._next = _head._next;
            listener._prev = _head;
            _head._next._prev = listener;
            _head._next = listener;

            return listener;
        }

        public void Fire(T info)
        {
            for (var listener = _head._next; listener != _head;)
            {
                var next = listener._next;
                listener.MaybeFire(info);
                listener = next;
            }
        }

        public void Prune()
        {
            for (var listener = _head._next; listener != _head;)
            {
                var next = listener._next;
                listener.MaybePrune();
                listener = next;
            }
        }
    }
}