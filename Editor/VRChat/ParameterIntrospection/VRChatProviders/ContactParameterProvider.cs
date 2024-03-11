#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using VRC.SDK3.Dynamics.Contact.Components;

#endregion

namespace nadena.dev.ndmf.VRChatProviders
{
    [ParameterProviderFor(typeof(VRCContactReceiver))]
    internal class ContactParameterProvider : IParameterProvider
    {
        private readonly VRCContactReceiver _component;

        public ContactParameterProvider(VRCContactReceiver receiver)
        {
            _component = receiver;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            if (string.IsNullOrEmpty(_component.parameter)) return Array.Empty<ProvidedParameter>();

            return new[]
            {
                new ProvidedParameter(_component.parameter, ParameterNamespace.Animator, _component,
                    VRChatBuiltinProviderPlugin.Instance, null)
                {
                    IsAnimatorOnly = true,
                    WantSynced = false,
                }
            };
        }
        
        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), string> nameMap, BuildContext context = null)
        {
            // no-op
        }
    }
}