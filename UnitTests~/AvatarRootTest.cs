using nadena.dev.ndmf.runtime;
using NUnit.Framework;
using UnityEngine;

namespace UnitTests.AvatarRootTests
{
    public class AvatarRoot : TestBase
    {
        private GameObject CreateGenericRoot(string name) => CreateRoot(name, isVRC: false);
        private GameObject CreateVRCRoot(string name) => CreateRoot(name, isVRC: true);

        private Transform parentAvatar;
        private Transform childAvatar;
        
        private void ParentIsAvatar()
        {
            Assert.That(RuntimeUtil.IsAvatarRoot(parentAvatar), Is.True);
            Assert.That(RuntimeUtil.IsAvatarRoot(childAvatar), Is.False);
            Assert.That(RuntimeUtil.FindAvatarInParents(parentAvatar), Is.EqualTo(parentAvatar));
            Assert.That(RuntimeUtil.FindAvatarInParents(childAvatar), Is.EqualTo(parentAvatar));
            Assert.That(RuntimeUtil.FindAvatarsInScene(parentAvatar.gameObject.scene), Is.EquivalentTo(new [] { parentAvatar }));
        }

        [Test]
        public void TestGenericContainsGeneric()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateGenericRoot("child").transform;

            childAvatar.parent = parentAvatar;

            ParentIsAvatar();
        }

#if NDMF_VRCSDK3_AVATARS
        [Test]
        public void TestGenericContainsVRC()
        {
            parentAvatar = CreateGenericRoot("parent").transform;
            childAvatar = CreateVRCRoot("child").transform;

            childAvatar.parent = parentAvatar;

            ParentIsAvatar();
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
            
            ParentIsAvatar();
        }
#endif

    }
}