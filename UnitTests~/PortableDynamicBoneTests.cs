using nadena.dev.ndmf.multiplatform.components;
using NUnit.Framework;
using UnityEngine;
#if NDMF_VRCSDK3_AVATARS
using nadena.dev.ndmf.vrchat;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

namespace UnitTests
{
    public class PortableDynamicBoneTests : TestBase
    {
        [Test]
        public void NDMF0025_OutsideAvatarRootUsesGenericTemplate()
        {
            var root = TrackObject(new GameObject("root"));
            var dynamicBone = TrackObject(new GameObject("dynamic-bone")).AddComponent<PortableDynamicBone>();

            Assert.DoesNotThrow(() =>
                Assert.AreEqual("generic", PortableDynamicBone.GuessTemplateName(dynamicBone, root.transform)));
        }
        #if NDMF_VRCSDK3_AVATARS
        [Test]
        public void NDMF0018_ConvertsInactivePhysBoneInEditMode()
        {
            var avatar = CreateRoot("avatar");
            var physBoneObject = CreateChild(avatar, "inactive physbone");
            var physBone = physBoneObject.AddComponent<VRCPhysBone>();
            physBoneObject.SetActive(false);

            VRChatPlatform.Instance.GeneratePortableComponents(avatar, false);

            Assert.AreSame(physBoneObject, physBone.GetComponent<PortableDynamicBone>().gameObject);
        }
        #endif
    }
}

