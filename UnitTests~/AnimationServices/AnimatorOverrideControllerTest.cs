using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class AnimatorOverrideControllerTest
    {
        [Test]
        public void TestSimpleOverride()
        {
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var originalController = new AnimatorController();
            var originalStateMachine = new AnimatorStateMachine();
            originalController.layers = new[] {new AnimatorControllerLayer {stateMachine = originalStateMachine}};
            
            var clip1 = new AnimationClip {name = "c1"};
            var clip2 = new AnimationClip {name = "c2"};
            
            var s1 = new AnimatorState {name = "s1", motion = clip1};
            var s2 = new AnimatorState {name = "s2", motion = clip2};
            
            originalStateMachine.states = new[] {new ChildAnimatorState {state = s1}, new ChildAnimatorState {state = s2}};
            originalStateMachine.defaultState = s1;
            
            var overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = originalController;
            
            var clip3 = new AnimationClip {name = "c3"};
            overrideController[clip1] = clip3;
            
            var virtualController = cloneContext.Clone(overrideController);
            var virtualStateMachine = virtualController.Layers.First().StateMachine;
            var virtualS1 = virtualStateMachine.States.First(s => s.State.Name == "s1");
            var virtualS2 = virtualStateMachine.States.First(s => s.State.Name == "s2");
            
            Assert.AreEqual("c3", virtualS1.State.Motion.Name);
            Assert.AreEqual("c2", virtualS2.State.Motion.Name);
        }

        /// <summary>
        /// Regression test for https://github.com/bdunderscore/ndmf/issues/800
        /// When an AOC substitutes c1 with c2, the ObjectRegistry must record that the committed clone
        /// replaces c2 (the override clip), not c1 (the original clip in the base controller).
        /// </summary>
        [Test]
        public void ObjectRegistry_MapsOverrideClipNotOriginalClip()
        {
            var objectRegistry = new ObjectRegistry(null);
            var reg = (IObjectRegistry)objectRegistry;

            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var originalController = new AnimatorController();
            var originalStateMachine = new AnimatorStateMachine();
            originalController.layers = new[] {new AnimatorControllerLayer {stateMachine = originalStateMachine}};

            var c1 = new AnimationClip {name = "c1"};
            var c2 = new AnimationClip {name = "c2"};

            var s1 = new AnimatorState {name = "s1", motion = c1};
            originalStateMachine.states = new[] {new ChildAnimatorState {state = s1}};
            originalStateMachine.defaultState = s1;

            var overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = originalController;
            overrideController[c1] = c2;

            VirtualAnimatorController virtualController;
            AnimatorController committed;

            using (new ObjectRegistryScope(objectRegistry))
            {
                virtualController = cloneContext.Clone(overrideController);

                var commitContext = new CommitContext();
                commitContext.NodeToReference = cloneContext.NodeToReference;
                committed = commitContext.CommitObject(virtualController);
            }

            var committedMotion = (AnimationClip) committed.layers[0].stateMachine.defaultState.motion;

            // The committed clip must resolve to c2's reference, not c1's.
            Assert.AreEqual(reg.GetReference(c2), reg.GetReference(committedMotion));
        }

        [Test]
        public void TestBlendTreeChildOverride()
        {
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            
            var originalController = new AnimatorController();
            var originalStateMachine = new AnimatorStateMachine();
            originalController.layers = new[] {new AnimatorControllerLayer {stateMachine = originalStateMachine}};
            originalController.AddParameter("Blend", AnimatorControllerParameterType.Float);
            
            var clip1 = new AnimationClip {name = "c1"};
            var clip2 = new AnimationClip {name = "c2"};
            var clip3 = new AnimationClip {name = "c3"};
            var bt = new BlendTree {name = "bt", blendType = BlendTreeType.Simple1D};
            bt.children = new[]
            {
                new ChildMotion {motion = clip1, timeScale = 1},
                new ChildMotion {motion = clip2, timeScale = 1}
            };
            
            var s1 = new AnimatorState {name = "s1", motion = bt};
            originalStateMachine.states = new[] {new ChildAnimatorState {state = s1}};
            originalStateMachine.defaultState = s1;
            
            var overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = originalController;
            overrideController[clip1] = clip3;
            
            var virtualController = cloneContext.Clone(overrideController);
            var virtualStateMachine = virtualController.Layers.First().StateMachine;
            var virtualS1 = virtualStateMachine.States.First(s => s.State.Name == "s1");
            var virtualBlendTree = (VirtualBlendTree) virtualS1.State.Motion;
            
            Assert.AreEqual("c3", virtualBlendTree.Children.First().Motion.Name);
            Assert.AreEqual("c2", virtualBlendTree.Children.Last().Motion.Name);
        }
        /// <summary>
        /// Regression test for https://github.com/bdunderscore/ndmf/issues/828
        /// When an AOC swaps two clips (c1 -> c2 and c2 -> c1), both states must end up on the swapped
        /// clip. Previously, cloning the first state cached the mapped clone under the target clip's key,
        /// so the second state's lookup short-circuited before mapping was applied and both states ended
        /// up playing the same (second) clip.
        /// </summary>
        [Test]
        public void TestSwappedOverrides()
        {
            var objectRegistry = new ObjectRegistry(null);
            var reg = (IObjectRegistry)objectRegistry;

            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var originalController = new AnimatorController();
            var originalStateMachine = new AnimatorStateMachine();
            originalController.layers = new[] {new AnimatorControllerLayer {stateMachine = originalStateMachine}};

            var c1 = new AnimationClip {name = "c1"};
            var c2 = new AnimationClip {name = "c2"};

            var s1 = new AnimatorState {name = "s1", motion = c1};
            var s2 = new AnimatorState {name = "s2", motion = c2};
            originalStateMachine.states = new[] {new ChildAnimatorState {state = s1}, new ChildAnimatorState {state = s2}};
            originalStateMachine.defaultState = s1;

            var overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = originalController;
            // Swap the two clips in both directions.
            overrideController[c1] = c2;
            overrideController[c2] = c1;

            VirtualAnimatorController virtualController;
            AnimatorController committed;

            using (new ObjectRegistryScope(objectRegistry))
            {
                virtualController = cloneContext.Clone(overrideController);

                var commitContext = new CommitContext();
                commitContext.NodeToReference = cloneContext.NodeToReference;
                committed = commitContext.CommitObject(virtualController);
            }

            var stateMachine = committed.layers[0].stateMachine;
            var committedS1 = (AnimationClip) stateMachine.states.First(s => s.state.name == "s1").state.motion;
            var committedS2 = (AnimationClip) stateMachine.states.First(s => s.state.name == "s2").state.motion;

            Assert.AreEqual("c2", committedS1.name);
            Assert.AreEqual("c1", committedS2.name);
            Assert.AreNotSame(committedS1, committedS2);

            // Each state's clip must resolve to the reference of its mapped (swapped) source.
            Assert.AreEqual(reg.GetReference(c2), reg.GetReference(committedS1));
            Assert.AreEqual(reg.GetReference(c1), reg.GetReference(committedS2));
        }
        [Test]
        public void TestSwappedOverridesInBlendTree()
        {
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var originalController = new AnimatorController();
            originalController.AddParameter("Blend", AnimatorControllerParameterType.Float);
            var originalStateMachine = new AnimatorStateMachine();
            originalController.layers = new[] {new AnimatorControllerLayer {stateMachine = originalStateMachine}};

            var c1 = new AnimationClip {name = "c1"};
            var c2 = new AnimationClip {name = "c2"};

            var bt1 = new BlendTree {name = "bt1", blendType = BlendTreeType.Simple1D};
            bt1.children = new[] {new ChildMotion {motion = c1, timeScale = 1}, new ChildMotion {motion = c2, timeScale = 1}};
            var bt2 = new BlendTree {name = "bt2", blendType = BlendTreeType.Simple1D};
            bt2.children = new[] {new ChildMotion {motion = c2, timeScale = 1}, new ChildMotion {motion = c1, timeScale = 1}};

            var s1 = new AnimatorState {name = "s1", motion = bt1};
            var s2 = new AnimatorState {name = "s2", motion = bt2};
            originalStateMachine.states = new[] {new ChildAnimatorState {state = s1}, new ChildAnimatorState {state = s2}};
            originalStateMachine.defaultState = s1;

            var overrideController = new AnimatorOverrideController();
            overrideController.runtimeAnimatorController = originalController;
            overrideController[c1] = c2;
            overrideController[c2] = c1;

            var virtualController = cloneContext.Clone(overrideController);
            var stateMachine = virtualController.Layers.First().StateMachine;
            var vbt1 = (VirtualBlendTree) stateMachine.States.Single(s => s.State.Name == "s1").State.Motion;
            var vbt2 = (VirtualBlendTree) stateMachine.States.Single(s => s.State.Name == "s2").State.Motion;

            Assert.AreEqual("c2", vbt1.Children.First().Motion.Name);
            Assert.AreEqual("c1", vbt1.Children.Last().Motion.Name);
            Assert.AreEqual("c1", vbt2.Children.First().Motion.Name);
            Assert.AreEqual("c2", vbt2.Children.Last().Motion.Name);
        }
    }
}