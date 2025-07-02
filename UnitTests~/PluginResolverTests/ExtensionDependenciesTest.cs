using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;

namespace UnitTests.PluginResolverTests
{
    [DependsOnContext(typeof(Ctx1))]
    public class Ctx4 : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            
        }

        public void OnDeactivate(BuildContext context)
        {
            
        }
    }
    
    [DependsOnContext(typeof(Ctx3))]
    [DependsOnContext(typeof(Ctx2))]
    public class Ctx1 : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            
        }

        public void OnDeactivate(BuildContext context)
        {
            
        }
    }
    
    public class Ctx2 : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            
        }

        public void OnDeactivate(BuildContext context)
        {
            
        }
    }
    
    public class Ctx3 : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
            
        }

        public void OnDeactivate(BuildContext context)
        {
            
        }
    }

    // Additional test contexts and passes for CompatibleWithContext tests
    public class Ctx5 : IExtensionContext
    {
        public void OnActivate(BuildContext context) { }
        public void OnDeactivate(BuildContext context) { }
    }

    [DependsOnContext(typeof(Ctx4))]
    public class Ctx5DependsOnCtx4 : Ctx5 { }

    [DependsOnContext(typeof(Ctx4))]
    public class Pass3 : Pass<Pass3>
    {
        protected override void Execute(BuildContext context) { }
    }

    [DependsOnContext(typeof(Ctx4))]
    public class Pass1 : Pass<Pass1>
    {
        protected override void Execute(BuildContext context)
        {
            
        }
    }
    
    [CompatibleWithContext(typeof(Ctx4))]
    public class CompatiblePass : Pass<CompatiblePass>
    {
        protected override void Execute(BuildContext context) { }
    }

    [CompatibleWithContext(typeof(Ctx5DependsOnCtx4))]
    public class TransitiveCompatiblePass : Pass<TransitiveCompatiblePass>
    {
        protected override void Execute(BuildContext context) { }
    }

    public class IncompatiblePass : Pass<IncompatiblePass>
    {
        protected override void Execute(BuildContext context) { }
    }

    [RunsOnAllPlatforms]
    public class Plugin1 : Plugin<Plugin1>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Generating);
            seq.Run(Pass1.Instance);
            seq.WithCompatibleExtension(typeof(Ctx4), seq =>
                {
                    seq.Run("test test", _ => { });
                });
            seq.Run("deactivate test", _ => { });
        }
    }
    
    [RunsOnAllPlatforms]
    public class PluginWithCompatiblePass : Plugin<PluginWithCompatiblePass>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Generating);
            seq.Run(Pass1.Instance);
            seq.Run(CompatiblePass.Instance);
            seq.Run(Pass3.Instance);
        }
    }

    [RunsOnAllPlatforms]
    public class PluginWithIncompatiblePass : Plugin<PluginWithIncompatiblePass>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Generating);
            seq.Run(Pass1.Instance);
            seq.Run(IncompatiblePass.Instance);
            seq.Run(Pass3.Instance);
        }
    }

    [RunsOnAllPlatforms]
    public class PluginWithTransitiveCompatiblePass : Plugin<PluginWithTransitiveCompatiblePass>
    {
        protected override void Configure()
        {
            var seq = InPhase(BuildPhase.Generating);
            seq.Run(Pass1.Instance);
            seq.Run(TransitiveCompatiblePass.Instance);
            seq.Run(Pass3.Instance);
        }
    }

    public class ExtensionDependenciesTest
    {
        [Test]
        public void AssertCorrectPassDependencies()
        {
            var resolver = new PluginResolver(new[] { typeof(Plugin1) }, GenericPlatform.Instance);

            var phase = resolver.Passes.First(p => p.Item1 == BuildPhase.Generating).Item2;
            var pass1 = phase.First(pass => pass.InstantiatedPass is Pass1);
            
            Assert.AreEqual(pass1.ActivatePlugins, (new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1), typeof(Ctx4) }).ToImmutableList());
            Assert.That(pass1.DeactivatePlugins, Is.Empty);
            
            var pass2 = phase.First(pass => pass.InstantiatedPass.DisplayName == "test test");
            Assert.That(pass2.ActivatePlugins.IsEmpty);
            Assert.That(pass2.DeactivatePlugins.IsEmpty);
            
            var pass3 = phase.First(pass => pass.InstantiatedPass.DisplayName == "deactivate test");
            Assert.That(pass3.ActivatePlugins, Is.Empty);
            Assert.AreEqual(pass3.DeactivatePlugins, (new[] { typeof(Ctx4), typeof(Ctx1), typeof(Ctx3), typeof(Ctx2) }).ToImmutableList());
        }

        [Test]
        public void CompatibleWithContext_ActivatesOnceWithCompatiblePass()
        {
            // Pass1 depends on Ctx4, Pass2 is compatible with Ctx4, Pass3 depends on Ctx4
            var resolver = new PluginResolver(new[] { typeof(PluginWithCompatiblePass) }, GenericPlatform.Instance);
            var phase = resolver.Passes.First(p => p.Item1 == BuildPhase.Generating).Item2;
            var pass1 = phase.First(pass => pass.InstantiatedPass is Pass1);
            var pass2 = phase.First(pass => pass.InstantiatedPass is CompatiblePass);
            var pass3 = phase.First(pass => pass.InstantiatedPass is Pass3);

            // Ctx4 should be activated before pass1, not deactivated for pass2, and still active for pass3
            Assert.That(pass1.ActivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1), typeof(Ctx4) }));
            Assert.That(pass2.ActivatePlugins, Is.Empty);
            Assert.That(pass2.DeactivatePlugins, Is.Empty);
            Assert.That(pass3.ActivatePlugins, Is.Empty); }

        [Test]
        public void CompatibleWithContext_DeactivatesIfNotCompatible()
        {
            // Pass1 depends on Ctx4, Pass2 is not compatible, Pass3 depends on Ctx4
            var resolver = new PluginResolver(new[] { typeof(PluginWithIncompatiblePass) }, GenericPlatform.Instance);
            var phase = resolver.Passes.First(p => p.Item1 == BuildPhase.Generating).Item2;
            var pass1 = phase.First(pass => pass.InstantiatedPass is Pass1);
            var pass2 = phase.First(pass => pass.InstantiatedPass is IncompatiblePass);
            var pass3 = phase.First(pass => pass.InstantiatedPass is Pass3);

            // Ctx4 should be deactivated after pass1, reactivated before pass3
            Assert.That(pass1.ActivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1), typeof(Ctx4) }));
            Assert.That(pass1.DeactivatePlugins, Is.Empty);
            Assert.That(pass2.ActivatePlugins, Is.Empty);
            Assert.That(pass2.DeactivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx4), typeof(Ctx1), typeof(Ctx3), typeof(Ctx2) }));
            Assert.That(pass3.ActivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1), typeof(Ctx4) }));
        }

        [Test]
        public void CompatibleWithContext_TransitiveCompatibilityPreventsDeactivation()
        {
            // Pass1 depends on Ctx4, Pass2 is compatible with Ctx5 (which depends on Ctx4), Pass3 depends on Ctx4
            var resolver = new PluginResolver(new[] { typeof(PluginWithTransitiveCompatiblePass) }, GenericPlatform.Instance);
            var phase = resolver.Passes.First(p => p.Item1 == BuildPhase.Generating).Item2;
            var pass1 = phase.First(pass => pass.InstantiatedPass is Pass1);
            var pass2 = phase.First(pass => pass.InstantiatedPass is TransitiveCompatiblePass);
            var pass3 = phase.First(pass => pass.InstantiatedPass is Pass3);

            // Ctx4 should not be deactivated for pass2, since Ctx5 depends on Ctx4
            Assert.That(pass1.ActivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1), typeof(Ctx4) }));
            Assert.That(pass2.ActivatePlugins, Is.Empty);
            Assert.That(pass2.DeactivatePlugins, Is.Empty);
            Assert.That(pass3.ActivatePlugins, Is.Empty);
        }
    }
}