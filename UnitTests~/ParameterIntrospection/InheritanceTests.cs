using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using nadena.dev.ndmf;
using nadena.dev.ndmf.UnitTestSupport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if NDMF_VRCSDK3_AVATARS

namespace UnitTests.Parameters
{
    [ParameterProviderFor(typeof(ITestInterface1))]
    internal class TestInterface1Provider : IParameterProvider
    {
        public TestInterface1Provider(ITestInterface1 _)
        {
            
        }
        
        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            return Array.Empty<ProvidedParameter>();
        }

        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context)
        {
        }
    }
    
    [ParameterProviderFor(typeof(ITestInterface2))]
    internal class TestInterface2Provider : IParameterProvider
    {
        public TestInterface2Provider(ITestInterface2 _)
        {
            
        }
        
        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            return Array.Empty<ProvidedParameter>();
        }
        
        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context)
        {
        }
    }
    
    [ParameterProviderFor(typeof(PTCDepthResolutionComponentBase2))]
    internal class DepthResolutionProvider : IParameterProvider
    {
        public DepthResolutionProvider(PTCDepthResolutionComponentBase2 _)
        {
            
        }
        
        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context)
        {
            return Array.Empty<ProvidedParameter>();
        }
        
        public void RemapParameters(ref ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context)
        {
        }
    }
    
    public class InheritanceTest : TestBase
    {
        [Test]
        public void ResolvesInterface()
        {
            var root = CreateRoot("root");
            var obj = root.AddComponent<PTCInheritanceComponent>();
            
            Assert.IsTrue(EnhancerDatabase<ParameterProviderFor, IParameterProvider>.Query(
                obj, out var provider
            ));
            
            Assert.IsTrue(provider is TestInterface1Provider);
        }
        
        [Test]
        public void DoesNotResolveAmbiguous()
        {
            var root = CreateRoot("root");
            var obj = root.AddComponent<PTCConflictComponent>();
            
            Assert.IsFalse(EnhancerDatabase<ParameterProviderFor, IParameterProvider>.Query(
                obj, out var provider
            ));
            LogAssert.Expect(LogType.Error, new Regex("Multiple candidate .*ParameterProviderFor attributes"));
        }
        
        [Test]
        public void ResolvesByDepth()
        {
            var root = CreateRoot("root");
            var obj = root.AddComponent<PTCDepthResolutionComponent>();
            
            Assert.IsTrue(EnhancerDatabase<ParameterProviderFor, IParameterProvider>.Query(
                obj, out var provider
            ));
            
            Assert.IsTrue(provider is TestInterface2Provider);
        }
    }
}

#endif