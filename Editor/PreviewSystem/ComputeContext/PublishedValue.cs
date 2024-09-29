using System;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.cs;
using nadena.dev.ndmf.preview.trace;
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

        public event Action<T> OnChange;
        public string DebugName;

        public T Value
        {
            get => _value;
            set
            {
                if (ReferenceEquals(_value, value)) return;
                
                var ev = TraceBuffer.RecordTraceEvent(
                    "PublishedValue.Set",
                    (ev) => $"[PublishedValue/{ev.Arg0}] Set value to {ev.Arg1}",
                    DebugName,
                    value
                );

                using (ev.Scope())
                {
                    _value = value;
                    _listeners.Fire(null);
                    var listeners = OnChange;
                    OnChange = default;

                    listeners?.Invoke(value);
                    
                    RepaintTrigger.RequestRepaint();
                }
            }
        }

        public void SetWithoutNotify(T value)
        {
            _value = value;
        }

        public PublishedValue(T value, string debugName = null)
        {
            _value = value;
            DebugName = debugName ?? typeof(T).Name;
        }

        private readonly ListenerSet<object> _listeners = new();

        internal R Observe<R>(
            ComputeContext context,
            Func<T, R> extract,
            Func<R, R, bool> eq,
            [CallerFilePath] string callerPath = "",
            [CallerLineNumber] int callerLine = 0
        )
        {
            var initialValue = extract(_value);

            _listeners.Register(_ =>
            {
                try
                {
                    if (eq(initialValue, extract(_value)))
                    {
                        TraceBuffer.RecordTraceEvent(
                            "PublishedValue.Observe",
                            (ev) => $"[PublishedValue/{ev.Arg0}] No change detected",
                            DebugName
                        );
                        return false;
                    }

                    return true;
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