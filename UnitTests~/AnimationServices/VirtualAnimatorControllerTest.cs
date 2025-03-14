﻿using System.Linq;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Assert = UnityEngine.Assertions.Assert;

namespace UnitTests.AnimationServices
{
    public class VirtualAnimatorControllerTest : TestBase
    {
        [Test]
        public void PreservesParameters()
        {
            CloneContext context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            
            var controller = TrackObject(new AnimatorController());
            controller.AddParameter("x", AnimatorControllerParameterType.Float);
            controller.AddParameter("y", AnimatorControllerParameterType.Bool);
            
            var virtualController = context.Clone(controller);
            Assert.AreEqual(2, virtualController.Parameters.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, virtualController.Parameters["x"].type);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, virtualController.Parameters["y"].type);

            using (new AssertInvalidate(virtualController))
            {
                virtualController.Parameters = virtualController.Parameters.Add("z", new AnimatorControllerParameter()
                {
                    name = "ignored",
                    type = AnimatorControllerParameterType.Trigger
                });
            }

            var commitContext = new CommitContext();
            var committed = commitContext.CommitObject(virtualController);
            
            Assert.AreEqual(3, committed.parameters.Length);
            Assert.AreEqual("x", committed.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, committed.parameters[0].type);
            Assert.AreEqual("y", committed.parameters[1].name);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, committed.parameters[1].type);
            Assert.AreEqual("z", committed.parameters[2].name);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, committed.parameters[2].type);
            
            commitContext.DestroyAllImmediate();
        }

        [Test]
        public void HandlesNullStateMachineReferences()
        {
            CloneContext context = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var ac1 = TrackObject(new AnimatorController());
            
            var sm1 = new AnimatorStateMachine();
            var sm2 = new AnimatorStateMachine();
            
            ac1.layers = new[]
            {
                new AnimatorControllerLayer()
                {
                    name = "1",
                    stateMachine = sm1
                },
                new AnimatorControllerLayer()
                {
                    name = "2",
                    stateMachine = sm2
                }
            };
            
            UnityEngine.Object.DestroyImmediate(sm1);
            
            var vc1 = context.Clone(ac1);
            
            Assert.AreEqual(2, vc1.Layers.Count());
            Assert.IsNull(vc1.Layers.First().StateMachine);
            Assert.AreEqual("2", vc1.Layers.Skip(1).First().Name);
            
            Assert.AreEqual(1, vc1.Layers.First().AllReachableNodes().Count());
        }

        [Test]
        public void PreservesLayersAndReferences()
        {
            CloneContext context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            
            var ac1 = TrackObject(new AnimatorController());
            var ac2 = TrackObject(new AnimatorController());

            ac1.layers = new[]
            {
                new AnimatorControllerLayer()
                {
                    name = "1",
                    stateMachine = new AnimatorStateMachine() { name = "1" }
                },
                new AnimatorControllerLayer()
                {
                    name = "2",
                    syncedLayerIndex = 0,
                }
            };
            ac2.layers = new[]
            {
                new AnimatorControllerLayer()
                {
                    name = "3",
                    stateMachine = new AnimatorStateMachine() { name = "1" }
                },
                new AnimatorControllerLayer()
                {
                    name = "4",
                    syncedLayerIndex = 0
                }
            };
            
            var vc1 = context.Clone(ac1);
            var vc2 = context.Clone(ac2);

            foreach (var l in vc2.Layers)
            {
                using (new AssertInvalidate(vc1)) vc1.AddLayer(new LayerPriority(), l);
            }
            
            var commitContext = new CommitContext();
            var committed = commitContext.CommitObject(vc1);
            
            Assert.AreEqual(4, committed.layers.Length);
            Assert.AreEqual("1", committed.layers[0].name);
            Assert.AreEqual("2", committed.layers[1].name);
            Assert.AreEqual("3", committed.layers[2].name);
            Assert.AreEqual("4", committed.layers[3].name);
            
            Assert.AreEqual("1", committed.layers[0].stateMachine.name);
            Assert.AreEqual(-1, committed.layers[0].syncedLayerIndex);
            Assert.AreEqual(0, committed.layers[1].syncedLayerIndex);
            Assert.AreEqual(-1, committed.layers[2].syncedLayerIndex);
            Assert.AreEqual(2, committed.layers[3].syncedLayerIndex);
            
            commitContext.DestroyAllImmediate();
        }

        [Test]
        public void TestLayersGetterSetter()
        {
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var controller = VirtualAnimatorController.Create(cloneContext);

            var layerPrioZero = controller.AddLayer(new LayerPriority(0), "t0");
            var LayerPrioMinusTen = controller.AddLayer(new LayerPriority(-10), "t-10");
            var LayerPrioTen = controller.AddLayer(new LayerPriority(10), "t+10");
            
            var layers = controller.Layers.ToList();
            
            Assert.AreEqual(3, layers.Count);
            Assert.AreEqual("t-10", layers[0].Name);
            Assert.AreEqual("t0", layers[1].Name);
            Assert.AreEqual("t+10", layers[2].Name);

            controller.Layers = new[] { layers[2], layers[0] };
            
            layers = controller.Layers.ToList();
            
            Assert.AreEqual(2, layers.Count);
            
            Assert.AreEqual("t+10", layers[0].Name);
            Assert.AreEqual("t-10", layers[1].Name);
            
            // Priority zero is added at the end
            controller.AddLayer(new LayerPriority(0), "t0.1");
            
            layers = controller.Layers.ToList();
            
            Assert.AreEqual(3, layers.Count);
            Assert.AreEqual("t0.1", layers[2].Name);
            
            // We can re-add layer t0 with a different priority
            
            controller.AddLayer(new LayerPriority(-5), "t0");
            
            layers = controller.Layers.ToList();
            
            Assert.AreEqual(4, layers.Count);
            Assert.AreEqual("t0", layers[0].Name);
            Assert.AreEqual("t+10", layers[1].Name);
            Assert.AreEqual("t-10", layers[2].Name);
            Assert.AreEqual("t0.1", layers[3].Name);
        }
    }
}