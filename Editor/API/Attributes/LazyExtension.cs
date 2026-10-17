using System;
using JetBrains.Annotations;

namespace nadena.dev.ndmf
{
    /// <summary>
    ///     Defers an extension context's OnActivate callback until it is first obtained through BuildContext.Extension.
    ///     An unused context will not receive OnDeactivate. Explicit ActivateExtensionContext calls still activate it
    ///     immediately. Contexts whose activation has required side effects must not use this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [PublicAPI]
    public sealed class LazyExtension : Attribute
    {
    }
}
