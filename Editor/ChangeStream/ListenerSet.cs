#region

using System;
using UnityEditor;

#endregion

namespace nadena.dev.ndmf.rq.unity.editor
{
    internal class Listener<T> : IDisposable
    {
        internal Listener<T> _next, _prev;

        private readonly ListenerSet<T>.Filter _filter;
        private readonly WeakReference<ComputeContext> _ctx;

        internal Listener(
            ListenerSet<T>.Filter filter,
            ComputeContext ctx
        )
        {
            _next = _prev = this;
            _filter = filter;
            _ctx = ctx == null ? null : new WeakReference<ComputeContext>(ctx);
        }

        public void Dispose()
        {
            if (_next != null)
            {
                _next._prev = _prev;
                _prev._next = _next;
            }

            _next = _prev = null;
            _ctx.SetTarget(null);
        }

        internal void MaybePrune()
        {
            if (!_ctx.TryGetTarget(out var ctx) || ctx.IsInvalidated)
            {
                Dispose();
            }
        }

        // Invoked under lock(_owner)
        internal void MaybeFire(T info)
        {
            if (!_ctx.TryGetTarget(out var ctx) || ctx.IsInvalidated)
            {
                Dispose();
            }
            else if (_filter(info))
            {
                ctx.Invalidate();
                // We need to wait two frames before repainting: One to process task callbacks, then one to actually
                // repaint (and update previews).
                EditorApplication.delayCall += Delay2Repaint;
                Dispose();
            }
        }

        private void Delay2Repaint()
        {
            EditorApplication.delayCall += SceneView.RepaintAll;
        }
    }

    internal class ListenerSet<T>
    {
        public delegate bool Filter(T info);

        private Listener<T> _head;

        public ListenerSet()
        {
            _head = new Listener<T>(_ => false, null);
            _head._next = _head._prev = _head;
        }

        public bool HasListeners()
        {
            return _head._next != _head;
        }

        public IDisposable Register(Filter filter, ComputeContext ctx)
        {
            var listener = new Listener<T>(filter, ctx);

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