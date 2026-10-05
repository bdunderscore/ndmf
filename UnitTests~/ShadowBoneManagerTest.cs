using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nadena.dev.ndmf.preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests
{
    public class ShadowBoneManagerTest : TestBase
    {
        private sealed class FakeShadowBoneManagerHandle : IShadowBoneManagerHandle
        {
            public Transform GetBone(Transform bone) => bone;

            public void ReplaceBone(Renderer originalRenderer, Transform original, Transform replacement)
            {
            }
        }

        private ShadowBoneManager _manager;
        private PreviewContext _context;
        private IDisposable _contextScope;

        [SetUp]
        public void SetUp()
        {
            _manager = new ShadowBoneManager();
            ActivateManagerLease();
        }

        [TearDown]
        public void TearDown()
        {
            _contextScope?.Dispose();
            _manager?.Dispose();
        }

        private void ActivateManagerLease()
        {
            _manager.ResetPipeline();
            _context = new PreviewContext { ShadowBoneManager = _manager.Handle };
            _context.Seal();
            _contextScope = _context.Activate();
        }

        private void ResetManagerLease()
        {
            _contextScope.Dispose();
            _manager.ResetPipeline();
            _context = new PreviewContext { ShadowBoneManager = _manager.Handle };
            _context.Seal();
            _contextScope = _context.Activate();
        }

        [Test]
        public void PreviewContextScopesAreAmbientMutableAndRestoredAcrossAwait()
        {
            _contextScope.Dispose();
            _contextScope = null;

            Assert.Throws<InvalidOperationException>(() => _ = PreviewContext.Instance);

            var first = new PreviewContext();
            var second = new PreviewContext();
            var firstHandle = new FakeShadowBoneManagerHandle();
            var secondHandle = new FakeShadowBoneManagerHandle();

            Assert.Throws<InvalidOperationException>(() => _ = first.ShadowBoneManager);
            first.ShadowBoneManager = firstHandle;
            first.ShadowBoneManager = secondHandle;
            Assert.That(first.ShadowBoneManager, Is.SameAs(secondHandle));

            using (first.Activate())
            {
                Assert.That(PreviewContext.Instance, Is.SameAs(first));
                Task.Run(() => Assert.That(PreviewContext.Instance, Is.SameAs(first))).GetAwaiter().GetResult();

                using (second.Activate())
                {
                    Assert.That(PreviewContext.Instance, Is.SameAs(second));
                }

                Assert.That(PreviewContext.Instance, Is.SameAs(first));
            }

            Assert.Throws<InvalidOperationException>(() => _ = PreviewContext.Instance);
        }

        [Test]
        public void SealedPreviewContextRejectsMutationAndStaleLeaseUse()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _context.ShadowBoneManager = new FakeShadowBoneManagerHandle());

            var source = CreateRoot("source");
            var staleContext = _context;
            _manager.GetBone(source.transform);

            ResetManagerLease();

            using (staleContext.Activate())
            {
                Assert.Throws<InvalidOperationException>(() => _manager.GetBone(source.transform));
            }
        }

        [UnityTest]
        public IEnumerator RejectsBoneReplacementAfterAncestorStageDisposes()
        {
            var source = CreateRoot("source");
            var sourceTransform = source.transform;
            var original = CreateChild(source, "original").AddComponent<SkinnedMeshRenderer>();
            var proxy = CreateChild(source, "proxy").AddComponent<SkinnedMeshRenderer>();
            var replacement = CreateRoot("replacement");
            var replacementTransform = replacement.transform;
            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var stageDisposed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            var taskA = Task.Factory.StartNew(async () =>
            {
                Task taskB;
                using (_manager.EnterStage(
                           1,
                           new Dictionary<Renderer, Renderer> { { original, proxy } }
                       ))
                {
                    taskB = Task.Factory.StartNew(async () =>
                    {
                        await stageDisposed.Task;
                        Assert.Throws<InvalidOperationException>(() =>
                            _manager.ReplaceBone(original, sourceTransform, replacementTransform));
                    }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();
                }

                stageDisposed.SetResult(true);
                await taskB;
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            yield return new WaitUntil(() => taskA.IsCompleted);
            taskA.GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator ParallelStagesReplaceTheirOwnProxies()
        {
            var firstSource = CreateRoot("first source");
            var firstOriginal = CreateChild(firstSource, "first original").AddComponent<SkinnedMeshRenderer>();
            var firstProxy = CreateChild(firstSource, "first proxy").AddComponent<SkinnedMeshRenderer>();
            var firstReplacement = CreateRoot("first replacement");
            firstProxy.bones = new[] { firstSource.transform };

            var secondSource = CreateRoot("second source");
            var secondOriginal = CreateChild(secondSource, "second original").AddComponent<SkinnedMeshRenderer>();
            var secondProxy = CreateChild(secondSource, "second proxy").AddComponent<SkinnedMeshRenderer>();
            var secondReplacement = CreateRoot("second replacement");
            secondProxy.bones = new[] { secondSource.transform };

            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var firstStageOpen = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var secondStageOpen = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var performReplacements = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            var firstTask = Task.Factory.StartNew(async () =>
            {
                using (var stage = _manager.EnterStage(
                           1,
                           new Dictionary<Renderer, Renderer> { { firstOriginal, firstProxy } }
                       ))
                {
                    firstStageOpen.SetResult(true);
                    await performReplacements.Task;
                    _manager.ReplaceBone(firstOriginal, firstSource.transform, firstReplacement.transform);
                    stage.Commit();
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            var secondTask = Task.Factory.StartNew(async () =>
            {
                using (var stage = _manager.EnterStage(
                           2,
                           new Dictionary<Renderer, Renderer> { { secondOriginal, secondProxy } }
                       ))
                {
                    secondStageOpen.SetResult(true);
                    await performReplacements.Task;
                    _manager.ReplaceBone(secondOriginal, secondSource.transform, secondReplacement.transform);
                    stage.Commit();
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            yield return new WaitUntil(() =>
                firstStageOpen.Task.IsCompleted && secondStageOpen.Task.IsCompleted);
            performReplacements.SetResult(true);
            yield return new WaitUntil(() => firstTask.IsCompleted && secondTask.IsCompleted);
            firstTask.GetAwaiter().GetResult();
            secondTask.GetAwaiter().GetResult();

            Assert.That(firstProxy.bones, Is.EqualTo(new[] { firstReplacement.transform }));
            Assert.That(secondProxy.bones, Is.EqualTo(new[] { secondReplacement.transform }));
        }

        [UnityTest]
        public IEnumerator ParallelStagesApplyImplicitReplacementsWhenEachRequestsTheShadow()
        {
            var source = CreateRoot("source");
            var sourceTransform = source.transform;
            var firstOriginal = CreateChild(source, "first original").AddComponent<SkinnedMeshRenderer>();
            var firstProxy = CreateChild(source, "first proxy").AddComponent<SkinnedMeshRenderer>();
            firstProxy.bones = new[] { sourceTransform };
            var secondOriginal = CreateChild(source, "second original").AddComponent<SkinnedMeshRenderer>();
            var secondProxy = CreateChild(source, "second proxy").AddComponent<SkinnedMeshRenderer>();
            secondProxy.bones = new[] { sourceTransform };

            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var firstStageOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStageOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invokeFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var invokeSecond = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completeStages = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Transform firstShadow = null;
            Transform secondShadow = null;

            var firstTask = Task.Factory.StartNew(async () =>
            {
                using (_manager.EnterStage(
                           1,
                           new Dictionary<Renderer, Renderer> { { firstOriginal, firstProxy } }
                       ))
                {
                    firstStageOpen.SetResult(true);
                    await invokeFirst.Task;
                    firstShadow = _manager.GetBone(sourceTransform);
                    firstRequested.SetResult(true);
                    await completeStages.Task;
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            var secondTask = Task.Factory.StartNew(async () =>
            {
                using (_manager.EnterStage(
                           2,
                           new Dictionary<Renderer, Renderer> { { secondOriginal, secondProxy } }
                       ))
                {
                    secondStageOpen.SetResult(true);
                    await invokeSecond.Task;
                    secondShadow = _manager.GetBone(sourceTransform);
                    secondRequested.SetResult(true);
                    await completeStages.Task;
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            yield return new WaitUntil(() =>
                firstStageOpen.Task.IsCompleted && secondStageOpen.Task.IsCompleted);

            invokeFirst.SetResult(true);
            yield return new WaitUntil(() => firstRequested.Task.IsCompleted);
            Assert.That(firstProxy.bones, Is.EqualTo(new[] { firstShadow }));
            Assert.That(secondProxy.bones, Is.EqualTo(new[] { sourceTransform }));

            invokeSecond.SetResult(true);
            yield return new WaitUntil(() => secondRequested.Task.IsCompleted);
            Assert.That(secondShadow, Is.SameAs(firstShadow));
            Assert.That(secondProxy.bones, Is.EqualTo(new[] { firstShadow }));

            completeStages.SetResult(true);
            yield return new WaitUntil(() => firstTask.IsCompleted && secondTask.IsCompleted);
            firstTask.GetAwaiter().GetResult();
            secondTask.GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator LaterStageCombinesParallelExplicitAndImplicitBoneReplacements()
        {
            var source = CreateRoot("source");
            var boneA = CreateChild(source, "bone A").transform;
            var boneB = CreateChild(source, "bone B").transform;
            var replacementA = CreateRoot("replacement A").transform;

            var firstOriginal = CreateChild(source, "first original").AddComponent<SkinnedMeshRenderer>();
            var firstProxy = CreateChild(source, "first proxy").AddComponent<SkinnedMeshRenderer>();
            firstProxy.bones = new[] { boneA, boneB };
            var secondOriginal = CreateChild(source, "second original").AddComponent<SkinnedMeshRenderer>();
            var secondProxy = CreateChild(source, "second proxy").AddComponent<SkinnedMeshRenderer>();
            secondProxy.bones = new[] { boneA, boneB };
            var laterOriginal = CreateChild(source, "later original").AddComponent<SkinnedMeshRenderer>();
            var laterProxy = CreateChild(source, "later proxy").AddComponent<SkinnedMeshRenderer>();
            laterProxy.bones = new[] { boneA, boneB };

            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            var firstStageOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStageOpen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var performReplacements = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var firstReplacementComplete = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var secondReplacementComplete = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            var completeStages = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Transform shadowB = null;

            var firstTask = Task.Factory.StartNew(async () =>
            {
                using (var stage = _manager.EnterStage(
                           1,
                           new Dictionary<Renderer, Renderer> { { firstOriginal, firstProxy } }
                       ))
                {
                    firstStageOpen.SetResult(true);
                    await performReplacements.Task;
                    _manager.ReplaceBone(firstOriginal, boneA, replacementA);
                    stage.Commit();
                    firstReplacementComplete.SetResult(true);
                    await completeStages.Task;
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            var secondTask = Task.Factory.StartNew(async () =>
            {
                using (_manager.EnterStage(
                           2,
                           new Dictionary<Renderer, Renderer> { { secondOriginal, secondProxy } }
                       ))
                {
                    secondStageOpen.SetResult(true);
                    await performReplacements.Task;
                    await firstReplacementComplete.Task;
                    shadowB = _manager.GetBone(boneB);
                    secondReplacementComplete.SetResult(true);
                    await completeStages.Task;
                }
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler).Unwrap();

            yield return new WaitUntil(() =>
                firstStageOpen.Task.IsCompleted && secondStageOpen.Task.IsCompleted);
            performReplacements.SetResult(true);
            yield return new WaitUntil(() =>
                firstReplacementComplete.Task.IsCompleted && secondReplacementComplete.Task.IsCompleted);
            Assert.That(firstProxy.bones, Is.EqualTo(new[] { replacementA, shadowB }));
            completeStages.SetResult(true);
            yield return new WaitUntil(() => firstTask.IsCompleted && secondTask.IsCompleted);
            firstTask.GetAwaiter().GetResult();
            secondTask.GetAwaiter().GetResult();
            _manager.SealPipeline();
            _manager.OnFrameStart(
                1,
                new Dictionary<Renderer, Renderer> { { firstOriginal, firstProxy } }
            );
            Assert.That(firstProxy.bones, Is.EqualTo(new[] { replacementA, shadowB }));

            using (_manager.EnterStage(
                       3,
                       new Dictionary<Renderer, Renderer> { { laterOriginal, laterProxy } }
                   ))
            {
                Assert.That(laterProxy.bones, Is.EqualTo(new[] { replacementA, shadowB }));
            }
        }

        [Test]
        public void LaterStageReappliesImplicitReplacementToAllPriorProxies()
        {
            var source = CreateRoot("source");
            var sourceTransform = source.transform;
            var firstOriginal = CreateChild(source, "first original").AddComponent<SkinnedMeshRenderer>();
            var firstProxy = CreateChild(source, "first proxy").AddComponent<SkinnedMeshRenderer>();
            firstProxy.bones = new[] { sourceTransform };
            var secondOriginal = CreateChild(source, "second original").AddComponent<SkinnedMeshRenderer>();
            var secondProxy = CreateChild(source, "second proxy").AddComponent<SkinnedMeshRenderer>();
            secondProxy.bones = new[] { sourceTransform };
            var thirdOriginal = CreateChild(source, "third original").AddComponent<SkinnedMeshRenderer>();
            var thirdProxy = CreateChild(source, "third proxy").AddComponent<SkinnedMeshRenderer>();
            thirdProxy.bones = new[] { sourceTransform };

            Transform shadow;
            using (_manager.EnterStage(
                       1,
                       new Dictionary<Renderer, Renderer> { { firstOriginal, firstProxy } }
                   ))
            {
                shadow = _manager.GetBone(sourceTransform);
            }
            using (_manager.EnterStage(
                       2,
                       new Dictionary<Renderer, Renderer> { { secondOriginal, secondProxy } }
                   ))
            {
                _manager.GetBone(sourceTransform);
            }

            firstProxy.bones = new[] { sourceTransform };
            secondProxy.bones = new[] { sourceTransform };
            using (_manager.EnterStage(
                       3,
                       new Dictionary<Renderer, Renderer> { { thirdOriginal, thirdProxy } }
                   ))
            {
                Assert.That(firstProxy.bones, Is.EqualTo(new[] { shadow }));
                Assert.That(secondProxy.bones, Is.EqualTo(new[] { shadow }));
                Assert.That(thirdProxy.bones, Is.EqualTo(new[] { shadow }));
            }
        }

        [Test]
        public void DisposedStageRestoresPreviousAmbientStage()
        {
            using (_manager.EnterStage(1, new Dictionary<Renderer, Renderer>()))
            {
                Assert.That(_manager.CurrentStage, Is.EqualTo(1));
            }

            Assert.That(_manager.CurrentStage, Is.Null);
        }

        [Test]
        public void UncommittedStageRollsBackReplacement()
        {
            var original = CreateRoot("original").AddComponent<SkinnedMeshRenderer>();
            var replacement = CreateRoot("replacement");
            original.bones = new[] { original.transform };
            original.rootBone = original.transform;

            using (_manager.EnterStage(
                       1,
                       new Dictionary<Renderer, Renderer> { { original, original } }
                   ))
            {
                _manager.ReplaceBone(original, original.transform, replacement.transform);
            }

            Assert.That(original.bones, Is.EqualTo(new[] { original.transform }));
            Assert.That(original.rootBone, Is.SameAs(original.transform));
        }

        [Test]
        public void TransfersLocalPoseWithoutRewritingUnchangedProxy()
        {
            var root = CreateRoot("root");
            var child = CreateChild(root, "child");
            child.transform.localPosition = new Vector3(1, 2, 3);
            child.transform.localRotation = Quaternion.Euler(10, 20, 30);
            child.transform.localScale = new Vector3(2, 3, 4);

            var proxy = _manager.GetBone(child.transform);
            var rootProxy = _manager.GetBone(root.transform);

            Assert.That(proxy.parent, Is.SameAs(rootProxy));
            Assert.That(proxy.localPosition, Is.EqualTo(child.transform.localPosition));
            Assert.That(Quaternion.Angle(proxy.localRotation, child.transform.localRotation), Is.LessThan(0.0001f));
            Assert.That(proxy.localScale, Is.EqualTo(child.transform.localScale));

            proxy.hasChanged = false;
            _manager.UpdateForTesting();
            Assert.That(proxy.hasChanged, Is.False);

            child.transform.localRotation = Quaternion.AngleAxis(0.01f, Vector3.right);
            _manager.UpdateForTesting();

            Assert.That(Quaternion.Angle(proxy.localRotation, child.transform.localRotation), Is.LessThan(0.0001f));
            Assert.That(proxy.hasChanged, Is.True);
        }

        [Test]
        public void UpdatesProxyHierarchyWhenSourceIsReparented()
        {
            var root = CreateRoot("root");
            var firstParent = CreateChild(root, "first parent");
            var secondParent = CreateChild(root, "second parent");
            var child = CreateChild(firstParent, "child");
            var proxy = _manager.GetBone(child.transform);

            child.transform.SetParent(secondParent.transform, false);
            child.transform.localPosition = new Vector3(4, 5, 6);
            _manager.UpdateForTesting();

            var secondParentProxy = _manager.GetBone(secondParent.transform);
            Assert.That(proxy.parent, Is.SameAs(secondParentProxy));
            Assert.That(proxy.localPosition, Is.EqualTo(child.transform.localPosition));
        }

        [Test]
        public void ResetReusesRequestedShadowsAndSealDisposesUnrequestedOnes()
        {
            var root = CreateRoot("root");
            var retained = CreateChild(root, "retained");
            var discarded = CreateChild(root, "discarded");
            var retainedProxy = _manager.GetBone(retained.transform);
            var discardedProxy = _manager.GetBone(discarded.transform);

            ResetManagerLease();
            Assert.That(_manager.GetBone(retained.transform), Is.SameAs(retainedProxy));
            _manager.SealPipeline();

            Assert.That(retainedProxy, Is.Not.Null);
            Assert.That(retainedProxy.parent, Is.SameAs(_manager.GetBone(root.transform)));
            Assert.That((UnityEngine.Object)discardedProxy == null, Is.True);
        }

        [Test]
        public void DisposeDestroysManagedShadowObjects()
        {
            var source = CreateRoot("source");
            var proxy = _manager.GetBone(source.transform);

            _manager.Dispose();

            Assert.That((UnityEngine.Object)proxy == null, Is.True);
            Assert.Throws<ObjectDisposedException>(() => _manager.GetBone(source.transform));
        }

        [Test]
        public void StagedReplacementIsExactRendererScopedAndReplayed()
        {
            var source = CreateRoot("source");
            var sourceChild = CreateChild(source, "source child");
            var replacement = CreateRoot("replacement");

            var skinnedObject = CreateChild(source, "skinned proxy");
            var skinned = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            skinned.bones = new[] { source.transform, sourceChild.transform, null };
            skinned.rootBone = source.transform;
            skinned.probeAnchor = source.transform;

            var meshObject = CreateChild(source, "mesh proxy");
            var mesh = meshObject.AddComponent<MeshRenderer>();
            mesh.probeAnchor = source.transform;

            var unlistedObject = CreateChild(source, "unlisted proxy");
            var unlisted = unlistedObject.AddComponent<SkinnedMeshRenderer>();
            unlisted.bones = new[] { source.transform, sourceChild.transform };
            unlisted.rootBone = source.transform;
            unlisted.probeAnchor = source.transform;

            using (var stage = _manager.EnterStage(
                       2,
                       new Dictionary<Renderer, Renderer> { { skinned, skinned }, { mesh, mesh } }
                   ))
            {
                _manager.ReplaceBone(skinned, source.transform, replacement.transform);
                _manager.ReplaceBone(mesh, source.transform, replacement.transform);
                stage.Commit();
            }

            AssertReplacementState(source.transform, sourceChild.transform, replacement.transform, skinned, mesh, unlisted);

            skinned.bones = new[] { source.transform, sourceChild.transform, null };
            skinned.rootBone = source.transform;
            skinned.probeAnchor = source.transform;
            skinned.transform.SetParent(source.transform, false);
            mesh.probeAnchor = source.transform;
            mesh.transform.SetParent(source.transform, false);

            var activeProxies = new Dictionary<Renderer, Renderer>
            {
                { skinned, skinned },
                { mesh, mesh }
            };

            skinned.bones = new[] { sourceChild.transform, sourceChild.transform, null };
            _manager.OnFrameStart(1, activeProxies);
            Assert.That(skinned.bones, Is.EqualTo(new[] { sourceChild.transform, sourceChild.transform, null }));
            _manager.OnFrameStart(2, activeProxies);
            Assert.That(skinned.bones, Is.EqualTo(new[] { replacement.transform, sourceChild.transform, null }));

            _manager.OnFrameStart(2, activeProxies);

            AssertReplacementState(source.transform, sourceChild.transform, replacement.transform, skinned, mesh, unlisted);
        }

        [Test]
        public void StagedReplacementReplaysOntoMappedActiveProxy()
        {
            var source = CreateRoot("source");
            var replacement = CreateRoot("replacement");
            var original = CreateChild(source, "original").AddComponent<SkinnedMeshRenderer>();
            var setupProxy = CreateChild(source, "setup proxy").AddComponent<SkinnedMeshRenderer>();
            var activeProxy = CreateChild(source, "active proxy").AddComponent<SkinnedMeshRenderer>();

            setupProxy.bones = new[] { source.transform };
            setupProxy.rootBone = source.transform;
            setupProxy.probeAnchor = source.transform;
            activeProxy.bones = new[] { source.transform };
            activeProxy.rootBone = source.transform;
            activeProxy.probeAnchor = source.transform;

            using (var stage = _manager.EnterStage(
                       2,
                       new Dictionary<Renderer, Renderer> { { original, setupProxy } }
                   ))
            {
                _manager.ReplaceBone(original, source.transform, replacement.transform);
                stage.Commit();
            }
            Assert.That(activeProxy.bones, Is.EqualTo(new[] { source.transform }));

            _manager.OnFrameStart(
                2,
                new Dictionary<Renderer, Renderer> { { original, activeProxy } }
            );

            Assert.That(activeProxy.bones, Is.EqualTo(new[] { replacement.transform }));
            Assert.That(activeProxy.rootBone, Is.SameAs(replacement.transform));
            Assert.That(activeProxy.probeAnchor, Is.SameAs(replacement.transform));
            Assert.That(activeProxy.transform.parent, Is.SameAs(replacement.transform));
        }

        [Test]
        public void RequestedShadowImplicitlyReplacesCurrentAndFutureRendererProxies()
        {
            var source = CreateRoot("source");
            var originalSkinned = CreateChild(source, "original skinned").AddComponent<SkinnedMeshRenderer>();
            var originalMesh = CreateChild(source, "original mesh").AddComponent<MeshRenderer>();
            var setupSkinned = CreateChild(source, "setup skinned").AddComponent<SkinnedMeshRenderer>();
            var setupMesh = CreateChild(source, "setup mesh").AddComponent<MeshRenderer>();
            var activeSkinned = CreateChild(source, "active skinned").AddComponent<SkinnedMeshRenderer>();
            var activeMesh = CreateChild(source, "active mesh").AddComponent<MeshRenderer>();

            foreach (var renderer in new[] { setupSkinned, activeSkinned })
            {
                renderer.bones = new[] { source.transform };
                renderer.rootBone = source.transform;
                renderer.probeAnchor = source.transform;
                renderer.transform.SetParent(source.transform, false);
            }

            foreach (var renderer in new[] { setupMesh, activeMesh })
            {
                renderer.probeAnchor = source.transform;
                renderer.transform.SetParent(source.transform, false);
            }

            using (var stage = _manager.EnterStage(
                       2,
                       new Dictionary<Renderer, Renderer>
                       {
                           { originalSkinned, setupSkinned },
                           { originalMesh, setupMesh }
                       }
                   ))
            {
                var shadow = _manager.GetBone(source.transform);
                AssertImplicitReplacement(shadow, setupSkinned, setupMesh);
                stage.Commit();
            }

            _manager.OnFrameStart(
                2,
                new Dictionary<Renderer, Renderer>
                {
                    { originalSkinned, activeSkinned },
                    { originalMesh, activeMesh }
                }
            );

            AssertImplicitReplacement(_manager.GetBone(source.transform), activeSkinned, activeMesh);
        }

        [Test]
        public void SetInitialParentParentsActiveMeshProxyToShadowWithIdentityLocalTransform()
        {
            var source = CreateRoot("source");
            var original = CreateChild(source, "original").AddComponent<MeshRenderer>();
            var proxyParent = CreateRoot("proxy parent");
            var proxy = CreateChild(proxyParent, "proxy").AddComponent<MeshRenderer>();
            proxy.transform.localPosition = new Vector3(1, 2, 3);
            proxy.transform.localRotation = Quaternion.Euler(10, 20, 30);
            proxy.transform.localScale = new Vector3(2, 3, 4);

            _manager.SetInitialParent(original, proxy);

            Assert.That(proxy.transform.parent, Is.SameAs(_manager.GetBone(original.transform)));
            Assert.That(proxy.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(proxy.transform.localRotation, Quaternion.identity), Is.LessThan(0.0001f));
            Assert.That(proxy.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void SetInitialParentUsesOriginalRendererShadowDespiteStageReplacement()
        {
            var source = CreateRoot("source");
            var original = CreateChild(source, "original").AddComponent<MeshRenderer>();
            var setupProxy = CreateChild(original.gameObject, "setup proxy").AddComponent<MeshRenderer>();
            var replacement = CreateRoot("replacement");
            var proxyParent = CreateRoot("proxy parent");
            var activeProxy = CreateChild(proxyParent, "active proxy").AddComponent<MeshRenderer>();
            activeProxy.transform.localPosition = new Vector3(1, 2, 3);
            activeProxy.transform.localRotation = Quaternion.Euler(10, 20, 30);
            activeProxy.transform.localScale = new Vector3(2, 3, 4);

            _manager.RegisterRendererStage(original, 2);
            using (var stage = _manager.EnterStage(
                       2,
                       new Dictionary<Renderer, Renderer> { { original, setupProxy } }
                   ))
            {
                _manager.ReplaceBone(original, original.transform, replacement.transform);
                stage.Commit();
            }
            _manager.RegisterRendererStage(original, 3);

            _manager.SetInitialParent(original, activeProxy);

            Assert.That(activeProxy.transform.parent, Is.SameAs(_manager.GetBone(original.transform)));
            Assert.That(activeProxy.transform.parent, Is.Not.SameAs(replacement.transform));
            Assert.That(activeProxy.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(activeProxy.transform.localRotation, Quaternion.identity), Is.LessThan(0.0001f));
            Assert.That(activeProxy.transform.localScale, Is.EqualTo(Vector3.one));
        }

        private static void AssertImplicitReplacement(
            Transform replacement,
            SkinnedMeshRenderer skinned,
            MeshRenderer mesh
        )
        {
            Assert.That(skinned.bones, Is.EqualTo(new[] { replacement }));
            Assert.That(skinned.rootBone, Is.SameAs(replacement));
            Assert.That(skinned.probeAnchor, Is.SameAs(replacement));
            Assert.That(skinned.transform.parent, Is.SameAs(replacement));
            Assert.That(mesh.probeAnchor, Is.SameAs(replacement));
            Assert.That(mesh.transform.parent, Is.SameAs(replacement));
        }

        private static void AssertReplacementState(
            Transform source,
            Transform sourceChild,
            Transform replacement,
            SkinnedMeshRenderer skinned,
            MeshRenderer mesh,
            SkinnedMeshRenderer unlisted
        )
        {
            Assert.That(skinned.bones, Is.EqualTo(new[] { replacement, sourceChild, null }));
            Assert.That(skinned.rootBone, Is.SameAs(replacement));
            Assert.That(skinned.probeAnchor, Is.SameAs(replacement));
            Assert.That(skinned.transform.parent, Is.SameAs(replacement));
            Assert.That(mesh.probeAnchor, Is.SameAs(replacement));
            Assert.That(mesh.transform.parent, Is.SameAs(replacement));

            Assert.That(unlisted.bones, Is.EqualTo(new[] { source, sourceChild }));
            Assert.That(unlisted.rootBone, Is.SameAs(source));
            Assert.That(unlisted.probeAnchor, Is.SameAs(source));
            Assert.That(unlisted.transform.parent, Is.SameAs(source));
        }
    }
}
