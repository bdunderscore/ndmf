using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
#if NDMF_VRCSDK3_AVATARS
using nadena.dev.ndmf.vrchat;
#endif
using NUnit.Framework;

namespace UnitTests.PluginResolverTests
{
    class TestPlatform : INDMFPlatformProvider
    {
        public string QualifiedName => "test-platform";
        public string DisplayName => "test-platform";
    }
    
    class DefaultPlatformPlugin : Plugin<DefaultPlatformPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.DefaultPlatform";
        public override string DisplayName => "Default Platform";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("DefaultPlatform", _ => { });
        }
    }
    
    [RunsOnPlatforms(WellKnownPlatforms.Generic)]
    class GenericPlatformPlugin : Plugin<GenericPlatformPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.GenericPlatform";
        public override string DisplayName => "Generic Platform";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("GenericPlatform", _ => { });
        }
    }
    
    [RunsOnPlatforms("test-platform")]
    class TestPlatformPlugin : Plugin<TestPlatformPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.TestPlatform";
        public override string DisplayName => "Test Platform";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("TestPlatform", _ => { });
        }
    }
    
    [RunsOnAllPlatforms]
    class AllPlatformsPlugin : Plugin<AllPlatformsPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.AllPlatforms";
        public override string DisplayName => "All Platforms";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("AllPlatforms", _ => { });
        }
    }
    
    [RunsOnPlatforms("nothing")]
    class NeverRunsPlugin : Plugin<NeverRunsPlugin>
    {
        public override string QualifiedName => "nadena.dev.ndmf.UnitTests.NeverRuns";
        public override string DisplayName => "Never Runs";
        
        protected override void Configure()
        {
            InPhase(BuildPhase.InternalPrePlatformInit)
                .Run("NeverRuns", _ => { Assert.Fail(); });
        }
    }
    
    public class PlatformFilteringTest : TestBase
    {
        [Test]
        public void TestCustomPlatform()
        {
            var passNames = GetPasses(new TestPlatform());

            var wantedPassNames = new HashSet<string>(new[] { "TestPlatform", "AllPlatforms" });
            
            Assert.That(passNames, Is.EquivalentTo(wantedPassNames));
        }

        private static HashSet<string> GetPasses(INDMFPlatformProvider platform)
        {
            var resolver = new PluginResolver(
                new[]
                {
                    typeof(DefaultPlatformPlugin), typeof(GenericPlatformPlugin), typeof(TestPlatformPlugin),
                    typeof(AllPlatformsPlugin)
                },
                platform
            );

            var passNames = resolver.Passes.SelectMany(kv => kv.Item2)
                .Where(pass => !pass.Skipped)
                .Select(pass => pass.InstantiatedPass.DisplayName)
                .ToHashSet();
            return passNames;
        }

#if NDMF_VRCSDK3_AVATARS
        [Test]
        public void TestVRCPlatform()
        {
            var passNames = GetPasses(VRChatPlatform.Instance);

            var wantedPassNames = new HashSet<string>(new[] { "DefaultPlatform", "AllPlatforms" });
            
            Assert.That(passNames, Is.EquivalentTo(wantedPassNames));
        }
        #endif
        
        [Test]
        public void TestGenericPlatform()
        {
            var passNames = GetPasses(GenericPlatform.Instance);

            var wantedPassNames = new HashSet<string>(new[] { "GenericPlatform", "AllPlatforms" });
            
            Assert.That(passNames, Is.EquivalentTo(wantedPassNames));
        }

        [Test]
        public void SuppressesExecution()
        {
            var resolver = new PluginResolver(
                new[]
                {
                    typeof(NeverRunsPlugin)
                },
                GenericPlatform.Instance
            );

            var pass = resolver.Passes
                .SelectMany(kv => kv.Item2)
                .First(pass => pass.Skipped && pass.Plugin.QualifiedName == NeverRunsPlugin.Instance.QualifiedName);
            
            pass.Execute(CreateContext(CreateRoot("root")));
        }
    }
}