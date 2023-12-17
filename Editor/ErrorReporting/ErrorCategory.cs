namespace nadena.dev.ndmf
{
    public enum ErrorCategory
    {
        /// <summary>
        /// Informational messages that indicate an unusual configuration that might be an error.
        /// </summary>
        Information,

        /// <summary>
        /// Errors that do not block avatar processing.
        /// </summary>
        NonFatal,

        /// <summary>
        /// Errors that block avatar processing. If an error of this category is emitted, the avatar upload will be
        /// blocked.
        /// </summary>
        Error,

        /// <summary>
        /// Internal errors, such as uncaught exceptions.  If an error of this category is emitted, the avatar upload
        /// will be blocked.
        /// </summary>
        InternalError
    }
}