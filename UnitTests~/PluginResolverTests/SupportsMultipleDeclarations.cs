using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnitTests.ExportsPluginTest;

[assembly: ExportsPlugin(typeof(PluginA))]
[assembly: ExportsPlugin(typeof(PluginB))]

namespace UnitTests.ExportsPluginTest
{
    internal class PluginA : Plugin<PluginA>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("test", _ctx => { });
        }
    }
    
    internal class PluginB : Plugin<PluginB>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("test", _ctx => { });
        }
    }
    
    public class SupportsMultipleDeclarations
    {
        [Test]
        public void TestSupportsMultipleDeclarations()
        {
            var resolver = new PluginResolver();
            var plugins =
                resolver.Passes.SelectMany(kv => kv.Item2) // passes per phase
                    .Select(pass => pass.Plugin.GetType())
                    .ToImmutableHashSet();
            
            Assert.IsTrue(plugins.Contains(typeof(PluginA)));
            Assert.IsTrue(plugins.Contains(typeof(PluginB)));
        }
    }
}