#if NDMF_VRCSDK3_AVATARS
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace UnitTests.Parameters
{
    public class VRChatBindingsTest : TestBase
    {
        
        [Test]
        public void VRCParams()
        {
            var av = CreateRoot("avatar");

            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            desc.expressionParameters.parameters = new[]
            {
                new VRCExpressionParameters.Parameter()
                {
                    name = "syncedFloat",
                    valueType = VRCExpressionParameters.ValueType.Float,
                    networkSynced = true,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "unsyncedFloat",
                    valueType = VRCExpressionParameters.ValueType.Float,
                    networkSynced = false,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "syncedInt",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    networkSynced = true,
                },
                new VRCExpressionParameters.Parameter()
                {
                    name = "syncedBool",
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    networkSynced = true,
                },
            };

            var parameters = ParameterInfo.ForUI.GetParametersForObject(av)
                .ToImmutableDictionary(p => p.EffectiveName, p => p);

            Assert.AreEqual(4, parameters.Count);
            Assert.IsTrue(parameters.ContainsKey("syncedFloat"));
            Assert.IsTrue(parameters.ContainsKey("unsyncedFloat"));
            Assert.IsTrue(parameters.ContainsKey("syncedInt"));
            Assert.IsTrue(parameters.ContainsKey("syncedBool"));
            Assert.IsFalse(parameters.Values.Any(p => p.IsAnimatorOnly));
            
            Assert.AreEqual(AnimatorControllerParameterType.Float, parameters["syncedFloat"].ParameterType);
            Assert.AreEqual(8, parameters["syncedFloat"].BitUsage);
            Assert.AreEqual(ParameterNamespace.Animator, parameters["syncedFloat"].Namespace);
            Assert.IsTrue(parameters["syncedFloat"].WantSynced);
            
            Assert.AreEqual(AnimatorControllerParameterType.Float, parameters["unsyncedFloat"].ParameterType);
            Assert.AreEqual(0, parameters["unsyncedFloat"].BitUsage);
            Assert.AreEqual(ParameterNamespace.Animator, parameters["unsyncedFloat"].Namespace);
            Assert.IsFalse(parameters["unsyncedFloat"].WantSynced);
            
            Assert.AreEqual(AnimatorControllerParameterType.Int, parameters["syncedInt"].ParameterType);
            Assert.AreEqual(8, parameters["syncedInt"].BitUsage);
            Assert.AreEqual(ParameterNamespace.Animator, parameters["syncedInt"].Namespace);
            Assert.IsTrue(parameters["syncedInt"].WantSynced);
            
            Assert.AreEqual(AnimatorControllerParameterType.Bool, parameters["syncedBool"].ParameterType);
            Assert.AreEqual(1, parameters["syncedBool"].BitUsage);
            Assert.AreEqual(ParameterNamespace.Animator, parameters["syncedBool"].Namespace);
            Assert.IsTrue(parameters["syncedBool"].WantSynced);
        }

        [Test]
        public void TestContact()
        {
            var root = CreateRoot("avatar");
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var obj = CreateChild(root, "foo");
            var contact = obj.AddComponent<VRCContactReceiver>();
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(obj)
                .ToImmutableDictionary(p => p.EffectiveName, p => p);
            Assert.IsTrue(parameters.IsEmpty);

            contact.parameter = "abc";
            parameters = ParameterInfo.ForUI.GetParametersForObject(obj)
                .ToImmutableDictionary(p => p.EffectiveName, p => p);
            
            Assert.AreEqual(1, parameters.Count);
            var param = parameters["abc"];
            Assert.AreEqual(null, param.ParameterType);
            Assert.AreEqual(0, param.BitUsage);
            Assert.AreEqual(ParameterNamespace.Animator, param.Namespace);
            Assert.IsFalse(param.WantSynced);
            Assert.IsTrue(param.IsAnimatorOnly);
        }

        [Test]
        public void TestPhysBone()
        {
            var root = CreateRoot("avatar");
            var desc = root.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var obj = CreateChild(root, "foo");
            var pb = obj.AddComponent<VRCPhysBone>();
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(obj)
                .ToImmutableDictionary(p => p.EffectiveName, p => p);
            Assert.IsTrue(parameters.IsEmpty);
            
            pb.parameter = "abc";
            
            parameters = ParameterInfo.ForUI.GetParametersForObject(obj)
                .ToImmutableDictionary(p => p.EffectiveName, p => p);
            
            Assert.AreEqual(1, parameters.Count);
            var param = parameters["abc"];
            Assert.AreEqual(null, param.ParameterType);
            Assert.AreEqual(0, param.BitUsage);
            Assert.AreEqual(ParameterNamespace.PhysBonesPrefix, param.Namespace);
            Assert.IsFalse(param.WantSynced);
            Assert.IsTrue(param.IsAnimatorOnly);
        }
    }
}
#endif