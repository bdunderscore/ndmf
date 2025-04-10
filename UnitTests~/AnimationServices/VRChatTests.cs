#if NDMF_VRCSDK3_AVATARS

using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.UnitTestSupport;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace UnitTests.AnimationServices
{
    public class VRChatTests : TestBase
    {
        [Test]
        public void LoadsDefaultControllersIfNoneProvided()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            var ctx = CreateContext(root);

            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();

            var fx = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            Assert.IsNotNull(fx);
            Assert.AreEqual("vrc_AvatarV3FaceLayer", fx.Name);
            
            var ikPose = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.IKPose];
            Assert.IsNotNull(ikPose);
            Assert.AreEqual("vrc_AvatarV3UtilityIKPose", ikPose.Name);
        }

        [Test]
        public void LoadsOverrideControllers()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();

            var controller = new AnimatorController() { name = "TEST" };
            
            SetBaseLayer(avDesc, controller, VRCAvatarDescriptor.AnimLayerType.FX);

            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();
            
            var fx = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            Assert.IsNotNull(fx);
            Assert.AreEqual("TEST", fx.Name);
        }

        private static void SetBaseLayer(VRCAvatarDescriptor avDesc, AnimatorController controller, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var layers = avDesc.baseAnimationLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.type == layerType)
                {
                    layer.animatorController = controller;
                    layer.isDefault = false;
                    layers[i] = layer;
                    break;
                }
            }
            avDesc.baseAnimationLayers = layers;
        }

        [Test]
        public void WritesBackOverrideControllers()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            
            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();

            anim.Controllers[VRCAvatarDescriptor.AnimLayerType.FX]!.Name = "FX";
            anim.Controllers[VRCAvatarDescriptor.AnimLayerType.IKPose]!.Name = "IK";
            
            ctx.DeactivateExtensionContext<VirtualControllerContext>();
            
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            var fxLayer = avDesc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var ikLayer = avDesc.specialAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.IKPose);
            
            Assert.AreEqual("FX", fxLayer.animatorController.name);
            Assert.AreEqual("IK", ikLayer.animatorController.name);
        }

        [Test]
        public void CorrectsInterLayerReferences()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            
            SetBaseLayer(avDesc, BuildInterlayerController("c1"), VRCAvatarDescriptor.AnimLayerType.FX);
            
            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();
            
            var fx = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            var c1_1 = fx.Layers.First(l => l.Name == "c1:1");
            var lc = (VRCAnimatorLayerControl) c1_1.StateMachine.DefaultState!.Behaviours.First();
            var c1_2 = fx.Layers.First(l => l.Name == "c1:2");
            Assert.AreEqual(c1_2.VirtualLayerIndex, lc.layer);

            var newController = anim.CloneContext.Clone(BuildInterlayerController("c2"))!;
            lc = (VRCAnimatorLayerControl) newController.Layers.ToList()[0].StateMachine.DefaultState!.Behaviours.First();
            Assert.AreEqual(newController.Layers.ToList()[1].VirtualLayerIndex, lc.layer);

            foreach (var l in newController.Layers)
            {
                fx.AddLayer(LayerPriority.Default, l);
            }
            
            ctx.DeactivateExtensionContext<VirtualControllerContext>();
            
            var fxLayer = avDesc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var stateMachines = ((AnimatorController)fxLayer.animatorController).layers
                .Select(l => l.stateMachine).ToArray();
            
            var out_c1_1 = stateMachines.First(sm => sm.name == "c1:1");
            var out_c2_1 = stateMachines.First(sm => sm.name == "c2:1");
            var out_c1_2_idx = stateMachines.ToList().IndexOf(stateMachines.First(sm => sm.name == "c1:2"));
            var out_c2_2_idx = stateMachines.ToList().IndexOf(stateMachines.First(sm => sm.name == "c2:2"));
            
            lc = out_c1_1.defaultState.behaviours.First() as VRCAnimatorLayerControl;
            Assert.AreEqual(out_c1_2_idx, lc.layer);
            lc = out_c2_1.defaultState.behaviours.First() as VRCAnimatorLayerControl;
            Assert.AreEqual(out_c2_2_idx, lc.layer);
        }

        [Test]
        public void IgnoresCrossLayerReferences()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            
            // Base -> FX
            SetBaseLayer(avDesc, BuildInterlayerController("c1"), VRCAvatarDescriptor.AnimLayerType.Base);
            
            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();

            var baseLayer = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.Base];
            var lc = (VRCAnimatorLayerControl) baseLayer.Layers
                .First(l => l.Name == "c1:1")
                .StateMachine.DefaultState.Behaviours.First();
            Assert.AreEqual(1, lc.layer);
            
            ctx.DeactivateExtensionContext<VirtualControllerContext>();
            
            var baseLayerController = avDesc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.Base);
            var c11layer = ((AnimatorController)baseLayerController.animatorController).layers
                .First(l => l.stateMachine.name == "c1:1");
            lc = c11layer.stateMachine.defaultState.behaviours.First() as VRCAnimatorLayerControl;
            Assert.AreEqual(1, lc.layer);
        }

        [Test]
        public void HandlesStateMachineBehaviours()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            var avDesc = root.GetComponent<VRCAvatarDescriptor>();
            
            var controller = new AnimatorController() { name = "TEST" };
            var sm = new AnimatorStateMachine() { name = "SM" };
            controller.layers = new[]
            {
                new AnimatorControllerLayer() { stateMachine = sm }
            };
            var layerControl = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
            layerControl.layer = 0;
            layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
            sm.behaviours = new StateMachineBehaviour[]
            {
                layerControl
            };
            
            SetBaseLayer(avDesc, controller, VRCAvatarDescriptor.AnimLayerType.FX);
            
            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();
            
            var fx = anim.Controllers[VRCAvatarDescriptor.AnimLayerType.FX];
            var lc = (VRCAnimatorLayerControl) fx.Layers.First().StateMachine.Behaviours.First();
            Assert.AreEqual(fx.Layers.First().VirtualLayerIndex, lc.layer);

            var l2 = fx.AddLayer(LayerPriority.Default, "test");
            lc.layer = l2.VirtualLayerIndex;
            
            ctx.DeactivateExtensionContext<VirtualControllerContext>();
            
            var fxLayer = avDesc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var stateMachine = ((AnimatorController)fxLayer.animatorController).layers.First().stateMachine;
            lc = stateMachine.behaviours.First() as VRCAnimatorLayerControl;
            Assert.AreEqual(1, lc.layer);
        }

        
        [Test]
        public void HandlesStateMachineBehaviours_OnVirtualizedControllers_OverReactivation()
        {
            var root = CreatePrefab("TestAssets/EmptyAvatar.prefab");
            
            var controller = new AnimatorController() { name = "TEST" };
            var sm = new AnimatorStateMachine() { name = "SM" };
            controller.layers = new[]
            {
                new AnimatorControllerLayer() { stateMachine = sm }
            };
            var layerControl = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
            layerControl.layer = 0;
            layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
            sm.behaviours = new StateMachineBehaviour[]
            {
                layerControl
            };
            var vc = root.AddComponent<VirtualizedComponent>();
            vc.AnimatorController = controller;
            vc.TargetControllerKey = VRCAvatarDescriptor.AnimLayerType.FX;
            
            var ctx = CreateContext(root);
            var anim = ctx.ActivateExtensionContext<VirtualControllerContext>();
            ctx.DeactivateAllExtensionContexts();

            var behavior = ((AnimatorController)vc.AnimatorController).layers[0].stateMachine.behaviours[0];
            Assert.AreEqual(0, ((VRCAnimatorLayerControl)behavior).layer);
            
            anim = ctx.ActivateExtensionContext<VirtualControllerContext>();
            
            var fx = anim.Controllers[vc];
            var lc = (VRCAnimatorLayerControl) fx.Layers.First().StateMachine!.Behaviours.First();
            Assert.AreEqual(fx.Layers.First().VirtualLayerIndex, lc.layer);
        }
        
        [Test]
        public void RemapsPlayAudioPaths()
        {
            var prefab = CreatePrefab("TestAssets/PlayAudio/PlayAudio.prefab");
            
            var ctx = CreateContext(prefab);
            ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();

            prefab.transform.Find("A").gameObject.name = "B";
            
            ctx.DeactivateAllExtensionContexts();
            
            var fx = prefab.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
                .First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var controller = (AnimatorController) fx.animatorController;
            
            var stateMachine = controller.layers.First().stateMachine;
            var state = stateMachine.defaultState;
            var behaviour = (VRCAnimatorPlayAudio) state.behaviours.First();

            Assert.AreEqual("B", behaviour.SourcePath);
        }

        [Test]
        public void RemapPlayAudioPaths_InVirtualizedController()
        {
            var root = CreateRoot("root");
            var ac = LoadAsset<AnimatorController>("TestAssets/PlayAudio/playaudio_ac.controller");

            var sub = CreateChild(root, "B");
            var audio = CreateChild(sub, "A");

            var virtualized = sub.AddComponent<VirtualizedComponent>();
            virtualized.AnimatorController = ac;
            virtualized.MotionBasePath = "B";
            
            var ctx = CreateContext(root);
            var vcc = ctx.ActivateExtensionContext<VirtualControllerContext>();

            var virtualController = vcc.Controllers[virtualized];
            var stateMachine = virtualController.Layers.First().StateMachine;
            var state = stateMachine.DefaultState;
            
            var behaviour = (VRCAnimatorPlayAudio) state.Behaviours.First();
            
            Assert.AreEqual("B/A", behaviour.SourcePath);
        }
        
        private AnimatorController BuildInterlayerController(string prefix)
        {
            var ac = new AnimatorController();
            var sm1 = new AnimatorStateMachine() { name = prefix + ":1" };
            var sm2 = new AnimatorStateMachine() { name = prefix + ":2" };
            
            var s1 = new AnimatorState() { name = prefix + ":1" };
            var s2 = new AnimatorState() { name = prefix + ":2" };
            
            sm1.states = new[] { new ChildAnimatorState() { state = s1 } };
            sm2.states = new[] { new ChildAnimatorState() { state = s2 } };
            sm1.defaultState = s1;
            sm2.defaultState = s2;

            var lc = ScriptableObject.CreateInstance<VRCAnimatorLayerControl>();
            lc.layer = 1;
            lc.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
            
            s1.behaviours = new StateMachineBehaviour[] { lc };

            ac.layers = new[]
            {
                new AnimatorControllerLayer() { stateMachine = sm1, name = prefix + ":1" },
                new AnimatorControllerLayer() { stateMachine = sm2, name = prefix + ":2" }
            };

            return ac;
        }
    }
}

#endif