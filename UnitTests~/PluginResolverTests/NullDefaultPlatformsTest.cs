using System.Collections.Immutable;
using nadena.dev.ndmf;
using NUnit.Framework;

namespace UnitTests.PluginResolverTests
{
    class NullDefaultPlatformsPlugin : Plugin<NullDefaultPlatformsPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.NullDefaultPlatforms";
        public override string DisplayName => "Null Default Platforms";
        
        protected override void Configure()
        {
            // Create a sequence without setting default platforms (should remain null)
            var sequence = InPhase(BuildPhase.InternalPrePlatformInit);
            sequence.Run("NullDefaultPlatforms", _ => { });
            
            // This should work even with null defaultPlatforms
            Assert.DoesNotThrow(() => {
                var newSequence = sequence.Then.Run("SecondPass", _ => { });
            });
        }
    }
    
    public class NullDefaultPlatformsTest
    {
        [Test]
        public void TestNullDefaultPlatformsAllowed()
        {
            // Test that plugins can have null default platforms and it works correctly
            Assert.DoesNotThrow(() => {
                var resolver = new PluginResolver(new[] { typeof(NullDefaultPlatformsPlugin) });
                var passes = resolver.PluginPasses.ToArray();
                
                // Should have created the pass without error
                Assert.IsTrue(passes.Length > 0);
            });
        }
    }
}