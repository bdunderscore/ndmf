namespace nadena.dev.ndmf
{
    /// <summary>
    /// The IExtensionContext is declared by custom extension contexts.
    /// </summary>
    public interface IExtensionContext
    {
        /// <summary>
        /// Invoked when the extension is activated.
        /// </summary>
        /// <param name="context"></param>
        void OnActivate(BuildContext context);

        /// <summary>
        /// Invoked when the extension is deactivated.
        /// </summary>
        /// <param name="context"></param>
        void OnDeactivate(BuildContext context);
    }
}