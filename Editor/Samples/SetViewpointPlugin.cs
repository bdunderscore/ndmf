using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.runtime.samples;
using nadena.dev.ndmf.sample;
using UnityEngine;

[assembly: ExportsPlugin(typeof(SetViewpointPlugin))]

namespace nadena.dev.ndmf.sample
{
    public class SetViewpointPlugin : Plugin<SetViewpointPlugin>
    {
        /// <summary>
        /// This name is used to identify the plugin internally, and can be used to declare BeforePlugin/AfterPlugin
        /// dependencies. If not set, the full type name will be used.
        /// </summary>
        public override string QualifiedName => "nadena.dev.av3-build-framework.sample.set-viewpoint";
        
        /// <summary>
        /// The plugin name shown in debug UIs. If not set, the qualified name will be shown.
        /// </summary>
        public override string DisplayName => "Set viewpoint using object";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Set viewpoint", ctx =>
            {
                var obj = ctx.AvatarRootObject.GetComponentInChildren<SetViewpoint>();
                if (obj != null)
                {
                    ctx.AvatarDescriptor.ViewPosition =
                        Quaternion.Inverse(ctx.AvatarRootTransform.rotation) * (
                            obj.transform.position - ctx.AvatarRootTransform.position);
                }
            });
        }
    }
}
