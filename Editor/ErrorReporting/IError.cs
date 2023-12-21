#region

using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// A base interface for custom NDMF error reports.
    /// </summary>
    public interface IError
    {
        /// <summary>
        /// The severity of the error
        /// </summary>
        ErrorSeverity Severity { get; }
        /// <summary>
        /// Creates a VisualElement used to display the error in the error report window.
        /// </summary>
        /// <param name="report">The report this error is in</param>
        /// <returns>A VisualElement to display</returns>
        VisualElement CreateVisualElement(ErrorReport report);
        /// <summary>
        /// Formats the error as a string, suitable for being dumped to the unity log.
        /// </summary>
        /// <returns></returns>
        string ToMessage();
        /// <summary>
        /// Adds a reference to a context object that might be helpful for tracking down the error.
        /// </summary>
        /// <param name="obj"></param>
        void AddReference(ObjectReference obj);
    }
}