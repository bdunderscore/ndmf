#region

using UnityEngine.UIElements;

#endregion

namespace nadena.dev.ndmf
{
    public interface IError
    {
        ErrorCategory Category { get; }
        VisualElement CreateVisualElement(ErrorReport report);
        string ToMessage();

        void AddReference(ObjectReference obj)
        {
        }
    }
}