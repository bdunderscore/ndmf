using System;
using nadena.dev.ndmf.cs;
using UnityEngine;

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     A holder of a value which can be subscribed to (using ComputeContext) to receive invalidation events when the
    ///     value changes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class PublishedValue<T>
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                if (ReferenceEquals(_value, value)) return;
                _value = value;
                _listeners.Fire(null);
            }
        }

        public PublishedValue(T value)
        {
            _value = value;
        }

        private readonly ListenerSet<object> _listeners = new();

        internal R Observe<R>(
            ComputeContext context,
            Func<T, R> extract,
            Func<R, R, bool> eq
        )
        {
            var initialValue = extract(_value);

            _listeners.Register(_ =>
            {
                try
                {
                    return !eq(initialValue, extract(_value));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return false;
                }
            }, context);

            return initialValue;
        }
    }
}