#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

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

        /// <summary>
        /// Declares that a group of passes are compatible with a given set of extensions - that is, they will not deactivate
        /// the extensions if already active.
        ///
        /// <code>
        ///   sequence.WithCompatibleExtensions(new[] {"foo.bar.MyExtension"}, s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// 
        /// </summary>
        /// <param name="extensions">The extensions to permit</param>
        /// <param name="action">An action that will be invoked with the extensions marked compatible</param>
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

        /// <summary>
        /// Declares that a group of passes are compatible with given extensions - that is, they will not deactivate
        /// the extensions if already active.
        ///
        /// <code>
        ///   sequence.WithCompatibleExtensions(new[] {typeof(MyExtension)}, s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// 
        /// </summary>
        /// <param name="extensions">The extensions to permit</param>
        /// <param name="action">An action that will be invoked with the extensions marked compatible</param>
        public void WithCompatibleExtensions(IEnumerable<Type> extensions, Action<Sequence> action)
        {
            WithCompatibleExtensions(extensions.Select(t => t.FullName), action);
        }

        /// <summary>
        /// Declares that a group of passes are compatible with a given extension - that is, they will not deactivate
        /// the extension if it is already active.
        ///
        /// <code>
        ///   sequence.WithCompatibleExtension("foo.bar.MyExtension", s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// 
        /// </summary>
        /// <param name="extension">The extension to permit</param>
        /// <param name="action">An action that will be invoked with the extensions marked compatible</param>
        public void WithCompatibleExtension(string extension, Action<Sequence> action)
        {
            WithCompatibleExtensions(new[] {extension}, action);
        }

        /// <summary>
        /// Declares that a group of passes are compatible with a given extension - that is, they will not deactivate
        /// the extension if it is already active.
        ///
        /// <code>
        ///   sequence.WithCompatibleExtension(typeof(MyExtension), s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// 
        /// </summary>
        /// <param name="extension">The extension to permit</param>
        /// <param name="action">An action that will be invoked with the extensions marked compatible</param>
        public void WithCompatibleExtension(Type extension, Action<Sequence> action)
        {
            WithCompatibleExtension(extension.FullName, action);
        }

        /// <summary>
        /// Declares that a group of passes require a given set of extensions - that is, they will activate the extensions
        /// before executing.
        ///
        /// <code>
        ///   sequence.WithRequiredExtensions(new[] {typeof(foo.bar.MyExtension)}, s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// </summary>
        /// <param name="extension">The extension to request</param>
        /// <param name="action">An action that will be invoked with the extensions marked required</param>
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

        /// <summary>
        /// Declares that a group of passes require a given extension - that is, they will activate the extension
        /// before executing.
        ///
        /// <code>
        ///   sequence.WithRequiredExtension(typeof(foo.bar.MyExtension), s => {
        ///     s.Run(typeof(MyPass));
        ///   });
        /// </code>
        /// </summary>
        /// <param name="extension">The extension to request</param>
        /// <param name="action">An action that will be invoked with the extensions marked required</param>
        public void WithRequiredExtension(Type extension, Action<Sequence> action)
        {
            WithRequiredExtensions(new[] {extension}, action);
        }
    }
}