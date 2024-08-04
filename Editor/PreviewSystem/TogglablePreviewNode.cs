using System;
using nadena.dev.ndmf.preview.UI;

namespace nadena.dev.ndmf.preview
{
    /// <summary>
    ///     Declares a previewable aspect of this plugin.
    /// </summary>
    public sealed class TogglablePreviewNode
    {
        /// <summary>
        ///     The name that will be shown to the user. Will be re-invoked on language change.
        /// </summary>
        public Func<string> DisplayName { get; }

        public PublishedValue<bool> IsEnabled { get; }

        private TogglablePreviewNode(Func<string> displayName, bool initialState)
        {
            DisplayName = displayName;
            IsEnabled = new PublishedValue<bool>(initialState);
        }
        
        /// <summary>
        /// Creates a togglable preview node. Must not be invoked during static initialization.
        /// </summary>
        /// <param name="displayName">A function which returns the localized display name for this switch</param>
        /// <param name="qualifiedName">If not null, a name which will be used to save this configuration</param>
        /// <param name="initialState">The initial state for this node; defaults to true</param>
        public static TogglablePreviewNode Create(Func<string> displayName, string qualifiedName = null,
            bool initialState = true)
        {
            if (qualifiedName != null) initialState = PreviewPrefs.instance.GetNodeState(qualifiedName, initialState);

            var node = new TogglablePreviewNode(displayName, initialState);

            if (qualifiedName != null)
                node.IsEnabled.OnChange += value => PreviewPrefs.instance.SetNodeState(qualifiedName, value);

            return node;
        }
    }
}