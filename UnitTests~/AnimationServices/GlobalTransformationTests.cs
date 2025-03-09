using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.util;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class GlobalTransformationTests : TestBase
    {
        [Test]
        public void TestPruningEmptyLayers()
        {
            AnimatorController ac = new AnimatorController();
            ac.layers = new[]
            {
                new AnimatorControllerLayer { stateMachine = new AnimatorStateMachine(),name = "0" },
                new AnimatorControllerLayer { stateMachine = NotEmpty("1"), name="1" },
                new AnimatorControllerLayer { stateMachine = new AnimatorStateMachine(),name = "2" },
                new AnimatorControllerLayer { stateMachine = NotEmpty("3"), name = "3" },
                new AnimatorControllerLayer { syncedLayerIndex = 3, name = "4" },
            };

            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vac = cloneContext.Clone(ac);

            GlobalTransformations.RemoveEmptyLayers(vac);
            
            Assert.AreEqual(4, vac.Layers.Count());
            Assert.AreEqual("0", vac.Layers.ElementAt(0).Name);
            Assert.AreEqual("1", vac.Layers.ElementAt(1).Name);
            Assert.AreEqual("3", vac.Layers.ElementAt(2).Name);
            Assert.AreEqual("4", vac.Layers.ElementAt(3).Name);
        }

        [Test]
        public void TestPruningASC()
        {
            AnimatorController ac = new AnimatorController();
            ac.layers = new[]
            {
                new AnimatorControllerLayer { stateMachine = new AnimatorStateMachine() { name = "0" } },
                new AnimatorControllerLayer { stateMachine = NotEmpty("1") },
                new AnimatorControllerLayer { stateMachine = new AnimatorStateMachine() { name = "2" } },
                new AnimatorControllerLayer { stateMachine = NotEmpty("3") },
                new AnimatorControllerLayer { syncedLayerIndex = 3, name = "4" },
            };

            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var vac = cloneContext.Clone(ac);

            var root = CreateRoot("root");
            var buildContext = new BuildContext(root, null);
            var asc = buildContext.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            asc.ControllerContext.Controllers["x"] = vac;

            asc.RemoveEmptyLayers();
            
            Assert.AreEqual(4, vac.Layers.Count());
        }

        private AnimatorStateMachine NotEmpty(string name)
        {
            var sm = new AnimatorStateMachine() { name = name };
            var st = new AnimatorState();
            var motion = new AnimationClip();

            st.motion = motion;
            sm.states = new[] {new ChildAnimatorState {state = st}};
            sm.defaultState = st;
            return sm;
        }
    }
}