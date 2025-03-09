#if NDMF_VRCSDK3_AVATARS

using nadena.dev.ndmf.animator;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace UnitTests.AnimationServices
{
    public class ReactivationTest : TestBase
    {
        [Test]
        public void WhenReactivatingController_ReusesAssets()
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
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            VirtualControllerContext tempQualifier = context.Extension<VirtualControllerContext>();
            var vl1 = tempQualifier.Controllers[vrcLayer.type];
            context.DeactivateAllExtensionContexts();

            var stage1 = (AnimatorController)vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreNotSame(controller, stage1);
            
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            VirtualControllerContext tempQualifier1 = context.Extension<VirtualControllerContext>();
            var vl2 = tempQualifier1.Controllers[vrcLayer.type];
            Assert.AreSame(vl1, vl2);
            context.DeactivateAllExtensionContexts();
            
            var stage2 = (AnimatorController)vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreSame(stage1, stage2);
        }
        
        [Test]
        public void WhenReactivatingController_WithMutations_DoesNotReuseAssets()
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
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.DeactivateAllExtensionContexts();

            var stage1 = (AnimatorController)vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreNotSame(controller, stage1);

            stage1.layers[0].stateMachine.defaultState.motion = new AnimationClip();
            
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            context.DeactivateAllExtensionContexts();
            
            var stage2 = (AnimatorController)vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreNotSame(stage1, stage2);
        }
        
    }
}

#endif