#if NDMF_VRCSDK3_AVATARS
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Assert = UnityEngine.Assertions.Assert;

namespace UnitTests.AnimationServices
{
    public class AnimationObjectRegistryTrackingTest : TestBase
    {
        [Test]
        public void TracksObjectReplacement()
        {
            var root = CreateRoot("root");
            var vrcDesc = root.GetComponent<VRCAvatarDescriptor>();
            var controller = new AnimatorController();
            
            // Add a minimal state machine
            var sm = new AnimatorStateMachine();
            var state = new AnimatorState();
            var motion = new AnimationClip();
            state.motion = motion;

            controller.name = sm.name = state.name = motion.name = "TEST";
            
            sm.defaultState = state;
            sm.states = new[]
            {
                new ChildAnimatorState
                {
                    state = state
                }
            };
            controller.layers = new[]
            {
                new AnimatorControllerLayer
                {
                    stateMachine = sm
                }
            };

            vrcDesc.customizeAnimationLayers = true;
            var vrcLayer = vrcDesc.baseAnimationLayers[0];
            vrcLayer.animatorController = controller;
            vrcLayer.isDefault = false;
            vrcDesc.baseAnimationLayers[0] = vrcLayer;

            var context = CreateContext(root);
            var objectRegistry = new ObjectRegistry(root.transform);
            var reg = (IObjectRegistry)objectRegistry;
            using (new ObjectRegistryScope(objectRegistry))
            {
                context.ActivateExtensionContext<VirtualControllerContext>();
                context.DeactivateExtensionContext<VirtualControllerContext>();
            }

            var newController = (AnimatorController) vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreNotEqual(newController, controller);
            Assert.AreEqual(reg.GetReference(newController), reg.GetReference(controller));
            
            var newSM = newController.layers[0].stateMachine;
            var newState = newSM.states[0].state;
            var newMotion = newState.motion;
            
            Assert.AreEqual(reg.GetReference(newSM), reg.GetReference(sm));
            Assert.AreEqual(reg.GetReference(newState), reg.GetReference(state));
            Assert.AreEqual(reg.GetReference(newMotion), reg.GetReference(motion));
        }
    }
}
#endif