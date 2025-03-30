#if NDMF_VRCSDK3_AVATARS

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.UnitTestSupport;
using NUnit.Framework;
using UnitTests;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class StateBehaviorReactivationTest : TestBase
{
    [Test]
    public void TestMatrix(
        [Values(false, true)] bool shouldReactivate,
        [Values(false, true)] bool useVirtualizedController
        )
    {
        var root = CreatePrefab("../TestAssets/EmptyAvatar.prefab");
        var controller = LoadAsset<AnimatorController>("ComplicatedAnimatorController.controller");
        var avDesc = root.GetComponent<VRCAvatarDescriptor>();

        VirtualizedComponent vc = null;
        if (useVirtualizedController)
        {
            vc = root.AddComponent<VirtualizedComponent>();
            vc.AnimatorController = controller;
            vc.TargetControllerKey = VRCAvatarDescriptor.AnimLayerType.FX;
        }
        else
        {
            SetBaseLayer(avDesc, controller, VRCAvatarDescriptor.AnimLayerType.FX);
        }
        
        var context = CreateContext(root);
        var ec = context.ActivateExtensionContext<VirtualControllerContext>();

        AssertVirtualState(ec);

        context.DeactivateAllExtensionContexts();

        AssertPhysicalState();
        
        if (shouldReactivate)
        {
            ec = context.ActivateExtensionContext<VirtualControllerContext>();

            AssertVirtualState(ec);
            
            context.DeactivateAllExtensionContexts();
            
            AssertPhysicalState();
        }

        VRCAnimatorLayerControl alc(StateMachineBehaviour b)
        {
            return (VRCAnimatorLayerControl)b;
        }

        void AssertVirtualState(VirtualControllerContext virtualControllerContext)
        {
            var virtualController = virtualControllerContext.Controllers[useVirtualizedController ? (object)vc : (object)VRCAvatarDescriptor.AnimLayerType.FX];
            var layers = virtualController.Layers.ToList();
            Assert.AreEqual(layers[0].VirtualLayerIndex, alc(layers[0].StateMachine.Behaviours[0]).layer);
            Assert.AreEqual(100, alc(layers[0].StateMachine.Behaviours[1]).layer);
            Assert.AreEqual(layers[1].VirtualLayerIndex, alc(layers[1].StateMachine.DefaultState.Behaviours[0]).layer);
            Assert.AreEqual(layers[2].VirtualLayerIndex, alc(layers[2].SyncedLayerBehaviourOverrides[layers[1].StateMachine.DefaultState][0]).layer);
        }
        
        void AssertPhysicalState()
        {
            var controller = (AnimatorController)(useVirtualizedController
                ? vc.AnimatorController
                : avDesc.baseAnimationLayers.First(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                    .animatorController);
            
            var layers = controller.layers.ToList();
            Assert.AreEqual(0, alc(layers[0].stateMachine.behaviours[0]).layer);
            Assert.AreEqual(100, alc(layers[0].stateMachine.behaviours[1]).layer);
            Assert.AreEqual(1, alc(layers[1].stateMachine.defaultState.behaviours[0]).layer);
            Assert.AreEqual(2, alc(layers[2].GetOverrideBehaviours(layers[1].stateMachine.defaultState)[0]).layer);
        }
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
}


#endif