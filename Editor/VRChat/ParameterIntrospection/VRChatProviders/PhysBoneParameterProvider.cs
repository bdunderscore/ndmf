#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

#endregion

namespace nadena.dev.ndmf.VRChatProviders
{
    [ParameterProviderFor(typeof(VRCPhysBone))]
    internal class PhysBoneParameterProvider : IParameterProvider
    {
        private readonly VRCPhysBone _bone;

        public PhysBoneParameterProvider(VRCPhysBone bone)
        {
            _bone = bone;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            if (string.IsNullOrEmpty(_bone.parameter)) return Array.Empty<ProvidedParameter>();

            return new[]
            {
                new ProvidedParameter(_bone.parameter, ParameterNamespace.PhysBonesPrefix, _bone,
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