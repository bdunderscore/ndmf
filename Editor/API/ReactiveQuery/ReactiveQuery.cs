using System;
using System.Threading.Tasks;

namespace nadena.dev.ndmf.ReactiveQuery
{
    public abstract class ReactiveQuery<T>
    {
        #region State

        private ReactionGraph _graph;
        private ReactionGraph.Node _node = new ReactionGraph.Node();
        
        #endregion
        
        #region Public API
        /// <summary>
        /// Invoked when the reactive query's value changes. Return true to continue listening, false to stop.
        /// The provided ValueTask is guaranteed to be already completed (but may also contain an exception if the
        /// query failed).
        /// </summary>
        public delegate bool DelegateOnUpdate(T? value, Exception? exception);
        
        /// <summary>
        /// Requests the value of the query. This will start computing the query in the background if not already
        /// available, but might call the delegate inline if the value is available now.
        /// </summary>
        /// <param name="onUpdate"></param>
        public void Listen(Reaction<T> reaction)
        {
            throw new System.NotImplementedException();
        }
        #endregion
        
        
        #region Subclass API
        protected ValueTask<T> Observe(ReactiveQuery<T> query)
        {
            throw new System.NotImplementedException();
        }
        
        protected U Observe<U>(U unityObject) where U : UnityEngine.Object
        {
            return unityObject;
        }
        
        protected abstract T Compute();

        protected virtual void DestroyObsoleteValue(T value)
        {
            // no-op
        } 
        #endregion
        
        #region Internal API

        internal void Invalidate()
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}