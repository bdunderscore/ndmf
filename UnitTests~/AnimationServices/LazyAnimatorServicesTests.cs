using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    [DependsOnContext(typeof(AnimatorServicesContext))]
    internal sealed class LazyAnimatorServicesPass : Pass<LazyAnimatorServicesPass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class LazyAnimatorServicesPlugin : Plugin<LazyAnimatorServicesPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run(LazyAnimatorServicesPass.Instance);
        }
    }

    public class LazyAnimatorServicesTests : TestBase
    {
        private static ConcretePass[] ResolvePasses()
        {
            var resolver = new PluginResolver(new IPluginInternal[] { new LazyAnimatorServicesPlugin() },
                GenericPlatform.Instance);
            return resolver.Passes.SelectMany(phase => phase.Item2).ToArray();
        }

        private Animator CreateAnimatedRoot()
        {
            var root = TrackObject(new GameObject("root"));
            var animator = root.AddComponent<Animator>();
            CreateChild(root, "child");

            var clip = TrackObject(new AnimationClip());
            var binding = EditorCurveBinding.FloatCurve("child", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1, 1));

            var state = TrackObject(new AnimatorState { name = "state", motion = clip });
            var stateMachine = TrackObject(new AnimatorStateMachine());
            stateMachine.states = new[] { new ChildAnimatorState { state = state } };
            stateMachine.defaultState = state;
            var controller = TrackObject(new AnimatorController());
            controller.layers = new[]
            {
                new AnimatorControllerLayer { name = "layer", stateMachine = stateMachine, defaultWeight = 1 }
            };
            animator.runtimeAnimatorController = controller;
            return animator;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnusedAnimatorServicesDoNotCommit(bool accessPathRemapper)
        {
            var animator = CreateAnimatedRoot();
            var original = animator.runtimeAnimatorController;
            var context = CreateContext(animator.gameObject);
            var passes = ResolvePasses();
            context.RunPass(passes[0]);
            if (accessPathRemapper)
            {
                var remapper = context.Extension<AnimatorServicesContext>().ObjectPathRemapper;
                Assert.That(remapper.GetObjectForPath("child"),
                    Is.SameAs(animator.transform.Find("child").gameObject));
            }

            context.RunPass(passes[1]);

            Assert.That(animator.runtimeAnimatorController, Is.SameAs(original));
        }

        [Test]
        public void AnimationIndexAccessInitializesAndUnusedReactivationSkipsCommit()
        {
            var animator = CreateAnimatedRoot();
            var original = animator.runtimeAnimatorController;
            var context = CreateContext(animator.gameObject);
            var passes = ResolvePasses();
            context.RunPass(passes[0]);
            var services = context.Extension<AnimatorServicesContext>();
            var index = services.AnimationIndex;

            Assert.That(services.AnimationIndex, Is.SameAs(index));
            Assert.That(services.ControllerContext, Is.SameAs(context.Extension<VirtualControllerContext>()));
            Assert.That(index.GetClipsForObjectPath("child").Count(), Is.EqualTo(1));
            context.RunPass(passes[1]);
            Assert.That(animator.runtimeAnimatorController, Is.Not.SameAs(original));

            var committed = animator.runtimeAnimatorController;
            context.RunPass(passes[0]);
            context.RunPass(passes[1]);
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(committed));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HierarchyChangesAreAppliedWithoutAccessingServices(bool deleteChild)
        {
            var animator = CreateAnimatedRoot();
            var originalClip = animator.runtimeAnimatorController.animationClips.Single();
            var context = CreateContext(animator.gameObject);
            var passes = ResolvePasses();
            context.RunPass(passes[0]);
            var child = animator.transform.Find("child");
            if (deleteChild)
            {
                Object.DestroyImmediate(child.gameObject);
            }
            else
            {
                child.name = "renamed";
            }

            context.RunPass(passes[1]);

            var clip = animator.runtimeAnimatorController.animationClips.Single();
            Assert.That(AnimationUtility.GetCurveBindings(clip).Select(binding => binding.path),
                Is.EqualTo(deleteChild ? new string[0] : new[] { "renamed" }));
            Assert.That(AnimationUtility.GetCurveBindings(originalClip).Single().path, Is.EqualTo("child"));
        }
    }
}
