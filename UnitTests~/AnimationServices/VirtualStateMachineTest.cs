using System;
using System.Linq;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class VirtualStateMachineTest : TestBase
    {
        private void AssertPreserveProperty(
            Action<AnimatorStateMachine> setup,
            Action<VirtualStateMachine> setupViaVirtualState,
            Action<AnimatorStateMachine> assert,
            Action<VirtualStateMachine> assertViaVirtualState
        )
        {
            var state = new AnimatorStateMachine();
            setup(state);

            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);

            var virtualState = cloneContext.Clone(state);
            assertViaVirtualState(virtualState);

            var commitContext = new CommitContext();
            var committed = commitContext.CommitObject(virtualState);
            Assert.AreNotEqual(state, committed);
            assert(committed);
            
            UnityEngine.Object.DestroyImmediate(state);
            UnityEngine.Object.DestroyImmediate(committed);

            state = new AnimatorStateMachine();

            virtualState = cloneContext.Clone(state);
            using (new AssertInvalidate(virtualState))
            {
                setupViaVirtualState(virtualState);
            }

            committed = commitContext.CommitObject(virtualState);
            
            assert(committed);
            
            UnityEngine.Object.DestroyImmediate(state);
            UnityEngine.Object.DestroyImmediate(committed);
        }
        
        [Test]
        public void PreservesName()
        {
            AssertPreserveProperty(
                state => state.name = "Test",
                virtualState => virtualState.Name = "Test",
                state => Assert.AreEqual("Test", state.name),
                virtualState => Assert.AreEqual("Test", virtualState.Name)
            );
        }

        [Test]
        public void PreservesAnyStatePosition()
        {
            AssertPreserveProperty(
                state => state.anyStatePosition = new Vector3(1, 2, 3),
                virtualState => virtualState.AnyStatePosition = new Vector3(1, 2, 3),
                state => Assert.AreEqual(new Vector3(1, 2, 3), state.anyStatePosition),
                virtualState => Assert.AreEqual(new Vector3(1, 2, 3), virtualState.AnyStatePosition)
            );
        }
        
        [Test]
        public void PreservesEntryPosition()
        {
            AssertPreserveProperty(
                state => state.entryPosition = new Vector3(1, 2, 3),
                virtualState => virtualState.EntryPosition = new Vector3(1, 2, 3),
                state => Assert.AreEqual(new Vector3(1, 2, 3), state.entryPosition),
                virtualState => Assert.AreEqual(new Vector3(1, 2, 3), virtualState.EntryPosition)
            );
        }
        
        [Test]
        public void PreservesExitPosition()
        {
            AssertPreserveProperty(
                state => state.exitPosition = new Vector3(1, 2, 3),
                virtualState => virtualState.ExitPosition = new Vector3(1, 2, 3),
                state => Assert.AreEqual(new Vector3(1, 2, 3), state.exitPosition),
                virtualState => Assert.AreEqual(new Vector3(1, 2, 3), virtualState.ExitPosition)
            );
        }
        
        [Test]
        public void PreservesParentStateMachinePosition()
        {
            AssertPreserveProperty(
                state => state.parentStateMachinePosition = new Vector3(1, 2, 3),
                virtualState => virtualState.ParentStateMachinePosition = new Vector3(1, 2, 3),
                state => Assert.AreEqual(new Vector3(1, 2, 3), state.parentStateMachinePosition),
                virtualState => Assert.AreEqual(new Vector3(1, 2, 3), virtualState.ParentStateMachinePosition)
            );
        }

        [Test]
        public void HandlesNullSubStateMachines()
        {
            var sm = TrackObject(new AnimatorStateMachine());
            var sm2 = TrackObject(new AnimatorStateMachine());
            var sm3 = TrackObject(new AnimatorStateMachine());
            var sm4 = TrackObject(new AnimatorStateMachine());
            
            sm2.name = "sm2";
            sm3.name = "sm3";
            sm4.name = "sm4";
            
            sm.stateMachines = new[]
            {
                new ChildAnimatorStateMachine() { stateMachine = sm2 },
                new ChildAnimatorStateMachine() { stateMachine = sm3 },
                new ChildAnimatorStateMachine() { stateMachine = sm4 }
            };
            
            UnityEngine.Object.DestroyImmediate(sm3);
            
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vsm = cloneContext.Clone(sm);
            
            Assert.AreEqual(2, vsm.StateMachines.Count);
            Assert.AreEqual("sm2", vsm.StateMachines[0].StateMachine.Name);
            Assert.AreEqual("sm4", vsm.StateMachines[1].StateMachine.Name);
        }

        [Test]
        public void AllStatesIteratesSubStateMachines()
        {
            var sm = TrackObject(new AnimatorStateMachine());
            var sm2 = TrackObject(new AnimatorStateMachine());
            sm2.name = "sm2";
            
            sm.stateMachines = new[]
            {
                new ChildAnimatorStateMachine() { stateMachine = sm2 }
            };
            
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vsm = cloneContext.Clone(sm);

            vsm.StateMachines[0].StateMachine.AddState("x");
            
            Assert.IsTrue(vsm.AllStates().Any(s => s.Name == "x"));
        }

        [Test]
        public void AllReachableNodesEnumeratesSubstateMachineTransitions()
        {
            var sm = TrackObject(new AnimatorStateMachine());
            var sm2 = TrackObject(new AnimatorStateMachine());
            
            sm.stateMachines = new[]
            {
                new ChildAnimatorStateMachine() { stateMachine = sm2 }
            };
            
            sm.SetStateMachineTransitions(sm2, new AnimatorTransition[]
            {
                new AnimatorTransition()
                {
                    name = "test",
                    isExit = true,
                }
            });
            
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vsm = cloneContext.Clone(sm);

            var node = vsm.AllReachableNodes().OfType<VirtualTransitionBase>().First();
            Assert.AreEqual("test", node.Name);
        }
    }
    
   
}