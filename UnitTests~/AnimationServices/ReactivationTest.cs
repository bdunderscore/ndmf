#if NDMF_VRCSDK3_AVATARS

using System.IO;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using NUnit.Framework.Internal;
using UnityEditor;
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
                    stateMachine = sm,
                    name = "test"
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
            Assert.AreEqual("test", stage1.layers[0].name);
            
            context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            VirtualControllerContext tempQualifier1 = context.Extension<VirtualControllerContext>();
            var vl2 = tempQualifier1.Controllers[vrcLayer.type];
            Assert.AreSame(vl1, vl2);
            Assert.AreEqual("test", vl1.Layers.First().Name);
            context.DeactivateAllExtensionContexts();
            
            var stage2 = (AnimatorController)vrcDesc.baseAnimationLayers[0].animatorController;
            Assert.AreSame(stage1, stage2);
            Assert.AreEqual("test", stage2.layers[0].name);
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
        
        /// <summary>
        /// Regression test for https://github.com/bdunderscore/ndmf/issues/761 and
        /// https://github.com/anatawa12/AvatarOptimizer/issues/1664.
        /// When an AnimatorOverrideController replaces a VRChat proxy animation, the override must not revert
        /// to the proxy after VirtualControllerContext is deactivated, externally modified, and reactivated.
        /// The root cause was that the ObjectRegistry incorrectly recorded the original (proxy) clip rather than
        /// the override clip, causing re-cloning on the second activation to treat the committed clip as a proxy
        /// marker and substitute it back.
        /// </summary>
        [Test]
        public void WhenAOCReplacesProxyAnimation_OverrideIsPreservedAfterReactivation()
        {
            // Load any VRChat proxy animation (must be recognized as a special motion)
            var proxyClipPath = AssetDatabase
                .FindAssets("t:AnimationClip", new[] { "Packages/com.vrchat.avatars" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).StartsWith("proxy_"));

            if (proxyClipPath == null) Assert.Ignore("No VRChat proxy animation found; is the VRChat SDK installed?");

            var proxyClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(proxyClipPath);
            Assert.IsNotNull(proxyClip);

            var root = CreateRoot("root");
            var vrcDesc = root.GetComponent<VRCAvatarDescriptor>();

            // Build a single-layer base controller whose default state uses the proxy animation
            var baseController = new AnimatorController();
            var sm = new AnimatorStateMachine();
            var state = new AnimatorState { motion = proxyClip };
            sm.states = new[] { new ChildAnimatorState { state = state } };
            sm.defaultState = state;
            baseController.layers = new[] { new AnimatorControllerLayer { stateMachine = sm } };

            // AOC replaces the proxy with a new empty clip
            var overrideClip = TrackObject(new AnimationClip { name = "override" });
            var aoc = new AnimatorOverrideController();
            aoc.runtimeAnimatorController = baseController;
            aoc[proxyClip] = overrideClip;

            // Install the AOC as the FX layer
            vrcDesc.customizeAnimationLayers = true;
            var vrcLayers = vrcDesc.baseAnimationLayers;
            for (int i = 0; i < vrcLayers.Length; i++)
            {
                if (vrcLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    vrcLayers[i].animatorController = aoc;
                    vrcLayers[i].isDefault = false;
                    break;
                }
            }
            vrcDesc.baseAnimationLayers = vrcLayers;

            var context = CreateContext(root);
            var objectRegistry = new ObjectRegistry(root.transform);

            using (new ObjectRegistryScope(objectRegistry))
            {
                // Step 1: Activate
                context.ActivateExtensionContext<VirtualControllerContext>();
                // Step 2: Deactivate — FX layer is now a plain AnimatorController with the AOC applied
                context.DeactivateExtensionContext<VirtualControllerContext>();

                // Step 3: Simulate an external plugin modifying the committed controller without going
                // through the Virtual Animator API (e.g. AvatarOptimizer's Trace and Optimize).
                // Adding a layer mutates the controller, which triggers OnAnimatorControllerDirty and
                // clears LastCommit, forcing VirtualControllerContext to perform a fresh clone on the
                // next activation rather than reusing the cached virtual controller.
                var fxAfterFirst = vrcDesc.baseAnimationLayers
                    .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                var committedController = (AnimatorController)fxAfterFirst.animatorController;
                committedController.layers = committedController.layers
                    .Append(new AnimatorControllerLayer { name = "dummy", stateMachine = new AnimatorStateMachine() })
                    .ToArray();

                // Step 4: Activate again — must re-clone from the committed controller
                context.ActivateExtensionContext<VirtualControllerContext>();
                // Step 5: Deactivate
                context.DeactivateExtensionContext<VirtualControllerContext>();
            }

            // Step 6: Assert the motion has NOT reverted to the original proxy animation
            var fxFinal = vrcDesc.baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var finalController = (AnimatorController)fxFinal.animatorController;
            var finalMotion = finalController.layers[0].stateMachine.defaultState.motion;

            Assert.AreNotSame(proxyClip, finalMotion,
                "Motion reverted to the original proxy animation after context reactivation");
        }
    }
}

#endif