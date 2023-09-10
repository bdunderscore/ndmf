using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace nadena.dev.ndmf.fluent
{
    public sealed partial class Sequence
    {        
        private ImmutableHashSet<Type> _requiredExtensions = ImmutableHashSet<Type>.Empty;
        private ImmutableHashSet<string> _compatibleExtensions = ImmutableHashSet<string>.Empty;

        private class DisposableAction : IDisposable
        {
            private readonly Action _action;

            public DisposableAction(Action action)
            {
                _action = action;
            }
            
            public void Dispose()
            {
                _action();
            }
        }
        
        public void WithCompatibleExtensions(IEnumerable<string> extensions, Action<Sequence> action)
        {
            var prior = _compatibleExtensions;
            _compatibleExtensions = _compatibleExtensions.Union(extensions);

            try
            {
                action(this);
            }
            finally
            {
                _compatibleExtensions = prior;
            }
        }
        
        public void WithCompatibleExtensions(IEnumerable<Type> extensions, Action<Sequence> action)
        {
            WithCompatibleExtensions(extensions.Select(t => t.FullName), action);
        } 
        
        public void WithCompatibleExtension(string extension, Action<Sequence> action)
        {
            WithCompatibleExtensions(new[] {extension}, action);
        }
        
        public void WithCompatibleExtension(Type extension, Action<Sequence> action)
        {
            WithCompatibleExtension(extension.FullName, action);
        }

        public void WithRequiredExtensions(IEnumerable<Type> extensions, Action<Sequence> action)
        {
            var prior = _requiredExtensions;
            _requiredExtensions = _requiredExtensions.Union(extensions);
            
            try
            {
                action(this);
            }
            finally
            {
                _requiredExtensions = prior;
            }
        }
        
        public void WithRequiredExtension(Type extension, Action<Sequence> action)
        {
            WithRequiredExtensions(new[] {extension}, action);
        }
    }
}