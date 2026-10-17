using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;

namespace UnitTests
{
    internal sealed class LazyLifecycleState
    {
        internal readonly List<string> Events = new();
    }

    [LazyExtension]
    internal sealed class LazyRequiredContext : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            context.GetState<LazyLifecycleState>().Events.Add("required activate");
        }

        public void OnDeactivate(BuildContext context)
        {
            context.GetState<LazyLifecycleState>().Events.Add("required deactivate");
        }
    }

    [LazyExtension]
    [DependsOnContext(typeof(LazyRequiredContext))]
    internal sealed class LazyDependentContext : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            context.GetState<LazyLifecycleState>().Events.Add("dependent activate");
        }

        public void OnDeactivate(BuildContext context)
        {
            context.GetState<LazyLifecycleState>().Events.Add("dependent deactivate");
        }
    }

    [DependsOnContext(typeof(LazyDependentContext))]
    internal sealed class LazyLifecyclePass : Pass<LazyLifecyclePass>
    {
        protected override void Execute(BuildContext context)
        {
        }
    }

    [RunsOnAllPlatforms]
    internal sealed class LazyLifecyclePlugin : Plugin<LazyLifecyclePlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).Run(LazyLifecyclePass.Instance);
        }
    }

    public class LazyExtensionTests : TestBase
    {
        private static ConcretePass ResolvePass()
        {
            var resolver = new PluginResolver(new IPluginInternal[] { new LazyLifecyclePlugin() },
                GenericPlatform.Instance);
            return resolver.Passes.SelectMany(phase => phase.Item2)
                .Single(pass => pass.InstantiatedPass is LazyLifecyclePass);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnusedContextsSkipBothCallbacks(bool singleStep)
        {
            var context = CreateContext(CreateRoot("lazy"));
            var pass = ResolvePass();
            if (singleStep)
            {
                foreach (var type in pass.ActivatePlugins)
                {
                    context.RunExtensionContextActivation(pass, type);
                }
                context.RunPassBody(pass);
            }
            else
            {
                context.RunPass(pass);
            }

            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.Empty);
            context.DeactivateAllExtensionContexts();
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.Empty);
            Assert.Throws<Exception>(() => context.Extension<LazyDependentContext>());
        }

        [Test]
        public void FirstAccessInitializesDependenciesAndReactivationDefersAgain()
        {
            var context = CreateContext(CreateRoot("lazy"));
            var pass = ResolvePass();
            context.RunPass(pass);

            var extension = context.Extension<LazyDependentContext>();
            Assert.That(context.Extension<LazyDependentContext>(), Is.SameAs(extension));
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.EqualTo(new[]
            {
                "required activate", "dependent activate"
            }));

            context.DeactivateAllExtensionContexts();
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.EqualTo(new[]
            {
                "required activate", "dependent activate", "dependent deactivate", "required deactivate"
            }));

            context.GetState<LazyLifecycleState>().Events.Clear();
            context.RunPass(pass);
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.Empty);
            Assert.That(context.Extension<LazyDependentContext>(), Is.SameAs(extension));
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.EqualTo(new[]
            {
                "required activate", "dependent activate"
            }));
            context.DeactivateAllExtensionContexts();
        }

        [Test]
        public void ExplicitActivationInitializesImmediately()
        {
            var context = CreateContext(CreateRoot("lazy"));
            var extension = context.ActivateExtensionContext<LazyRequiredContext>();
            Assert.That(context.Extension<LazyRequiredContext>(), Is.SameAs(extension));
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.EqualTo(new[] { "required activate" }));
            context.DeactivateAllExtensionContexts();

            context.GetState<LazyLifecycleState>().Events.Clear();
            context.RunPass(ResolvePass());
            context.ActivateExtensionContext<LazyDependentContext>();
            Assert.That(context.GetState<LazyLifecycleState>().Events, Is.EqualTo(new[]
            {
                "required activate", "dependent activate"
            }));
            context.DeactivateAllExtensionContexts();
        }
    }
}
