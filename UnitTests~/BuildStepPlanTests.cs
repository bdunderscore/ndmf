#nullable enable

using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;

namespace UnitTests
{
    internal sealed class SingleStepTestContext : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
        }

        public void OnDeactivate(BuildContext context)
        {
        }
    }

    [DependsOnContext(typeof(SingleStepTestContext))]
    internal sealed class SingleStepContextPass : Pass<SingleStepContextPass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepPlanPlugin : Plugin<SingleStepPlanPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run(SingleStepContextPass.Instance)
                .Then.Run("Pass without extension context", _ => { });
        }
    }

    internal sealed class SingleStepBookmarkPass : Pass<SingleStepBookmarkPass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    internal sealed class SingleStepGroupingContext : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
        }

        public void OnDeactivate(BuildContext context)
        {
        }
    }

    [DependsOnContext(typeof(SingleStepGroupingContext))]
    internal sealed class SingleStepGroupingPriorPass : Pass<SingleStepGroupingPriorPass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    [DependsOnContext(typeof(SingleStepGroupingContext))]
    internal sealed class SingleStepGroupingFollowerPass : Pass<SingleStepGroupingFollowerPass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepGroupingPriorPlugin : Plugin<SingleStepGroupingPriorPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run(SingleStepGroupingPriorPass.Instance);
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepGroupingFollowerPlugin : Plugin<SingleStepGroupingFollowerPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin<SingleStepGroupingPriorPlugin>()
                .Run(SingleStepGroupingFollowerPass.Instance)
                .Then.Run("Grouping trailing pass", _ => { });
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepGroupingImmediateFollowerPlugin
        : Plugin<SingleStepGroupingImmediateFollowerPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin<SingleStepGroupingPriorPlugin>()
                .Run("Immediate grouping follower", _ => { });
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepGroupingActivatingPlugin : Plugin<SingleStepGroupingActivatingPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Grouping leading pass", _ => { })
                .Then.Run(SingleStepGroupingPriorPass.Instance);
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepGroupingActivatingFollowerPlugin
        : Plugin<SingleStepGroupingActivatingFollowerPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .AfterPlugin<SingleStepGroupingActivatingPlugin>()
                .Run(SingleStepGroupingFollowerPass.Instance)
                .Then.Run("Grouping trailing pass", _ => { });
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepBookmarkPlugin : Plugin<SingleStepBookmarkPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run(SingleStepBookmarkPass.Instance);
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepBookmarkWithLeadingPlugin : Plugin<SingleStepBookmarkWithLeadingPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .Run("Leading pass", _ => { })
                .Then.Run(SingleStepBookmarkPass.Instance);
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class SingleStepBookmarkRemovedPlugin : Plugin<SingleStepBookmarkRemovedPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Different pass", _ => { });
        }
    }

    public class BuildStepPlanTests
    {
        [Test]
        public void NDMFBuildStepPlanFlattensContextTransitionsAndPasses()
        {
            var resolver = new PluginResolver(
                new IPluginInternal[] { new SingleStepPlanPlugin() },
                GenericPlatform.Instance
            );
            var plan = new BuildStepPlan(resolver);

            Assert.That(
                plan.Steps.Select(step => step.Kind),
                Is.EqualTo(new[]
                {
                    BuildStepKind.ActivateExtension,
                    BuildStepKind.ExecutePass,
                    BuildStepKind.DeactivateExtension,
                    BuildStepKind.ExecutePass,
                    BuildStepKind.Complete
                })
            );
            Assert.That(plan.Steps[0].ExtensionType, Is.EqualTo(typeof(SingleStepTestContext)));
            Assert.That(plan.Steps[2].ExtensionType, Is.EqualTo(typeof(SingleStepTestContext)));
            Assert.That(plan.CompleteStepIndex, Is.EqualTo(plan.Steps.Count - 1));
        }

        [Test]
        public void NDMFBuildStepGroupingCreatesPluginFoldouts()
        {
            var plan = new BuildStepPlan(new PluginResolver(
                new IPluginInternal[] { new SingleStepPlanPlugin() },
                GenericPlatform.Instance
            ));

            var groups = BuildStepGrouping.Group(plan.Steps);

            Assert.That(groups[0].IsFoldout, Is.True);
            Assert.That(groups[0].Steps.Count, Is.EqualTo(4));
            Assert.That(groups[0].Steps.Select(step => step.Kind), Is.EqualTo(new[]
            {
                BuildStepKind.ActivateExtension,
                BuildStepKind.ExecutePass,
                BuildStepKind.DeactivateExtension,
                BuildStepKind.ExecutePass
            }));
        }

        [Test]
        public void NDMFBuildStepGroupingSplitsAtADeactivationFromBeforeTheGroup()
        {
            var plan = ResolveGroupingPlan(
                new SingleStepGroupingPriorPlugin(),
                new SingleStepGroupingFollowerPlugin()
            );
            var groups = BuildStepGrouping.Group(plan.Steps);
            var followerPassGroup = groups.Single(group => group.Steps.Any(step =>
                step.Pass?.InstantiatedPass is SingleStepGroupingFollowerPass
            ));
            var deactivationGroup = groups.Single(group => group.Steps.Any(step =>
                step.Kind == BuildStepKind.DeactivateExtension
            ));

            Assert.That(deactivationGroup, Is.Not.SameAs(followerPassGroup));
            Assert.That(deactivationGroup.Steps[0].Kind, Is.EqualTo(BuildStepKind.DeactivateExtension));
            Assert.That(
                deactivationGroup.Steps.Any(step => step.Pass?.Description == "Grouping trailing pass"),
                Is.True
            );
        }

        [Test]
        public void NDMFBuildStepGroupingSplitsAtAnActivationLastingPastTheGroup()
        {
            var plan = ResolveGroupingPlan(
                new SingleStepGroupingActivatingPlugin(),
                new SingleStepGroupingActivatingFollowerPlugin()
            );
            var groups = BuildStepGrouping.Group(plan.Steps);
            var leadingPassGroup = groups.Single(group => group.Steps.Any(step =>
                step.Pass?.Description == "Grouping leading pass"
            ));
            var activationGroup = groups.Single(group => group.Steps.Any(step =>
                step.Kind == BuildStepKind.ActivateExtension
            ));

            Assert.That(activationGroup, Is.Not.SameAs(leadingPassGroup));
            Assert.That(activationGroup.Steps[0].Kind, Is.EqualTo(BuildStepKind.ActivateExtension));
            Assert.That(
                activationGroup.Steps.Any(step =>
                    step.Pass?.InstantiatedPass is SingleStepGroupingPriorPass
                ),
                Is.True
            );
        }

        [Test]
        public void NDMFBuildStepGroupingIncludesAnImmediatelyAdjacentDeactivation()
        {
            var plan = ResolveGroupingPlan(
                new SingleStepGroupingPriorPlugin(),
                new SingleStepGroupingImmediateFollowerPlugin()
            );
            var groups = BuildStepGrouping.Group(plan.Steps);
            var priorGroup = groups.Single(group => group.Steps.Any(step =>
                step.Pass?.InstantiatedPass is SingleStepGroupingPriorPass
            ));

            Assert.That(
                priorGroup.Steps.Any(step => step.Kind == BuildStepKind.DeactivateExtension),
                Is.True
            );
        }

        [Test]
        public void NDMFBuildStepBookmarkSurvivesAnInsertedStep()
        {
            var initialPlan = ResolveBookmarkPlan(new SingleStepBookmarkPlugin());
            var bookmarkedStep = initialPlan.Steps.Single(step =>
                step.Pass?.InstantiatedPass is SingleStepBookmarkPass
            );
            var bookmark = new BuildStepBookmark();
            bookmark.Set(bookmarkedStep);

            var updatedPlan = ResolveBookmarkPlan(new SingleStepBookmarkWithLeadingPlugin());

            Assert.That(bookmark.TryResolve(updatedPlan, out var remappedIndex), Is.True);
            Assert.That(remappedIndex, Is.GreaterThan(bookmarkedStep.Index));
            Assert.That(
                updatedPlan.Steps[remappedIndex].Pass?.InstantiatedPass,
                Is.TypeOf<SingleStepBookmarkPass>()
            );
        }

        [Test]
        public void NDMFBuildStepBookmarkRejectsARemovedStep()
        {
            var initialPlan = ResolveBookmarkPlan(new SingleStepBookmarkPlugin());
            var bookmark = new BuildStepBookmark();
            bookmark.Set(initialPlan.Steps.Single(step =>
                step.Pass?.InstantiatedPass is SingleStepBookmarkPass
            ));

            var updatedPlan = ResolveBookmarkPlan(new SingleStepBookmarkRemovedPlugin());

            Assert.That(bookmark.TryResolve(updatedPlan, out _), Is.False);
        }

        [Test]
        public void NDMFBuildStepBookmarkSurvivesUnitySerialization()
        {
            var plan = ResolveBookmarkPlan(new SingleStepBookmarkPlugin());
            var bookmarkedStep = plan.Steps.Single(step =>
                step.Pass?.InstantiatedPass is SingleStepBookmarkPass
            );
            var bookmark = new BuildStepBookmark();
            bookmark.Set(bookmarkedStep);

            var serialized = EditorJsonUtility.ToJson(bookmark);
            var restored = new BuildStepBookmark();
            EditorJsonUtility.FromJsonOverwrite(serialized, restored);

            Assert.That(restored.TryResolve(plan, out var restoredIndex), Is.True);
            Assert.That(restoredIndex, Is.EqualTo(bookmarkedStep.Index));
        }

        private static BuildStepPlan ResolveBookmarkPlan(IPluginInternal plugin)
        {
            return new BuildStepPlan(new PluginResolver(
                new[] { plugin },
                GenericPlatform.Instance
            ));
        }

        private static BuildStepPlan ResolveGroupingPlan(params IPluginInternal[] plugins)
        {
            return new BuildStepPlan(new PluginResolver(plugins, GenericPlatform.Instance));
        }
    }
}
