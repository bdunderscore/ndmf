using nadena.dev.ndmf.runtime;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.AvatarRootTests
{
    public class AvatarRoot : TestBase
    {
        private GameObject CreateGenericRoot(string name) => CreatePlatformRoot(name, isVRC: false, isVRM0: false, isVRM1: false);
        private GameObject CreateVRCRoot(string name) => CreatePlatformRoot(name, isVRC: true, isVRM0: false, isVRM1: false);
        private GameObject CreateVRM0Root(string name) => CreatePlatformRoot(name, isVRC: false, isVRM0: true, isVRM1: false);
        private GameObject CreateVRM1Root(string name) => CreatePlatformRoot(name, isVRC: false, isVRM0: false, isVRM1: true);
        private GameObject CreateHybridRoot(string name) => CreatePlatformRoot(name, isVRC: true, isVRM0: true, isVRM1: true);

        private Transform parentAvatar;
        private Transform childAvatar;
        
        private void NoAvatars()
        {
            Assert.That(RuntimeUtil.IsAvatarRoot(parentAvatar), Is.False);
            Assert.That(RuntimeUtil.IsAvatarRoot(childAvatar), Is.False);
            Assert.That(RuntimeUtil.FindAvatarInParents(parentAvatar), Is.Null);
            Assert.That(RuntimeUtil.FindAvatarInParents(childAvatar), Is.Null);
            Assert.That(RuntimeUtil.FindAvatarsInScene(parentAvatar.gameObject.scene), Is.EquivalentTo(System.Array.Empty<Transform>()));
        }

        private void ParentIsAvatar()
        {
            Assert.That(RuntimeUtil.IsAvatarRoot(parentAvatar), Is.True);
            Assert.That(RuntimeUtil.IsAvatarRoot(childAvatar), Is.False);
            Assert.That(RuntimeUtil.FindAvatarInParents(parentAvatar), Is.EqualTo(parentAvatar));
            Assert.That(RuntimeUtil.FindAvatarInParents(childAvatar), Is.EqualTo(parentAvatar));
            Assert.That(RuntimeUtil.FindAvatarsInScene(parentAvatar.gameObject.scene), Is.EquivalentTo(new [] { parentAvatar }));
        }

        private void ChildIsAvatar()
        {
            Assert.That(RuntimeUtil.IsAvatarRoot(parentAvatar), Is.False);
            Assert.That(RuntimeUtil.IsAvatarRoot(childAvatar), Is.True);
            Assert.That(RuntimeUtil.FindAvatarInParents(parentAvatar), Is.EqualTo(null));
            Assert.That(RuntimeUtil.FindAvatarInParents(childAvatar), Is.EqualTo(childAvatar));
            Assert.That(RuntimeUtil.FindAvatarsInScene(parentAvatar.gameObject.scene), Is.EquivalentTo(new [] { childAvatar }));
        }

        private void ParentAndChildAreAvatars()
        {
            Assert.That(RuntimeUtil.IsAvatarRoot(parentAvatar), Is.True);
            Assert.That(RuntimeUtil.IsAvatarRoot(childAvatar), Is.True);
            Assert.That(RuntimeUtil.FindAvatarInParents(parentAvatar), Is.EqualTo(parentAvatar));
            Assert.That(RuntimeUtil.FindAvatarInParents(childAvatar), Is.EqualTo(childAvatar));
            Assert.That(RuntimeUtil.FindAvatarsInScene(parentAvatar.gameObject.scene), Is.EquivalentTo(new [] { parentAvatar, childAvatar }));
        }

        [Test]
        public void TestGenericContainsGeneric()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateGenericRoot("child").transform;

            childAvatar.parent = parentAvatar;

#if NDMF_VRCSDK3_AVATARS || NDMF_VRM0 || NDMF_VRM1
            NoAvatars();
#else
            // Use fallback heuristic 
            ParentIsAvatar();
#endif
        }

#if NDMF_VRCSDK3_AVATARS
        [Test]
        public void TestGenericContainsVRC()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateVRCRoot("child").transform;

            childAvatar.parent = parentAvatar;

            ChildIsAvatar();
        }

        [Test]
        public void TestVRCContainsGeneric()
        {
            parentAvatar = CreateVRCRoot("parent").transform;
            childAvatar = CreateGenericRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentIsAvatar();
        }

        [Test]
        public void TestVRCContainsVRC()
        {
            parentAvatar = CreateVRCRoot("parent").transform;
            childAvatar = CreateVRCRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentAndChildAreAvatars();
        }
#endif

#if NDMF_VRM0
        [Test]
        public void TestGenericContainsVRM0()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateVRM1Root("child").transform;

            childAvatar.parent = parentAvatar;
            
            ChildIsAvatar();
        }

        [Test]
        public void TestVRM0ContainsGeneric()
        {
            parentAvatar = CreateVRM1Root("parent").transform;
            childAvatar = CreateGenericRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentIsAvatar();
        }

        [Test]
        public void TestVRM0ContainsVRM0()
        {
            parentAvatar = CreateVRM1Root("parent").transform;
            childAvatar = CreateVRM1Root("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentAndChildAreAvatars();
        }
#endif

#if NDMF_VRCSDK3_AVATARS && NDMF_VRM0
        [Test]
        public void TestGenericContainsHybrid()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateHybridRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ChildIsAvatar();
        }

        [Test]
        public void TestHybridContainsGeneric()
        {
            parentAvatar = CreateHybridRoot("parent").transform;
            childAvatar = CreateGenericRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentIsAvatar();
        }

        [Test]
        public void TestHybridContainsHybrid()
        {
            parentAvatar = CreateHybridRoot("parent").transform;
            childAvatar = CreateHybridRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentAndChildAreAvatars();
        }

        [Test]
        public void TestVRCContainsVRM0()
        {
            parentAvatar = CreateVRCRoot("parent").transform;
            childAvatar = CreateVRM0Root("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentAndChildAreAvatars();
        }

        [Test]
        public void TestVRM0ContainsVRC()
        {
            parentAvatar = CreateVRM0Root("parent").transform;
            childAvatar = CreateVRCRoot("child").transform;

            childAvatar.parent = parentAvatar;
            
            ParentAndChildAreAvatars();
        }
#endif
    }
}