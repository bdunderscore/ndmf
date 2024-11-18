using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;

namespace UnitTests.PluginResolverTests
{
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

    [DependsOnContext(typeof(Ctx1))]
    public class Pass1 : Pass<Pass1>
    {
        protected override void Execute(BuildContext context)
        {
            
        }
    }
    
    public class Plugin1 : Plugin<Plugin1>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run(Pass1.Instance)
                .Then.WithCompatibleExtension(typeof(Ctx1), seq =>
                {
                    seq.Run("test test", _ => { });
                });
        }
    }
    
    public class ExtensionDependenciesTest
    {
        [Test]
        public void AssertCorrectPassDependencies()
        {
            var resolver = new PluginResolver(new[] { typeof(Plugin1) });

            var phase = resolver.Passes.First(p => p.Item1 == BuildPhase.Generating).Item2;
            var pass1 = phase.First(pass => pass.InstantiatedPass is Pass1);
            
            Assert.That(pass1.ActivatePlugins, Is.EquivalentTo(new[] { typeof(Ctx2), typeof(Ctx3), typeof(Ctx1) }));
            Assert.That(pass1.DeactivatePlugins, Is.Empty);
            
            var pass2 = phase.First(pass => pass.InstantiatedPass.DisplayName == "test test");
            Assert.That(pass2.ActivatePlugins.IsEmpty);
            Assert.That(pass2.DeactivatePlugins.IsEmpty);
        }
    }
}