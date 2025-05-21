using UnityEngine.UIElements;

namespace nadena.dev.ndmf.platform
{
    public abstract class BuildUIElement : VisualElement
    {
        /// <summary>
        /// The currently selected avatar root. Will be invoked when the user selects a new avatar root.
        /// </summary>
        public virtual UnityEngine.GameObject AvatarRoot { get; set; }
    }
}