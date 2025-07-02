using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf
{
    /// <summary>
    ///     This attribute is used to mark a class as compatible with a specific context.
    ///     NDMF will not activate the context solely based on this attribute, but will avoid deactivating it if it is
    ///     already active. This attribute also implicitly includes any dependencies of the specified context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    [PublicAPI]
    public sealed class CompatibleWithContext : Attribute
    {
        public Type ExtensionContext { get; }

        public CompatibleWithContext(Type extensionContext)
        {
            if (!typeof(IExtensionContext).IsAssignableFrom(extensionContext))
            {
                throw new ArgumentException(
                    $"{extensionContext.FullName} does not implement {nameof(IExtensionContext)}",
                    nameof(extensionContext));
            }

            ExtensionContext = extensionContext;
        }
    }
}