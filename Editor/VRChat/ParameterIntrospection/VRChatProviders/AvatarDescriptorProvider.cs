#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

#endregion

namespace nadena.dev.ndmf.VRChatProviders
{
    [ParameterProviderFor(typeof(VRCAvatarDescriptor))]
    internal class AvatarDescriptorProvider : IParameterProvider
    {
        private VRCAvatarDescriptor _avatarDescriptor;

        public AvatarDescriptorProvider(VRCAvatarDescriptor descriptor)
        {
            _avatarDescriptor = descriptor;
        }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            if (_avatarDescriptor.expressionParameters == null) return Array.Empty<ProvidedParameter>();
            if (_avatarDescriptor.expressionParameters.parameters == null) return Array.Empty<ProvidedParameter>();

            return _avatarDescriptor.expressionParameters.parameters.Select(p =>
            {
                return new ProvidedParameter(
                    p.name,
                    ParameterNamespace.Animator,
                    _avatarDescriptor,
                    VRChatBuiltinProviderPlugin.Instance,
                    ConvertType(p.valueType)
                )
                {
                    WantSynced = p.networkSynced,
                    DefaultValue = p.defaultValue
                };
            });
        }

        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap,
            BuildContext context)
        {
            // no-op
        }

        private AnimatorControllerParameterType ConvertType(VRCExpressionParameters.ValueType argValueType)
        {
            switch (argValueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    return AnimatorControllerParameterType.Bool;
                case VRCExpressionParameters.ValueType.Float:
                    return AnimatorControllerParameterType.Float;
                case VRCExpressionParameters.ValueType.Int:
                    return AnimatorControllerParameterType.Int;
                default:
                    return AnimatorControllerParameterType.Trigger; // TODO
            }
        }
    }
}