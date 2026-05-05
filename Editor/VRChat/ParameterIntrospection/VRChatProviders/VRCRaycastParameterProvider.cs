#if NDMF_VRCSDK3_AVATARS_3_10_3_OR_NEWER

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using VRC.SDK3.Avatars.Components;

#endregion

namespace nadena.dev.ndmf.VRChatProviders
{
    [ParameterProviderFor(typeof(VRCRaycast))]
    internal class VRCRaycastParameterProvider : IParameterProvider
    {
        private readonly VRCRaycast _raycast;

        public VRCRaycastParameterProvider(VRCRaycast raycast)
        {
            _raycast = raycast;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            if (string.IsNullOrEmpty(_raycast.Parameter)) return Array.Empty<ProvidedParameter>();

            return new[]
            {
                new ProvidedParameter(_raycast.Parameter, ParameterNamespace.PhysBonesPrefix, _raycast,
                    VRChatBuiltinProviderPlugin.Instance, null)
            };
        }

        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
            BuildContext context)
        {
            // no-op
        }
    }
}
#endif