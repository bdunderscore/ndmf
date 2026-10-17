using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine;

namespace UnitTests
{
    class TestRenderFilterNode : IRenderFilterNode
    {
        public RenderAspects WhatChanged { get; set; }

        public Func<IEnumerable<(Renderer, Renderer)>, ComputeContext, RenderAspects, Task<IRenderFilterNode>> RefreshFunc { get; set; }
        public Action OnFrameGroupFunc { get; set; }
        public Action<Renderer, Renderer> OnFrameFunc { get; set; }
        
        public Task<IRenderFilterNode> Refresh(
            IEnumerable<(Renderer, Renderer)> proxyPairs,
            ComputeContext context,
            RenderAspects updatedAspects
        )
        {
            return RefreshFunc(proxyPairs, context, updatedAspects);
        }

        public void OnFrameGroup()
        {
            OnFrameGroupFunc?.Invoke();
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            OnFrameFunc?.Invoke(original, proxy);
        }
    }

    class TestRenderFilter : IRenderFilter
    {
        public Func<ComputeContext, ImmutableList<RenderGroup>> GetTargetGroupsFunc { get; set; }

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext context)
        {
            return GetTargetGroupsFunc(context);
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
        private ShadowBoneManager _manager;
        private PreviewContext _context;
        private IDisposable _contextScope;
        
        [SetUp]
        public void SetUp()
        {
            _cache = new ProxyObjectCache();
            _manager = new ShadowBoneManager();
            _manager.ResetPipeline();
            _context = new PreviewContext { ShadowBoneManager = _manager.Handle };
            _context.Seal();
            _contextScope = _context.Activate();
        }
        
        [TearDown]
        public void TearDown()
        {
            _contextScope.Dispose();
            _manager.Dispose();
            _cache.Dispose();
        }
        
        [TestCase("Instantiate")]
        [TestCase("null")]
        [TestCase("Refresh")]
        public void NodeFailureDoesNotAbortEvaluation(string operation)
        {
            var original = CreateRoot("original").AddComponent<SkinnedMeshRenderer>();
            using var proxy = new ProxyObjectController(_cache, original, null);
            var proxies = new List<(Renderer, ProxyObjectController, ObjectRegistry)>
            {
                (original, proxy, new ObjectRegistry(null))
            };
            var node = new TestRenderFilterNode
            {
                RefreshFunc = (_, _, _) => Task.FromException<IRenderFilterNode>(new Exception("test failure"))
            };
            var filter = new TestRenderFilter
            {
                InstantiateFunc = (_, _, _) => operation == "Instantiate"
                    ? throw new Exception("test failure")
                    : Task.FromResult<IRenderFilterNode>(operation == "null" ? null : node)
            };
            LogAssert.Expect(LogType.Exception, new Regex(operation == "null"
                ? "^InvalidOperationException:"
                : "^Exception: test failure$"));
            using var created = NodeController.Create(filter, RenderGroup.For(original), proxies, "", _context, 0).Result;
            Assert.That(created, Is.Not.Null);

            if (operation == "Refresh")
            {
                using var refreshed = created.Refresh(proxies, RenderAspects.Mesh, "", _context, 0).Result;
                Assert.That(refreshed, Is.Not.Null);
                Assert.That(refreshed.WhatChanged, Is.EqualTo(RenderAspects.Everything));
            }
        }

        [Test]
        public void TargetGroupFailureRetriesWhenDependencyChanges()
        {
            var retry = new PublishedValue<bool>(false);
            var calls = 0;
            var filter = new TestRenderFilter
            {
                GetTargetGroupsFunc = context =>
                {
                    calls++;
                    if (!context.Observe(retry))
                    {
                        throw new Exception("test failure");
                    }

                    return ImmutableList<RenderGroup>.Empty;
                }
            };
            var filters = ImmutableList.Create<IRenderFilter>(filter);
            LogAssert.Expect(LogType.Exception, "Exception: test failure");
            var targets = new TargetSet(filters, ImmutableHashSet<Renderer>.Empty, null);

            targets = targets.Refresh(filters, ImmutableHashSet<Renderer>.Empty, null);
            Assert.That(targets.ResolveActiveStages(new ComputeContext("failed targets")), Is.Empty);
            Assert.That(calls, Is.EqualTo(1));

            retry.Value = true;
            ComputeContext.FlushInvalidates();
            Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SessionRetriesFailedNodeOnlyWhenDependencyChanges()
        {
            var original = CreateRoot("original").AddComponent<SkinnedMeshRenderer>();
            var group = RenderGroup.For(original);
            var retry = new PublishedValue<bool>(false);
            var attempts = 0;
            var node = new TestRenderFilterNode();
            var filter = new TestRenderFilter
            {
                GetTargetGroupsFunc = _ => ImmutableList.Create(group),
                InstantiateFunc = (_, _, context) =>
                {
                    attempts++;
                    if (!context.Observe(retry))
                    {
                        return Task.FromResult<IRenderFilterNode>(null);
                    }

                    return Task.FromResult<IRenderFilterNode>(node);
                }
            };
            using var session = new ProxySession(ImmutableList.Create<IRenderFilter>(filter));
            LogAssert.Expect(LogType.Exception, new Regex("^InvalidOperationException:"));

            for (var frame = 0; frame < 120 && session.OriginalToProxyRenderer.Count == 0; frame++)
            {
                session.OnPreCull(true);
                yield return null;
            }

            Assert.That(session.OriginalToProxyRenderer.ContainsKey(original), Is.True);
            for (var frame = 0; frame < 10; frame++)
            {
                session.OnPreCull(true);
                yield return null;
            }
            Assert.That(attempts, Is.EqualTo(1));

            retry.Value = true;
            for (var frame = 0; frame < 120 && attempts < 2; frame++)
            {
                session.OnPreCull(true);
                yield return null;
            }

            Assert.That(attempts, Is.EqualTo(2));
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
            
            var group = new RenderGroup(ImmutableList.Create<Renderer>(r1, r2), ImmutableDictionary<Renderer, string>.Empty);

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
                },
                "",
                _context,
                0
            ).Result;

            IObjectRegistry reg2 = nodeController.ObjectRegistry;
            Assert.AreEqual(reg2.GetReference(root), reg2.GetReference(tmp));
            
            Assert.AreNotEqual(((IObjectRegistry)or1).GetReference(root), ((IObjectRegistry)or2).GetReference(tmp));
            nodeController.Dispose();
            
            poc1.Dispose();
            poc2.Dispose();
        }

