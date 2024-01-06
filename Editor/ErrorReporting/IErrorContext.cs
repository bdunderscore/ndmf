using System.Collections.Generic;

namespace nadena.dev.ndmf
{
    /// <summary>
    /// This interface allows multiple context objects to be passed to an error in a single argument.
    /// Passing an object implementing IErrorContext to ErrorReport methods will add all objects referenced in
    /// ContextReferences as context objects.
    /// </summary>
    public interface IErrorContext
    {
        IEnumerable<ObjectReference> ContextReferences { get; }
    }
}