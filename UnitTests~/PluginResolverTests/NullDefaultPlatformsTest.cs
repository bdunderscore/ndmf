using System.Collections.Immutable;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
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
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("NullDefaultPlatforms", _ => { });
        }
    }
    
    public class NullDefaultPlatformsTest
    {
        [Test]
        public void TestNullDefaultPlatformsAllowed()
        {
            // Test that plugins can have null default platforms and it works correctly
            Assert.DoesNotThrow(() => {
                // This should work - plugin resolver should handle plugins with null default platforms
                var resolver = new PluginResolver(new[] { typeof(NullDefaultPlatformsPlugin) }, GenericPlatform.Instance);
                
                // Just creating the resolver should work without error
                Assert.IsNotNull(resolver);
            });
        }
    }
}