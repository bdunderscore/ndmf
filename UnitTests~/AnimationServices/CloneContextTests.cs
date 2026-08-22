using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace UnitTests.AnimationServices
{
    public class CloneContextTests : TestBase
    {
#if NDMF_VRCSDK3_AVATARS
        private class TestPlatform : IPlatformAnimatorBindings
        {
            public object innateKey;

            public void VirtualizeStateBehaviour(CloneContext context, StateMachineBehaviour behaviour)
            {
                innateKey = context.ActiveInnateLayerKey;
            }
        }
        
        [Test]
        public void TestInnateKeyOverride()
        {
            var controller = new AnimatorController();
            var sm = new AnimatorStateMachine();
            sm.behaviours = new[] { ScriptableObject.CreateInstance<VRCAnimatorLayerControl>() };
            controller.layers = new[] { new AnimatorControllerLayer { stateMachine = sm } };

            var p = new TestPlatform();
            var context = new CloneContext(p);
            context.Clone(controller, "hello, world");
            
            Assert.AreEqual("hello, world", p.innateKey);
        }
#endif

        [Test]
        public void DistinctScopeRestoresPriorCloneCache()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var source = new AnimationClip();
            var originalClone = context.Clone(source);

            using (context.PushDistinctScope())
            {
                Assert.AreNotSame(originalClone, context.Clone(source));
            }

            Assert.AreSame(originalClone, context.Clone(source));
        }
    }
}