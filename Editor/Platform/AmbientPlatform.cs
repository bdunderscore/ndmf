#nullable enable

using System;

namespace nadena.dev.ndmf.platform
{
    internal static class AmbientPlatform
    {
        /// <summary>
        ///     The default platform to use in legacy calls like AvatarProcessor.ProcessAvatar
        /// </summary>
        public static INDMFPlatformProvider DefaultPlatform { get; internal set; } = new GenericPlatform();

        internal class Scope : IDisposable
        {
            private readonly INDMFPlatformProvider? _previous;
            
            public Scope(INDMFPlatformProvider? platform)
            {
                _previous = DefaultPlatform;
                DefaultPlatform = platform ?? throw new ArgumentNullException(nameof(platform));
            }
            
            public void Dispose()
            {
                DefaultPlatform = _previous ?? throw new InvalidOperationException("Ambient platform was not set");
            }
        }
    }
}