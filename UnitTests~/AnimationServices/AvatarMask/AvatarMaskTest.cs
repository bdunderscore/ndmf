using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class AvatarMaskTest : TestBase
    {
        class ExtractedMask {
            public int[] humanoidMaskElements;
            public List<(string, float)> transformMaskElements;
            
            public ExtractedMask(int[] humanoidMaskElements, List<(string, float)> transformMaskElements)
            {
                this.humanoidMaskElements = humanoidMaskElements;
                this.transformMaskElements = transformMaskElements;
            }

            public override bool Equals(object obj)
            {
                if (obj == this) return true;
                if (!(obj is ExtractedMask other)) return false;

                if (!humanoidMaskElements.SequenceEqual(other.humanoidMaskElements)) return false;
                return transformMaskElements.SequenceEqual(other.transformMaskElements);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(humanoidMaskElements, transformMaskElements);
            }

            public static ExtractedMask FromAvatarMask(AvatarMask mask)
            {
                if (mask == null)
                {
                    Debug.LogError("Avatar mask is null");
                }
                
                var so = new SerializedObject(mask);
                var m_Mask = so.FindProperty("m_Mask");
                var m_Elements = so.FindProperty("m_Elements");

                var humanoidMaskElements = new int[m_Mask.arraySize];
                var maskElementCount = m_Mask.arraySize;
                for (var i = 0; i < maskElementCount; i++)
                {
                    var element = m_Mask.GetArrayElementAtIndex(i);
                    humanoidMaskElements[i] = element.intValue;
                }

                var transformMaskElements = new List<(string, float)>();
                var transformElementCount = m_Elements.arraySize;
                for (var i = 0; i < transformElementCount; i++)
                {
                    var element = m_Elements.GetArrayElementAtIndex(i);
                    var path = element.FindPropertyRelative("m_Path").stringValue;
                    var weight = element.FindPropertyRelative("m_Weight").floatValue;
                
                    transformMaskElements.Add((path, weight));
                }
                
                return new ExtractedMask(humanoidMaskElements, transformMaskElements);
            }
        }

        private BuildContext CreateTestAvatar()
        {
            var av = CreatePrefab("AvatarMaskRewriteTest.prefab");
            var context = CreateContext(av);

            var asc = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            VirtualAnimatorController value = asc.ControllerContext.Clone(LoadAsset<AnimatorController>("base-fx-ac.controller"));
            asc.ControllerContext.Controllers[0] = value;
            VirtualAnimatorController value1 = asc.ControllerContext.Clone(LoadAsset<AnimatorController>("av-mask-test-ac.controller"));
            asc.ControllerContext.Controllers[1] = value1;

            Assert.IsNotNull(asc.ControllerContext.Controllers[0]?.Layers.First().AvatarMask);
            Assert.IsNotNull(asc.ControllerContext.Controllers[1]?.Layers.First().AvatarMask);
            
            new AnimationIndex(new [] { asc.ControllerContext.Controllers[1] }).RewritePaths(p =>
            {
                if (p == "") return "";
                else return "parent/anim-root/" + p;
            });
            
            Assert.IsNotNull(asc.ControllerContext.Controllers[0]?.Layers.First().AvatarMask);
            Assert.IsNotNull(asc.ControllerContext.Controllers[1]?.Layers.First().AvatarMask);
            
            return context;
        }
        
        [Test]
        public void whenAvatarMaskIsPresentOnMergedAnimator_originalMaskIsUnchanged()
        {
            var ctx = CreateTestAvatar();
            var vcc = ctx.Extension<VirtualControllerContext>();
            ctx.DeactivateExtensionContext<AnimatorServicesContext>();
            var originalMask = LoadAsset<AvatarMask>("ndmf-test-mask.mask");
            
            var originalState = ExtractedMask.FromAvatarMask(originalMask);
            var commit = new CommitContext();

            var mergedController = commit.CommitObject(vcc.Controllers[1]);
            var baseController = commit.CommitObject(vcc.Controllers[0]);
            
            var newMask = mergedController!.layers[0].avatarMask;
            var baseFxMask = baseController!.layers[0].avatarMask;
            Assert.AreNotEqual(originalMask, newMask);
            Assert.AreNotEqual(originalMask, baseFxMask);
            
            var newState = ExtractedMask.FromAvatarMask(originalMask);
            
            Assert.AreEqual(originalState, newState);
        }
        
        [Test]
        public void whenAvatarMaskIsPresentOnMergedAnimator_rewritesPathsByPrefix()
        {
            var ctx = CreateTestAvatar();
            var vcc = ctx.Extension<VirtualControllerContext>();
            ctx.DeactivateExtensionContext<AnimatorServicesContext>();
            
            var commit = new CommitContext();

            var mergedController = commit.CommitObject(vcc.Controllers[1]);
            
            var newMask = mergedController!.layers[0].avatarMask;
            var state = ExtractedMask.FromAvatarMask(newMask);
            
            var parentIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent");
            var animRootIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/anim-root");
            var bodyIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/anim-root/Body");
            
            Assert.Greater(parentIndex, -1);
            Assert.Greater(animRootIndex, -1);
            Assert.Greater(bodyIndex, -1);
            
            Assert.Greater(animRootIndex, parentIndex);
            Assert.Greater(bodyIndex, animRootIndex);
            
            // Body is still enabled; the injected parent and parent/anim-root are not
            Assert.IsTrue(state.transformMaskElements[parentIndex].Item2 < 0.5f);
            Assert.IsTrue(state.transformMaskElements[animRootIndex].Item2 < 0.5f);
            Assert.IsTrue(state.transformMaskElements[bodyIndex].Item2 > 0.5f);
            
            // Original paths are removed
            Assert.IsFalse(state.transformMaskElements.Any(e => e.Item1 == "Body"));
        }

        [Test]
        public void whenObjectMovedByBoneProxy_avatarMaskPathsAreRewritten()
        {
            var ctx = CreateTestAvatar();

            var hips = ctx.AvatarRootTransform.Find("parent/anim-root/Armature/Hips");
            var destination = ctx.AvatarRootTransform.Find("parent/relocated-to");
            hips.parent = destination;
            
            var vcc = ctx.Extension<VirtualControllerContext>();
            ctx.DeactivateExtensionContext<AnimatorServicesContext>();
            
            // Armature/Hips -> parent/relocated-to/Hips
            
            var newMask = new CommitContext().CommitObject(vcc.Controllers[1])!.layers[0].avatarMask;
            var state = ExtractedMask.FromAvatarMask(newMask);
            
            var parentIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent");
            var relocatedToIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/relocated-to");
            var hipsIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/relocated-to/Hips");
            // UpperLeg.L will be enabled, .R will be disabled
            var upperLegLIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/relocated-to/Hips/UpperLeg.L");
            var upperLegRIndex = state.transformMaskElements.FindIndex(e => e.Item1 == "parent/relocated-to/Hips/UpperLeg.R");
            
            Assert.Greater(parentIndex, -1);
            Assert.Greater(relocatedToIndex, -1);
            Assert.Greater(hipsIndex, -1);
            Assert.Greater(upperLegLIndex, -1);
            Assert.Greater(upperLegRIndex, -1);
            
            Assert.Greater(relocatedToIndex, parentIndex);
            Assert.Greater(hipsIndex, relocatedToIndex);
            Assert.Greater(upperLegLIndex, hipsIndex);
            Assert.Greater(upperLegRIndex, hipsIndex);
            
            // Hips -> 1, .L -> 1, .R -> 0
            Assert.IsTrue(state.transformMaskElements[parentIndex].Item2 < 0.5f);
            Assert.IsTrue(state.transformMaskElements[relocatedToIndex].Item2 < 0.5f);
            Assert.IsTrue(state.transformMaskElements[hipsIndex].Item2 > 0.5f);
            Assert.IsTrue(state.transformMaskElements[upperLegLIndex].Item2 > 0.5f);
            Assert.IsTrue(state.transformMaskElements[upperLegRIndex].Item2 < 0.5f);
            
            // Original paths are removed
            Assert.IsFalse(state.transformMaskElements.Any(e => e.Item1 == "Armature/Hips"));
            Assert.IsFalse(state.transformMaskElements.Any(e => e.Item1 == "Armature/Hips/UpperLeg.L"));
            Assert.IsFalse(state.transformMaskElements.Any(e => e.Item1 == "Armature/Hips/UpperLeg.R"));
        }
    }
}