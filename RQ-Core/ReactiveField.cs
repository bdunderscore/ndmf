#region

using System.Threading.Tasks;

#endregion

namespace nadena.dev.ndmf.rq
{
    public sealed class ReactiveField<T>
    {
        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                _value = value;
                _reactiveValue.Invalidate();
            }
        }

        private ReactiveValue<T> _reactiveValue;

        public ReactiveValue<T> AsReactiveValue()
        {
            return _reactiveValue;
        }

        public ReactiveField(T value)
        {
            _value = value;
            _reactiveValue = ReactiveValue<T>.Create("reactive field", _ => Task.FromResult(_value));
        }
    }
}