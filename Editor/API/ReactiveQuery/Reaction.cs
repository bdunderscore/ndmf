using System;

namespace nadena.dev.ndmf.ReactiveQuery
{
    public interface Reaction<T>
    {
        public void OnValueUpdated(T value);
        public void OnQueryFailed(Exception e);
        public void OnRecalculateRequested();
        
        public bool StillListening { get; } 
    }
}