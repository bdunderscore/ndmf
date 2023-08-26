using System.Collections.Immutable;
using System.Collections.Generic;
using nadena.dev.build_framework;
using nadena.dev.build_framework.runtime.samples;
using nadena.dev.build_framework.samples;
using UnityEngine;

[assembly: ExportsPlugin(typeof(SetViewpointPlugin))]

namespace nadena.dev.build_framework.samples
{
    public class SetViewpointPlugin : Plugin
    {
        public override string QualifiedName => "nadena.dev.av3-build-framework.sample.set-viewpoint";

        public override ImmutableList<PluginPass> Passes
            => new List<PluginPass> {new SetViewpointPass()}.ToImmutableList();
    }

    public class SetViewpointPass : PluginPass
    {
        public override void Process(BuildContext context)
        {
            var obj = context.AvatarRootObject.GetComponentInChildren<SetViewpoint>();
            if (obj != null)
            {
                context.AvatarDescriptor.ViewPosition =
                    Quaternion.Inverse(context.AvatarRootTransform.rotation) * (
                        obj.transform.position - context.AvatarRootTransform.position);
            }
        }
    }
}