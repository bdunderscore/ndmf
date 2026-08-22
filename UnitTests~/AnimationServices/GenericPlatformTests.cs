using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.UnitTestSupport;
using NUnit.Framework;
using nadena.dev.ndmf.platform;
using nadena.dev.ndmf.runtime.components;
using UnityEditor.Animations;
using UnityEngine;

namespace UnitTests.AnimationServices
{
    public class GenericPlatformTests : TestBase
    {
        public static IEnumerable<(string, Func<GenericPlatformTests, GameObject>)> CreateAvatarSource()
        {
            yield return ("Generic", t =>
            {
                var obj = t.TrackObject(new GameObject("test"));
                obj.AddComponent<Animator>();
                return obj;
            });

#if NDMF_VRCSDK3_AVATARS
            yield return ("VRChat", t => t.CreateRoot("VRChat"));
#endif
        }
        
        [Test]
        public void TracksAnimationsForAnimators(
            [ValueSource(nameof(CreateAvatarSource))]
            (string, Func<GenericPlatformTests, GameObject>) createAvatar
        )
        {
            var root = createAvatar.Item2(this);
            var animator = root.GetComponent<Animator>();
            
            var child = TrackObject(new GameObject("child"));
            child.transform.parent = root.transform;
            var childAnimator = child.AddComponent<Animator>();

            var startingController = new AnimatorController();
            childAnimator.runtimeAnimatorController = startingController;

            var buildContext = CreateContext(root);
            var ctx = buildContext.ActivateExtensionContext<VirtualControllerContext>();
            
            Assert.IsNotNull(ctx.Controllers[childAnimator]);
            Assert.IsFalse(ctx.Controllers.ContainsKey(animator));
            
            buildContext.DeactivateExtensionContext<VirtualControllerContext>();
            
            Assert.AreNotEqual(startingController, childAnimator.runtimeAnimatorController);
            Assert.NotNull(childAnimator.runtimeAnimatorController);
        }

        [Test]
        public void TracksAnimationsForCustomComponents(
            [ValueSource(nameof(CreateAvatarSource))]
            (string, Func<GenericPlatformTests, GameObject>) createAvatar
        )
        {
            var root = createAvatar.Item2(this);
            
            var child = TrackObject(new GameObject("child"));
            child.transform.parent = root.transform;
            var childComponent = child.AddComponent<VirtualizedComponent>();

            var startingController = new AnimatorController();
            childComponent.AnimatorController = startingController;

            var buildContext = CreateContext(root);
            var ctx = buildContext.ActivateExtensionContext<VirtualControllerContext>();
            
            Assert.IsNotNull(ctx.Controllers[childComponent]);
            
            buildContext.DeactivateExtensionContext<VirtualControllerContext>();
            
            Assert.AreNotEqual(startingController, childComponent.AnimatorController);
        }

        [Test]
        public void NormalizesOnDeactivate(
            [ValueSource(nameof(CreateAvatarSource))]
            (string, Func<GenericPlatformTests, GameObject>) createAvatar
        )
        {
            var root = createAvatar.Item2(this);
            
            var child = TrackObject(new GameObject("child"));
            child.transform.parent = root.transform;
            var childComponent = child.AddComponent<VirtualizedComponent>();
            
            var startingController = new AnimatorController();
            childComponent.AnimatorController = startingController;
            
            startingController.layers = new AnimatorControllerLayer[]
            {
                new AnimatorControllerLayer {name = "Layer1", defaultWeight = 0f, stateMachine = TrackObject(new AnimatorStateMachine())},
            };
            
            var buildContext = CreateContext(root);
            
            buildContext.ActivateExtensionContext<VirtualControllerContext>();
            buildContext.DeactivateExtensionContext<VirtualControllerContext>();

            var newController = (AnimatorController) childComponent.AnimatorController;
            Assert.AreEqual(1f, newController.layers[0].defaultWeight);
        }

        [Test]
        public void NDMF0014_VisemeInitializationMatchesExistingShapeByVisemeName()
        {
            var avatar = CreateRoot("avatar");
            var renderer = TrackObject(new GameObject("face")).AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(avatar.transform);
            var visemes = avatar.AddComponent<PortableBlendshapeVisemes>();
            visemes.TargetRenderer = renderer;
            visemes.Shapes.Add(new PortableBlendshapeVisemes.Shape
            {
                VisemeName = "legacy",
                Blendshape = CommonAvatarInfo.Viseme_aa
            });
            var info = new CommonAvatarInfo { VisemeRenderer = renderer };
            info.VisemeBlendshapes[CommonAvatarInfo.Viseme_aa] = "new-blendshape";

            Assert.DoesNotThrow(() => GenericPlatform.Instance.InitFromCommonAvatarInfo(avatar, info));
            Assert.AreEqual(
                "new-blendshape",
                visemes.Shapes.Single(shape => shape.VisemeName == CommonAvatarInfo.Viseme_aa).Blendshape
            );
        }
    }
}