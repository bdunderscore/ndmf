using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using PlasticPipe.PlasticProtocol.Messages;

namespace UnitTests.PluginResolverTests
{
    class PluginA : Plugin<PluginA>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .AfterPlugin<PluginB>()
                .Run("PluginA", _ctx => { });
        }
    }

    class PluginB : Plugin<PluginB>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .Run("PluginB", _ctx => { });
        }
    }

    class PluginC : Plugin<PluginC>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                .BeforePlugin<PluginB>()
                .Run("PluginC", _ctx => { });
        }
    }
    
    public class BeforeAfterPlugin
    {
        [Test]
        public void TestBeforeAfterPluginConstraints()
        {
            var resolver = new PluginResolver(new IPluginInternal[]
            {
                PluginA.Instance, PluginB.Instance, PluginC.Instance
            });
            var passNames = resolver.Passes.SelectMany(kv => kv.Item2)
                .Select(pass => pass.Description)
                .ToImmutableList();
            
            var wantedPassNames = ImmutableList.Create("PluginC", "PluginB", "PluginA");
            
            Assert.That(passNames, Is.EqualTo(wantedPassNames));
        }
    }
}