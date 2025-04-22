using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

namespace UnitTests.AnimationServices
{
    public class VirtualClipTest : TestBase
    {
        private GameObject avatarRoot;
        private CloneContext context;
        
        [SetUp]
        public void Setup()
        {
            avatarRoot = CreateRoot("root");
            context = CreateCloneContext();
        }

        private CloneContext CreateCloneContext()
        {
            return new CloneContext(GenericPlatformAnimatorBindings.Instance);
        }

        AnimationClip Commit(VirtualClip clip)
        {
            return TrackObject((AnimationClip) new CommitContext().CommitObject(clip));
        }

        [Test]
        public void PreservesInitialCurves()
        {
            var material = NewTestMaterial();
            AnimationClip ac = TrackObject(new AnimationClip());
            ac.name = "foo";
            var originalCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
            ac.SetCurve("abc", typeof(GameObject), "m_IsActive", originalCurve);
            
            AnimationUtility.SetObjectReferenceCurve(ac, EditorCurveBinding.PPtrCurve("def", typeof(MeshRenderer), "m_Materials"), new ObjectReferenceKeyframe[]
            {
                new() {time = 0, value = material},
            });
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            var committedClip = Commit(vc);
            
            var bindings = AnimationUtility.GetCurveBindings(committedClip).ToList();
            Assert.AreEqual(1, bindings.Count);
            Assert.AreEqual("abc", bindings[0].path);
            Assert.AreEqual(originalCurve, AnimationUtility.GetEditorCurve(committedClip, bindings[0]));
            
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(committedClip);
            Assert.AreEqual(1, objBindings.Length);
            Assert.AreEqual("def", objBindings[0].path);
            Assert.AreEqual(material, AnimationUtility.GetObjectReferenceCurve(committedClip, objBindings[0])[0].value);
        }
        
        [Test]
        public void EditExistingFloatCurve()
        {
            AnimationClip ac = TrackObject(new AnimationClip());
            ac.name = "foo";
            var originalCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));
            ac.SetCurve("abc", typeof(GameObject), "m_IsActive", originalCurve);
            
            VirtualClip vc = VirtualClip.Clone(context, ac);

            var bindings = vc.GetFloatCurveBindings().ToList();
            Assert.AreEqual(1, bindings.Count);
            Assert.AreEqual("abc", bindings[0].path);
            Assert.AreEqual(typeof(GameObject), bindings[0].type);
            Assert.AreEqual("m_IsActive", bindings[0].propertyName);
            
            var existingCurve = vc.GetFloatCurve("abc", typeof(GameObject), "m_IsActive");
            Assert.IsNotNull(existingCurve);
            AssertEqualNotSame(existingCurve, originalCurve);
            