        [Test]
        public void ReturnSetupProxy_ReparentsProxyToSceneRoot()
        {
            var original = CreateRoot("Original").AddComponent<MeshRenderer>();
            var transientParent = CreateRoot("Transient Parent");
            var handle = _cache.GetHandle(
                original,
                () => new GameObject("Proxy").AddComponent<MeshRenderer>()
            );

            try
            {
                var setupProxy = handle.GetSetupProxy();
                var proxyScene = setupProxy.gameObject.scene;
                setupProxy.transform.SetParent(transientParent.transform, false);

                handle.ReturnSetupProxy(setupProxy);

                Assert.That(setupProxy.transform.parent, Is.Null);
                Assert.That(setupProxy.gameObject.scene, Is.EqualTo(proxyScene));

                var reusedProxy = handle.GetSetupProxy();
                Assert.That(reusedProxy, Is.SameAs(setupProxy));
                Assert.That(reusedProxy.transform.parent, Is.Null);
                handle.ReturnSetupProxy(reusedProxy);
            }
            finally
            {
                handle.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PipelineParentsPrimaryMeshProxyToOriginalTransformShadow()
        {
            var originalObject = CreateRoot("Original");
            originalObject.AddComponent<MeshFilter>();
            var original = originalObject.AddComponent<MeshRenderer>();
            var group = new RenderGroup(
                ImmutableList.Create<Renderer>(original),
                ImmutableDictionary<Renderer, string>.Empty
            );
            var node = new TestRenderFilterNode();
            var filter = new TestRenderFilter
            {
                GetTargetGroupsFunc = _ => ImmutableList.Create(group),
                InstantiateFunc = (_, _, _) => Task.FromResult<IRenderFilterNode>(node)
            };
            var pipeline = new ProxyPipeline(_cache, new[] { filter }, null, null);

            try
            {
                while (!pipeline.IsReady && !pipeline.IsFailed) yield return null;
                Assert.That(pipeline.IsFailed, Is.False);

                Renderer proxy = null;
                foreach (var (_, candidate) in pipeline.Renderers)
                {
                    proxy = candidate;
                }

                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.transform.parent, Is.Not.Null);
                Assert.That(proxy.transform.parent, Is.Not.SameAs(original.transform));
                Assert.That(proxy.transform.parent.name, Is.EqualTo(original.transform.name));
                Assert.That(proxy.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(proxy.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(proxy.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                pipeline.Dispose();
            }
        }

        [Test]
        public void InstantiateAndRefreshMayReplaceBonesWithinTheirStageScopes()
        {
            var source = CreateRoot("source");
            var replacement = CreateRoot("replacement");
            var original = CreateChild(source, "original").AddComponent<SkinnedMeshRenderer>();
            var controller = new ProxyObjectController(_cache, original, null);
            var proxy = (SkinnedMeshRenderer)controller.Renderer;
            proxy.bones = new[] { source.transform };
            var group = new RenderGroup(ImmutableList.Create<Renderer>(original), ImmutableDictionary<Renderer, string>.Empty);
            var registry = new ObjectRegistry(null);
            var node = new TestRenderFilterNode();
            var filter = new TestRenderFilter();
            var instantiateCalled = false;
            var refreshCalled = false;
            var onFrameGroupRejected = false;
            var onFrameRejected = false;

            node.OnFrameGroupFunc = () =>
            {
                Assert.Throws<InvalidOperationException>(() => PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                    original, source.transform, replacement.transform));
                onFrameGroupRejected = true;
            };
            node.OnFrameFunc = (_, _) =>
            {
                Assert.Throws<InvalidOperationException>(() => PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                    original, source.transform, replacement.transform));
                onFrameRejected = true;
            };

            filter.InstantiateFunc = (_, _, _) =>
            {
                PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                    original, source.transform, replacement.transform);
                instantiateCalled = true;
                return Task.FromResult<IRenderFilterNode>(node);
            };
            node.RefreshFunc = (_, _, _) =>
            {
                PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                    original, source.transform, replacement.transform);
                refreshCalled = true;
                return Task.FromResult<IRenderFilterNode>(node);
            };

            try
            {
                var nodeController = NodeController.Create(
                    filter,
                    group,
                    new() { (original, controller, registry) },
                    "",
                    _context,
                    1
                ).Result;

                var refreshed = nodeController.Refresh(
                    new() { (original, controller, registry) },
                    (RenderAspects)0,
                    "",
                    _context,
                    1
                ).Result;

                Assert.That(instantiateCalled, Is.True);
                Assert.That(refreshCalled, Is.True);
                Assert.That(onFrameGroupRejected, Is.True);
                Assert.That(onFrameRejected, Is.True);
                Assert.That(proxy.bones[0], Is.SameAs(replacement.transform));

                refreshed.Dispose();
                nodeController.Dispose();
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public void NullRefreshRollsBackShadowBoneChanges()
        {
            var source = CreateRoot("source");
            var firstReplacement = CreateRoot("first replacement");
            var secondReplacement = CreateRoot("second replacement");
            var original = CreateChild(source, "original").AddComponent<SkinnedMeshRenderer>();
            var controller = new ProxyObjectController(_cache, original, null);
            var proxy = (SkinnedMeshRenderer)controller.Renderer;
            proxy.bones = new[] { source.transform };
            var group = new RenderGroup(ImmutableList.Create<Renderer>(original), ImmutableDictionary<Renderer, string>.Empty);
            var registry = new ObjectRegistry(null);
            var node = new TestRenderFilterNode();
            var filter = new TestRenderFilter
            {
                InstantiateFunc = (_, _, _) =>
                {
                    PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                        original, source.transform, firstReplacement.transform);
                    return Task.FromResult<IRenderFilterNode>(node);
                }
            };
            node.RefreshFunc = (_, _, _) =>
            {
                PreviewContext.Instance.ShadowBoneManager.ReplaceBone(
                    original, source.transform, secondReplacement.transform);
                return Task.FromResult<IRenderFilterNode>(null);
            };

            try
            {
                var nodeController = NodeController.Create(
                    filter,
                    group,
                    new() { (original, controller, registry) },
                    "",
                    _context,
                    1
                ).Result;

                var refreshed = nodeController.Refresh(
                    new() { (original, controller, registry) },
                    (RenderAspects)0,
                    "",
                    _context,
                    1
                ).Result;

                Assert.That(refreshed, Is.Null);
                Assert.That(proxy.bones, Is.EqualTo(new[] { firstReplacement.transform }));
                nodeController.Dispose();
            }
            finally
            {
                controller.Dispose();
            }
        }

        [Test]
        public void ZeroChangeRefreshWithoutShadowBoneManagerAccessReusesNode()
        {
            var original = CreateRoot("original").AddComponent<SkinnedMeshRenderer>();
            var proxy = CreateRoot("proxy").AddComponent<SkinnedMeshRenderer>();
            var controller = new ProxyObjectController(_cache, original, null);
            var group = new RenderGroup(ImmutableList.Create<Renderer>(original), ImmutableDictionary<Renderer, string>.Empty);
            var registry = new ObjectRegistry(null);
            var node = new TestRenderFilterNode();
            var filter = new TestRenderFilter();
            var refreshCalled = false;

            filter.InstantiateFunc = (_, _, _) => Task.FromResult<IRenderFilterNode>(node);
            node.RefreshFunc = (_, _, _) =>
            {
                refreshCalled = true;
                return Task.FromResult<IRenderFilterNode>(node);
            };

            try
            {
                var nodeController = NodeController.Create(
                    filter,
                    group,
                    new() { (original, controller, registry) },
                    "",
                    _context,
                    0
                ).Result;
                var refreshed = nodeController.Refresh(
                    new() { (original, controller, registry) },
                    (RenderAspects)0,
                    "",
                    _context,
                    0
                ).Result;

                Assert.That(refreshCalled, Is.False);
                refreshed.Dispose();
                nodeController.Dispose();
            }
            finally
            {
                controller.Dispose();
            }
        }

    }
}