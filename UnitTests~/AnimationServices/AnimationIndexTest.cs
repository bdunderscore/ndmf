using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class AnimationIndexTest
    {
        [Test]
        public void TestBasicIndexing()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            var clip2 = VirtualClip.Create("c2");
            
            layer.StateMachine.AddState("s1", motion: clip1);
            layer.StateMachine.AddState("s2", motion: clip2);
            
            var index = new AnimationIndex( new [] { controller });
            
            // Verify the index starts empty. This also sets us up to test cache invalidation.
            Assert.IsEmpty(index.GetClipsForObjectPath("x"));
            
            var binding1 = EditorCurveBinding.FloatCurve("path1", typeof(Transform), "prop1");
            var binding2 = EditorCurveBinding.FloatCurve("path2", typeof(Transform), "prop2");

            clip1.SetFloatCurve(binding1, AnimationCurve.Constant(0, 1, 1));
            clip1.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            clip2.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));

            var p1clips = index.GetClipsForObjectPath("path1").ToList();
            var p2clips = index.GetClipsForObjectPath("path2").ToList();
            
            Assert.AreEqual(1, p1clips.Count);
            Assert.AreEqual(2, p2clips.Count);
            Assert.AreEqual(clip1, p1clips[0]);
            Assert.Contains(clip1, p2clips);
            Assert.Contains(clip2, p2clips);
            
            var b1clips = index.GetClipsForBinding(binding1).ToList();
            var b2clips = index.GetClipsForBinding(binding2).ToList();
            
            Assert.AreEqual(1, b1clips.Count);
            Assert.AreEqual(2, b2clips.Count);
            
            Assert.AreEqual(clip1, b1clips[0]);
            Assert.Contains(clip1, b2clips);
            Assert.Contains(clip2, b2clips);
        }
        
        [Test]
        public void TestRewritePaths()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            var clip2 = VirtualClip.Create("c2");
            
            layer.StateMachine.AddState("s1", motion: clip1);
            layer.StateMachine.AddState("s2", motion: clip2);
            
            var index = new AnimationIndex( new [] { controller });
            
            var binding1 = EditorCurveBinding.FloatCurve("path1", typeof(Transform), "prop1");
            var binding2 = EditorCurveBinding.FloatCurve("path2", typeof(Transform), "prop2");

            clip1.SetFloatCurve(binding1, AnimationCurve.Constant(0, 1, 1));
            clip1.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            clip2.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            
            index.RewritePaths(new Dictionary<string, string>
            {
                { "path1", "path3" },
                { "path2", "path4" }
            });
            
            // Verify the clips were in fact rewritten
            var clip1paths = clip1.GetFloatCurveBindings().Select(ecb => ecb.path).ToList();
            var clip2paths = clip2.GetFloatCurveBindings().Select(ecb => ecb.path).ToList();
            
            Assert.Contains("path3", clip1paths);
            Assert.Contains("path4", clip1paths);
            Assert.Contains("path4", clip2paths);
            
            Assert.IsFalse(clip1paths.Contains("path1"));
            Assert.IsFalse(clip1paths.Contains("path2"));
            Assert.IsFalse(clip2paths.Contains("path2"));
            
            // Verify the index was updated

            var p1clips = index.GetClipsForObjectPath("path1").ToList();
            var p2clips = index.GetClipsForObjectPath("path2").ToList();
            var p3clips = index.GetClipsForObjectPath("path3").ToList();
            var p4clips = index.GetClipsForObjectPath("path4").ToList();
            
            Assert.IsEmpty(p1clips);
            Assert.IsEmpty(p2clips);
            Assert.AreEqual(1, p3clips.Count);
            Assert.AreEqual(2, p4clips.Count);
            Assert.AreEqual(clip1, p3clips[0]);
            Assert.Contains(clip1, p4clips);
            Assert.Contains(clip2, p4clips);
            
            var b1clips = index.GetClipsForBinding(binding1).ToList();
            var b2clips = index.GetClipsForBinding(binding2).ToList();
            
            Assert.IsEmpty(b1clips);
            Assert.IsEmpty(b2clips);
        }

        [Test]
        public void RewritePathDistinguishesBetweenMissingAndNullMappings()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            var clip2 = VirtualClip.Create("c2");
            
            layer.StateMachine.AddState("s1", motion: clip1);
            layer.StateMachine.AddState("s2", motion: clip2);
            
            var index = new AnimationIndex( new [] { controller });
            
            var binding1 = EditorCurveBinding.FloatCurve("path1", typeof(Transform), "prop1");
            var binding2 = EditorCurveBinding.FloatCurve("path2", typeof(Transform), "prop2");

            clip1.SetFloatCurve(binding1, AnimationCurve.Constant(0, 1, 1));
            clip1.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            
            index.RewritePaths(new Dictionary<string, string>
            {
                { "path1", null }
            });

            var p1clips = index.GetClipsForObjectPath("path1").ToList();
            var p2clips = index.GetClipsForObjectPath("path2").ToList();
            
            Assert.IsEmpty(p1clips);
            Assert.AreEqual(1, p2clips.Count);
        }

        [Test]
        public void TestEditClipsByBinding()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            var clip2 = VirtualClip.Create("c2");
            
            layer.StateMachine.AddState("s1", motion: clip1);
            layer.StateMachine.AddState("s2", motion: clip2);
            
            var index = new AnimationIndex( new [] { controller });
            
            var binding1 = EditorCurveBinding.FloatCurve("path1", typeof(Transform), "prop1");
            var binding2 = EditorCurveBinding.FloatCurve("path2", typeof(Transform), "prop2");
            var binding3 = EditorCurveBinding.FloatCurve("path3", typeof(Transform), "prop3");

            clip1.SetFloatCurve(binding1, AnimationCurve.Constant(0, 1, 1));
            clip1.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            clip2.SetFloatCurve(binding2, AnimationCurve.Constant(0, 1, 1));
            
            List<VirtualClip> visited = new();
            index.EditClipsByBinding(new [] { binding1 }, clip =>
            {
                visited.Add(clip);
                clip.SetFloatCurve(binding1, AnimationCurve.Constant(0, 1, 2));
                clip.SetFloatCurve(binding2, null);
                clip.SetFloatCurve(binding3, AnimationCurve.Constant(0, 1, 2));
            });
            
            // Verify only the correct clips were visited
            Assert.AreEqual(1, visited.Count);
            Assert.AreEqual(clip1, visited[0]);
            
            // Verify that we updated the index
            var b2clips = index.GetClipsForBinding(binding2).ToList();
            var b3clips = index.GetClipsForBinding(binding3).ToList();
            
            Assert.AreEqual(1, b2clips.Count);
            Assert.AreEqual(1, b3clips.Count);
            Assert.AreEqual(clip2, b2clips[0]);
            Assert.AreEqual(clip1, b3clips[0]);
        }

        [Test]
        public void TestGraphLoops()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");
            
            var sm1 = VirtualStateMachine.Create(context, "sm1");
            var sm2 = VirtualStateMachine.Create(context, "sm2");

            layer.StateMachine.StateMachines = layer.StateMachine.StateMachines.Add(new ()
            {
                StateMachine = sm1
            });
            sm1.StateMachines = sm1.StateMachines.Add(new ()
            {
                StateMachine = sm2
            });
            sm2.StateMachines = sm2.StateMachines.Add(new ()
            {
                StateMachine = sm1
            });
            
            var index = new AnimationIndex( new [] { controller });
            
            // Make sure we don't infinite loop
            Assert.IsEmpty(index.GetClipsForObjectPath("x"));
        }

        [Test]
        public void RewriteObjectCurvesTest()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");
            
            var clip1 = VirtualClip.Create("c1");
            layer.StateMachine!.AddState("s1", motion: clip1);

            var shader = Shader.Find("Standard");
            var m1 = new Material(shader);
            m1.name = "m1";
            var m2 = new Material(shader);
            m2.name = "m2";
            
            var ecb = EditorCurveBinding.PPtrCurve("path", typeof(MeshRenderer), "m_Materials.Array.data[0]");
            clip1.SetObjectCurve(ecb, new ObjectReferenceKeyframe[]
            {
                new ObjectReferenceKeyframe
                {
                    time = 0,
                    value = m1
                },
                new ObjectReferenceKeyframe
                {
                    time = 1,
                    value = m2
                }
            });
            
            var index = new AnimationIndex( new [] { controller });
            
            Assert.That(index.GetPPtrReferencedObjects, Is.EquivalentTo(new [] { m1, m2 }));
            
            var m3 = new Material(shader);
            m3.name = "m3";
            
            index.RewriteObjectCurves(mat => mat == m2 ? m3 : mat);
            
            var newCurve = clip1.GetObjectCurve(ecb);
            
            Assert.AreEqual(2, newCurve.Length);
            Assert.AreEqual(m1, newCurve[0].value);
            Assert.AreEqual(m3, newCurve[1].value);
        }

        [Test]
        public void NullObjectKeyframeTest()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");
            
            var clip1 = VirtualClip.Create("c1");
            layer.StateMachine!.AddState("s1", motion: clip1);

            var shader = Shader.Find("Standard");
            var m1 = new Material(shader);
            m1.name = "m1";
            var m2 = new Material(shader);
            m2.name = "m2";
            
            var ecb = EditorCurveBinding.PPtrCurve("path", typeof(MeshRenderer), "m_Materials.Array.data[0]");
            clip1.SetObjectCurve(ecb, new ObjectReferenceKeyframe[]
            {
                new ObjectReferenceKeyframe
                {
                    time = 0,
                    value = m1
                },
                new ObjectReferenceKeyframe()
                {
                    time = 0.5f,
                    value = null
                },
                new ObjectReferenceKeyframe
                {
                    time = 1,
                    value = m2
                }
            });
            
            
            var index = new AnimationIndex( new [] { controller });
            
            Assert.That(index.GetPPtrReferencedObjects, Is.EquivalentTo(new [] { m1, m2 }));
            
            var m3 = new Material(shader);
            m3.name = "m3";
            
            index.RewriteObjectCurves(mat => mat == m2 ? m3 : mat);
            
            var newCurve = clip1.GetObjectCurve(ecb);
            
            Assert.AreEqual(3, newCurve.Length);
            Assert.AreEqual(m1, newCurve[0].value);
            Assert.AreEqual(null, newCurve[1].value);
            Assert.AreEqual(m3, newCurve[2].value);
        }

        [Test]
        public void GetPPtrReferencedObjectsWithBindingTest()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            layer.StateMachine!.AddState("s1", motion: clip1);

            var shader = Shader.Find("Standard");
            var m1 = new Material(shader) { name = "m1" };
            var m2 = new Material(shader) { name = "m2" };
            var ecb = EditorCurveBinding.PPtrCurve("path", typeof(MeshRenderer), "m_Materials.Array.data[0]");
            clip1.SetObjectCurve(ecb, new ObjectReferenceKeyframe[]
            {
                new ObjectReferenceKeyframe { time = 0, value = m1 },
                new ObjectReferenceKeyframe { time = 1, value = m2 }
            });

            var index = new AnimationIndex(new[] { controller });
            var pairs = index.GetPPtrReferencedObjectsWithBinding.ToList();
            Assert.That(pairs, Has.Exactly(1).Matches<(EditorCurveBinding, Object)>(p => p.Item2 == m1 && p.Item1.Equals(ecb)));
            Assert.That(pairs, Has.Exactly(1).Matches<(EditorCurveBinding, Object)>(p => p.Item2 == m2 && p.Item1.Equals(ecb)));
        }

        [Test]
        public void RewriteObjectCurvesWithBindingTest()
        {
            var context = new CloneContext(GenericPlatformAnimatorBindings.Instance);
            var controller = VirtualAnimatorController.Create(context, "test");
            var layer = controller.AddLayer(LayerPriority.Default, "test");

            var clip1 = VirtualClip.Create("c1");
            layer.StateMachine!.AddState("s1", motion: clip1);

            var shader = Shader.Find("Standard");
            var m1 = new Material(shader) { name = "m1" };
            var m2 = new Material(shader) { name = "m2" };
            var m3 = new Material(shader) { name = "m3" };
            var ecb = EditorCurveBinding.PPtrCurve("path", typeof(MeshRenderer), "m_Materials.Array.data[0]");
            clip1.SetObjectCurve(ecb, new ObjectReferenceKeyframe[]
            {
                new ObjectReferenceKeyframe { time = 0, value = m1 },
                new ObjectReferenceKeyframe { time = 1, value = m2 }
            });

            var index = new AnimationIndex(new[] { controller });
            index.RewriteObjectCurves((binding, obj) => (obj == m2 && binding.Equals(ecb)) ? m3 : obj);
            var newCurve = clip1.GetObjectCurve(ecb);
            Assert.AreEqual(2, newCurve.Length);
            Assert.AreEqual(m1, newCurve[0].value);
            Assert.AreEqual(m3, newCurve[1].value);
        }
    }
}