            // Replace the curve and see if it commits
            var newCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 2));
            using (new AssertInvalidate(vc))
            {
                vc.SetFloatCurve("abc", typeof(GameObject), "m_IsActive", newCurve);
            }

            var committedClip = Commit(vc);
            var newCommittedCurve = AnimationUtility.GetEditorCurve(committedClip, bindings[0]);
            AssertEqualNotSame(newCommittedCurve, newCurve);
            Assert.AreEqual(ac.name, committedClip.name);
        }

        [Test]
        public void CreateDeleteFloatCurve()
        {
            AnimationClip ac = TrackObject(new AnimationClip());
            ac.SetCurve("abc", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            using (new AssertInvalidate(vc))
            {
                vc.SetFloatCurve("abc", typeof(GameObject), "m_IsActive", null);
            }

            using (new AssertInvalidate(vc))
            {
                vc.SetFloatCurve("def", typeof(GameObject), "m_IsActive",
                    new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            }

            Assert.AreEqual(vc.GetFloatCurveBindings().Count(), 1);
            
            var committedClip = Commit(vc);
            var bindings = AnimationUtility.GetCurveBindings(committedClip).ToList();
            Assert.AreEqual(1, bindings.Count);
            Assert.AreEqual("def", bindings[0].path);
        }

        [Test]
        public void EditExistingObjectCurve()
        {
            var m1 = TrackObject(NewTestMaterial());
            var m2 = TrackObject(NewTestMaterial());
            
            AnimationClip ac = TrackObject(new AnimationClip());
            AnimationUtility.SetObjectReferenceCurve(ac, new EditorCurveBinding()
            {
                path = "abc",
                type = typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            }, new ObjectReferenceKeyframe[]
            {
                new() {time = 0, value = m1},
            });
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            using (new AssertInvalidate(vc))
            {
                vc.SetObjectCurve("abc", typeof(MeshRenderer), "m_Materials.Array.data[0]",
                    new ObjectReferenceKeyframe[]
                    {
                        new() { time = 0, value = m2 },
                    });
            }

            var committedClip = Commit(vc);
            var newCommittedCurve = AnimationUtility.GetObjectReferenceCurve(committedClip, new EditorCurveBinding()
            {
                path = "abc",
                type = typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            });
            Assert.IsNotNull(newCommittedCurve);
            Assert.AreEqual(1, newCommittedCurve.Length);
            Assert.AreEqual(0, newCommittedCurve[0].time);
            Assert.AreEqual(m2, newCommittedCurve[0].value);
            
            // check that the original clip is not modified
            var originalCurve = AnimationUtility.GetObjectReferenceCurve(ac, new EditorCurveBinding()
            {
                path = "abc",
                type = typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            });
            Assert.IsNotNull(originalCurve);
            Assert.AreEqual(m1, originalCurve[0].value);
        }

        private Material NewTestMaterial()
        {
            Shader s = Shader.Find("Unlit/Color");
            return new Material(s);
        }

        [Test]
        public void CreateDeleteObjectCurve()
        {
            var m1 = TrackObject(NewTestMaterial());

            var ac = TrackObject(new AnimationClip());
            AnimationUtility.SetObjectReferenceCurve(ac, new EditorCurveBinding()
            {
                path = "abc",
                type = typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            }, new ObjectReferenceKeyframe[]
            {
                new() {time = 0, value = m1},
            });
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            vc.SetObjectCurve("abc", typeof(MeshRenderer), "m_Materials.Array.data[0]", null);
            using (new AssertInvalidate(vc))
            {
                vc.SetObjectCurve("def", typeof(MeshRenderer), "m_Materials.Array.data[0]",
                    new ObjectReferenceKeyframe[]
                    {
                        new() { time = 0, value = m1 },
                    });
            }

            Assert.AreEqual(1, vc.GetObjectCurveBindings().Count());
            
            var committedClip = Commit(vc);
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(committedClip);
            Assert.AreEqual(1, bindings.Length);
            Assert.AreEqual("def", bindings[0].path);
        }

        [Test]
        public void TestEditPath()
        {
            AnimationClip ac = TrackObject(new AnimationClip());
            ac.SetCurve("abc", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            ac.SetCurve("DEF", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            ac.SetCurve("x", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            ac.SetCurve("X", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            AnimationUtility.SetObjectReferenceCurve(ac, new EditorCurveBinding()
            {
                path = "foo",
                type = typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            }, new ObjectReferenceKeyframe[]
            {
                new() {time = 0, value = new Material(Shader.Find("Standard"))},
            });
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            using (new AssertInvalidate(vc))
            {
                vc.EditPaths(s => s.ToUpperInvariant());
            }

            Assert.AreEqual(new[] { "ABC", "DEF", "X" }, vc.GetFloatCurveBindings().Select(b => b.path).OrderBy(b => b).ToArray());
            Assert.AreEqual(new[] { "FOO" }, vc.GetObjectCurveBindings().Select(b => b.path).OrderBy(b => b).ToArray());
            
            var committedClip = Commit(vc);
            var bindings = AnimationUtility.GetCurveBindings(committedClip).Select(b => b.path).OrderBy(b => b).ToList();
            Assert.AreEqual(new[] { "ABC", "DEF", "X" }, bindings);
            
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(committedClip).Select(b => b.path).OrderBy(b => b).ToList();
            Assert.AreEqual( new[] { "FOO" }, objBindings);
        }

        [Test]
        public void PreservesHighQualityMode([Values("HQ_ON.anim", "HQ_OFF.anim")] string testAsset)
        {
            AnimationClip ac = LoadAsset<AnimationClip>(testAsset);
            bool hq = new SerializedObject(ac).FindProperty("m_UseHighQualityCurve").boolValue;
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            
            vc.SetFloatCurve(EditorCurveBinding.FloatCurve("abc", typeof(GameObject), "m_IsActive"), 
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            var committedClip = Commit(vc);
            
            Assert.AreEqual(hq, new SerializedObject(committedClip).FindProperty("m_UseHighQualityCurve").boolValue);
        }
#if NDMF_VRCSDK3_AVATARS
        [Test]
        public void PreservesDiscreteCurves([Values("DiscreteCurves_WithEvent.anim", "DiscreteCurves.anim")] string testAsset)
        {
            AnimationClip ac = LoadAsset<AnimationClip>(testAsset);

            var bindings = AnimationUtility.GetCurveBindings(ac);
            
            VirtualClip vc = VirtualClip.Clone(context, ac);
            
            var vcBindings = vc.GetFloatCurveBindings().ToList();
            
            var committedClip = Commit(vc);
            
            var committedBindings = AnimationUtility.GetCurveBindings(committedClip);
            
            Assert.That(bindings, Is.EquivalentTo(vcBindings));
            Assert.That(bindings, Is.EquivalentTo(committedBindings));
            
            Assert.That(AnimationUtility.GetEditorCurve(ac, bindings[0]),
                Is.EqualTo(AnimationUtility.GetEditorCurve(committedClip, bindings[0])));
        }

        [Test]
        public void CanSetDiscreteCurves()
        {
            var vc = VirtualClip.Create("test");
            var ecb = EditorCurveBinding.DiscreteCurve("test", typeof(VRCPhysBone), "allowGrabbing");
            vc.SetFloatCurve(ecb,
                AnimationCurve.Constant(0, 1, 42));
            
            var committedClip = Commit(vc);
            
            var bindings = AnimationUtility.GetCurveBindings(committedClip);
            Assert.AreEqual(1, bindings.Length);
            Assert.AreEqual(ecb, bindings[0]);
            
            var curve = AnimationUtility.GetEditorCurve(committedClip, bindings[0]);
            Assert.IsNotNull(curve);
            Assert.AreEqual(2, curve.keys.Length);
            Assert.AreEqual(0, curve.keys[0].time);
            Assert.AreEqual(42, curve.keys[0].value);
        }
#endif

        [Test]
        public void MultiplePathRewrites()
        {
            var clip = VirtualClip.Create("test");
            clip.SetFloatCurve("abc", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            clip.EditPaths(p => p + "1");
            clip.EditPaths(p => p + "2");
            clip.EditPaths(p => p + "3");
            
            var bindings = clip.GetFloatCurveBindings().ToList();
            Assert.AreEqual(1, bindings.Count);
            Assert.AreEqual("abc123", bindings[0].path);
            Assert.AreEqual(typeof(GameObject), bindings[0].type);
            Assert.AreEqual("m_IsActive", bindings[0].propertyName);
            Assert.AreEqual(2, clip.GetFloatCurve("abc123", typeof(GameObject), "m_IsActive")!.keys.Length);
        }
        
        [Test]
        public void MultiplePathRewrites_WithOverride()
        {
            var clip = VirtualClip.Create("test");
            clip.SetFloatCurve("abc", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            clip.EditPaths(_ => "1");
            clip.EditPaths(_ => "2");
            clip.EditPaths(_ => "abc123");
            
            var bindings = clip.GetFloatCurveBindings().ToList();
            Assert.AreEqual(1, bindings.Count);
            Assert.AreEqual("abc123", bindings[0].path);
            Assert.AreEqual(typeof(GameObject), bindings[0].type);
            Assert.AreEqual("m_IsActive", bindings[0].propertyName);
            Assert.AreEqual(2, clip.GetFloatCurve("abc123", typeof(GameObject), "m_IsActive")!.keys.Length);
        }
        
        [Test]
        public void PreservesAdditiveReferencePose()
        {
            // Create an original AnimationClip with additive reference pose
            AnimationClip refPose = TrackObject(new AnimationClip());
            refPose.name = "refPose";
            var refPoseEcb = EditorCurveBinding.FloatCurve("abc", typeof(GameObject), "m_IsActive"); 
            AnimationUtility.SetEditorCurve(refPose, refPoseEcb, new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            AnimationClip originalClip = TrackObject(new AnimationClip());
            originalClip.name = "originalClip";
            originalClip.SetCurve("def", typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1)));
            
            var settings = AnimationUtility.GetAnimationClipSettings(originalClip);
            settings.additiveReferencePoseClip = refPose;
            AnimationUtility.SetAnimationClipSettings(originalClip, settings);
        
            VirtualClip vc = VirtualClip.Clone(context, originalClip);
            AnimationClip committedClip = Commit(vc);
        
            var newRefPose = AnimationUtility.GetAnimationClipSettings(committedClip).additiveReferencePoseClip;
            Assert.AreNotSame(refPose, newRefPose);
            Assert.AreEqual(refPose.name, newRefPose.name);
            
            Assert.AreEqual(
                AnimationUtility.GetEditorCurve(refPose, refPoseEcb),
                AnimationUtility.GetEditorCurve(newRefPose, refPoseEcb)
            );
        }

        #if NDMF_VRCSDK3_AVATARS
        [Test]
        public void WhenProxyClipIsCloned_RevertToOriginalProxyClip()
        {
            using var _ = new ObjectRegistryScope(new ObjectRegistry(TrackObject(new GameObject()).transform));
            
            var proxy_path = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_afk.anim";
            var proxy_clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(proxy_path);
            
            var new_clip = TrackObject(new AnimationClip() { name = proxy_clip.name});
            ObjectRegistry.RegisterReplacedObject(proxy_clip, new_clip);
            
            var cloneContext = new CloneContext(VRChatPlatformAnimatorBindings.Instance);
            var virtualClip = VirtualClip.Clone(cloneContext, new_clip);
            
            var committedClip = new CommitContext().CommitObject(virtualClip);
            
            Assert.AreSame(proxy_clip, committedClip);
        }
        #endif

        [Test]
        public void LoopTime_IsPreserved(
            [Values(true, false)] bool loopTime,
            [Values(true, false)] bool hasRefClip,
            [Values(true, false)] bool hasEvent
        )
        {
            var clipName = "LoopTimeIsPreserved/LoopTime_" + (loopTime ? "ON" : "OFF");
            if (hasRefClip) clipName += " RefClip";
            if (hasEvent) clipName += " Event";

            var clip = LoadAsset<AnimationClip>(clipName + ".anim");
            
            var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var virtualClip = VirtualClip.Clone(cloneContext, clip);
            
            var committedClip = Commit(virtualClip);
            var settings = AnimationUtility.GetAnimationClipSettings(committedClip);
            
            Assert.AreEqual(loopTime, settings.loopTime);
        }

        [Test]
        public void MarkerClipConfigurationIsPreserved()
        {
            var markerClip = TrackObject(new AnimationClip());
            markerClip.name = "m1";
            markerClip.legacy = false;
            markerClip.localBounds = new Bounds(Vector3.zero, Vector3.one);
            markerClip.wrapMode = WrapMode.Loop;
            markerClip.frameRate = 60;
            
            var ref1 = TrackObject(new AnimationClip());
            var ref2 = TrackObject(new AnimationClip());

            AnimationClipSettings settings = new AnimationClipSettings();
            settings.additiveReferencePoseClip = ref1;
            settings.additiveReferencePoseTime = 0;
            AnimationUtility.SetAnimationClipSettings(markerClip, settings);

            var vc = VirtualClip.FromMarker(markerClip);
            vc.Name = "notamarker";
            vc.Legacy = true;
            vc.LocalBounds = new Bounds(Vector3.one, Vector3.one * 2);
            vc.AdditiveReferencePoseClip = VirtualClip.FromMarker(ref2);
            vc.AdditiveReferencePoseTime = 123;
            vc.WrapMode = WrapMode.PingPong;
            vc.FrameRate = 123;
            
            // Original clip is unchanged
            Assert.AreEqual("m1", markerClip.name);
            Assert.IsFalse(markerClip.legacy);
            Assert.AreEqual(new Bounds(Vector3.zero, Vector3.one), markerClip.localBounds);
            Assert.AreEqual(WrapMode.Loop, markerClip.wrapMode);
            Assert.AreEqual(60, markerClip.frameRate);
            Assert.AreEqual(ref1, AnimationUtility.GetAnimationClipSettings(markerClip).additiveReferencePoseClip);
            Assert.AreEqual(0, AnimationUtility.GetAnimationClipSettings(markerClip).additiveReferencePoseTime);
        }
        
        // TODO: animation clip settings/misc properties tests

        private static void AssertEqualNotSame(AnimationCurve newCommittedCurve, AnimationCurve newCurve)
        {
            Assert.IsNotNull(newCommittedCurve);
            Assert.AreNotSame(newCommittedCurve, newCurve);
            Assert.AreEqual(newCurve.keys.Length, newCommittedCurve.keys.Length);
            for (int i = 0; i < newCurve.keys.Length; i++)
            {
                Assert.AreEqual(newCurve.keys[i].time, newCommittedCurve.keys[i].time);
                Assert.AreEqual(newCurve.keys[i].value, newCommittedCurve.keys[i].value);
            }
        }
    }
}