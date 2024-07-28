using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using nadena.dev.ndmf.rq;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests
{
    class TestRenderFilterNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged { get; set; }

        public Func<IEnumerable<(Renderer, Renderer)>, ComputeContext, RenderAspects, Task<IRenderFilterNode>> RefreshFunc { get; set; }
        
        public Task<IRenderFilterNode> Refresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            return RefreshFunc(proxyPairs, context, updatedAspects);
        }
    }

    class TestRenderFilter : IRenderFilter
    {
        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            throw new System.NotImplementedException();
        }
        
        public Func<RenderGroup, IEnumerable<(Renderer, Renderer)>, ComputeContext, Task<IRenderFilterNode>> InstantiateFunc { get; set; }

        public Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            return InstantiateFunc(group, proxyPairs, context);
        }
    }
    
    public class NodeControllerTest : TestBase
    {
        private ProxyObjectCache _cache;
        
        [SetUp]
        public void SetUp()
        {
            _cache = new ProxyObjectCache();
        }
        
        [TearDown]
        public void TearDown()
        {
            _cache.Dispose();
        }
        
        [Test]
        public void TestObjectRegistryProcessing()
        {
            var filter = new TestRenderFilter();
            var node = new TestRenderFilterNode();

            var root = CreateRoot("r");
            var c1 = CreateChild(root, "c1");
            var c2 = CreateChild(root, "c2");
            var tmp = CreateChild(root, "tmp");
            
            var r1 = c1.AddComponent<SkinnedMeshRenderer>();
            var r2 = c2.AddComponent<SkinnedMeshRenderer>();

            var poc1 = new ProxyObjectController(_cache, r1, null);
            var poc2 = new ProxyObjectController(_cache, r2, null);
            
            var group = new RenderGroup(ImmutableList.Create<Renderer>(r1, r2));

            var or1 = new ObjectRegistry(null);
            var or2 = new ObjectRegistry(null);

            ((IObjectRegistry)or1).RegisterReplacedObject(c1, r1);
            ((IObjectRegistry)or2).RegisterReplacedObject(c2, r2);

            filter.InstantiateFunc = (_, _, _) =>
            {
                Assert.AreEqual(ObjectRegistry.GetReference(c1), ObjectRegistry.GetReference(r1));
                Assert.AreEqual(ObjectRegistry.GetReference(c2), ObjectRegistry.GetReference(r2));

                ObjectRegistry.RegisterReplacedObject(root, tmp);
                
                return Task.FromResult<IRenderFilterNode>(node);
            };
            
            var nodeController = NodeController.Create(
                filter,
                group,
                new()
                {
                    (r1, poc1, or1),
                    (r2, poc2, or2)
                }
            ).Result;

            IObjectRegistry reg2 = nodeController.ObjectRegistry;
            Assert.AreEqual(reg2.GetReference(root), reg2.GetReference(tmp));
            
            Assert.AreNotEqual(((IObjectRegistry)or1).GetReference(root), ((IObjectRegistry)or2).GetReference(tmp));
            
            poc1.Dispose();
            poc2.Dispose();
        }
    }
}