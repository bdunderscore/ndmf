#nullable enable

using System;

namespace nadena.dev.ndmf.platform
{
    internal static class AmbientPlatform
    {
        public static event Action OnDefaultPlatformChanged;
        
        private static INDMFPlatformProvider _defaultPlatform = GenericPlatform.Instance;
        /// <summary>
        ///     The default platform to use in legacy calls like AvatarProcessor.ProcessAvatar
        /// </summary>
        public static INDMFPlatformProvider DefaultPlatform
        {
            get => _defaultPlatform;
            internal set
            {
                if (_defaultPlatform == value) return;
                _defaultPlatform = value;
                OnDefaultPlatformChanged?.Invoke();
            }
        }

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