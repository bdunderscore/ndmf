#nullable enable

using System;
using nadena.dev.ndmf.runtime;
using nadena.dev.ndmf.runtime.components;

namespace nadena.dev.ndmf.platform
{
    /// <summary>
    /// Provides access to the currently selected NDMF build platform, even outside of the context of the build process.
    /// </summary>
    public static class AmbientPlatform
    {
        public static event Action? OnDefaultPlatformChanged;
        
        private static INDMFPlatformProvider _defaultPlatform = GenericPlatform.Instance;
        /// <summary>
        ///     The default platform to use in legacy calls like AvatarProcessor.ProcessAvatar.
        ///     This is _not_ necessarily the platform in use in a build; use CurrentPlatform or BuildContext.PlatformProvider
        ///     for that.
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
        
        /// <summary>
        /// Returns the current platform; either the one in use in the current build context, or the default platform.
        /// </summary>
        public static INDMFPlatformProvider CurrentPlatform => _currentPlatform ?? _defaultPlatform;

        /// <summary>
        /// This disposable scope allows you to temporarily set the current platform for the duration of the scope.
        /// </summary>
        public class Scope : IDisposable
        {
            private readonly INDMFPlatformProvider? _previous;
            
            /// <summary>
            /// 
            /// </summary>
            /// <param name="platform">The platform to set as the ambient platform, or null to use the default platform</param>
            /// <exception cref="ArgumentNullException"></exception>
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