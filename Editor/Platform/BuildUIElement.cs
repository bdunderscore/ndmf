using UnityEngine.UIElements;

namespace nadena.dev.ndmf.platform
{
    internal abstract class BuildUIElement : VisualElement
    {
        public virtual UnityEngine.GameObject AvatarRoot { get; set; }
    }
}