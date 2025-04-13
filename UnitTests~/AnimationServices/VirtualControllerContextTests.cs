using System.Linq;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
#endif

namespace UnitTests.AnimationServices
{
    public class VirtualControllerContextTests : TestBase
    {
        #if NDMF_VRCSDK3_AVATARS
        [Test]
        public void WhenContextReactivated_CorrectInnateKeyUsed()
        {
            var root = CreateRoot("x");
            var desc = root.GetComponent<VRCAvatarDescriptor>();

            var fx = new AnimatorController();
            var fxLayer = new AnimatorControllerLayer
            {
                name = "FX",
                defaultWeight = 1,
                stateMachine = new AnimatorStateMachine()
            };
            fx.layers = new[] { fxLayer };
            fxLayer.stateMachine.name = "FX";
            
            var state = new AnimatorState
            {
                name = "Test",
                motion = new AnimationClip()
            };
            fxLayer.stateMachine.states = new[] { new ChildAnimatorState()
            {
                state = state
            }};
            fxLayer.stateMachine.defaultState = state;

            state.behaviours = new StateMachineBehaviour[]
            {
                new VRCAnimatorLayerControl()
                {
                    layer = 0,
                    playable = VRC_AnimatorLayerControl.BlendableLayer.FX,
                }
            };

            desc.customizeAnimationLayers = true;
            var vrcLayers = desc.baseAnimationLayers;
            for (int i = 0; i < vrcLayers.Length; i++)
            {
                if (vrcLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    vrcLayers[i].animatorController = fx;
                    vrcLayers[i].isDefault = false;
                }
            }

            desc.baseAnimationLayers = vrcLayers;

            var context = CreateContext(root);
            
            context.ActivateExtensionContext<VirtualControllerContext>();
            context.DeactivateExtensionContext<VirtualControllerContext>();
            context.ActivateExtensionContext<VirtualControllerContext>();
            context.DeactivateExtensionContext<VirtualControllerContext>();

            var finalFx = (AnimatorController)desc.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController;
            
            Assert.AreEqual(1, finalFx.layers[0].stateMachine.defaultState.behaviours.Length);
        }
        #endif
    }
}