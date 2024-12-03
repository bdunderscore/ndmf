#nullable enable

using System;
using nadena.dev.ndmf.runtime;
using nadena.dev.ndmf.runtime.components;

namespace nadena.dev.ndmf.platform
{
    internal static class AmbientPlatform
    {
        public static event Action? OnDefaultPlatformChanged;
        
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

        private static INDMFPlatformProvider? _currentPlatform = null;
        public static INDMFPlatformProvider CurrentPlatform => _currentPlatform ?? _defaultPlatform;

        internal class Scope : IDisposable
        {
            private readonly INDMFPlatformProvider? _previous;
            
            public Scope(INDMFPlatformProvider? platform)
            {
                _previous = _currentPlatform;
                _currentPlatform = platform ?? throw new ArgumentNullException(nameof(platform));
            }
            
            public void Dispose()
            {
                _currentPlatform = _previous;
            }
        }
    }
}