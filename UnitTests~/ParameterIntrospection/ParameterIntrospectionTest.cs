#if NDMF_VRCSDK3_AVATARS
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.UnitTestSupport;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace UnitTests.Parameters
{
    
    public class ParameterIntrospectionTest : TestBase
    {
        [TearDown]
        public void TearDown()
        {
            ParamTestComponentProvider.ClearAll();
        }
        
        [Test]
        public void TestEmpty()
        {
            var av = CreateRoot("avatar");

            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(av);
            Assert.IsEmpty(parameters);
        }

        [Test]
        public void SimpleUsage()
        {
            var av = CreateRoot("avatar");
            
            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var obj1 = CreateChild(av, "obj1");
            var obj2 = CreateChild(av, "obj2");

            var tc = obj1.AddComponent<ParamTestComponent>();
            var p1 = new ProvidedParameter("p1", ParameterNamespace.Animator, tc, InternalPasses.Instance,
                AnimatorControllerParameterType.Bool);
            ParamTestComponentProvider.SetParameters(tc, p1);

            var parameters = ParameterInfo.ForUI.GetParametersForObject(av).ToList();
            Assert.AreEqual(1, parameters.Count());
            Assert.AreEqual(p1, parameters[0]);
            Assert.AreNotSame(p1, parameters[0]);

            parameters = ParameterInfo.ForUI.GetParametersForObject(obj1).ToList();
            Assert.AreEqual(1, parameters.Count());
            Assert.AreEqual(p1, parameters[0]);
            
            parameters = ParameterInfo.ForUI.GetParametersForObject(obj2).ToList();
            Assert.IsEmpty(parameters);
        }

        [Test]
        public void SimpleRemap()
        {
            var av = CreateRoot("avatar");
            
            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            
            var obj1 = CreateChild(av, "obj1");
            var obj2 = CreateChild(obj1, "obj2");
            
            var o1t1 = obj1.AddComponent<ParamTestComponent>();
            var o2t1 = obj2.AddComponent<ParamTestComponent>();
            var o2t2 = obj2.AddComponent<ParamTestComponent>();
            
            var p1 = new ProvidedParameter("p1", ParameterNamespace.Animator, o2t1, InternalPasses.Instance,
                AnimatorControllerParameterType.Bool);
            
            ParamTestComponentProvider.SetParameters(o2t1, p1);
            
            ParamTestComponentProvider.SetRemapper(o1t1, (map, _) => map.SetItem(
                (ParameterNamespace.Animator, "p1"), new ParameterMapping("p2")
            ));
            
            ParamTestComponentProvider.SetRemapper(o2t2, (map, _) => map.SetItem(
                (ParameterNamespace.Animator, "p1"), new ParameterMapping("invalid")
            ));
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(obj1).ToList();
            Assert.AreEqual(1, parameters.Count());
            Assert.AreEqual("p2", parameters[0].EffectiveName);
            Assert.AreEqual("p1", parameters[0].OriginalName);
            
            Assert.AreEqual(ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>.Empty
                .Add((ParameterNamespace.Animator, "p1"), new ParameterMapping("p2")),
                ParameterInfo.ForUI.GetParameterRemappingsAt(o2t1, true)
            );
            
            Assert.AreEqual(ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>.Empty
                    .Add((ParameterNamespace.Animator, "p1"), new ParameterMapping("p2")),
                ParameterInfo.ForUI.GetParameterRemappingsAt(o2t2, false)
            );
            
            Assert.AreEqual(ImmutableDictionary<(ParameterNamespace, string), ParameterMapping>.Empty
                    .Add((ParameterNamespace.Animator, "p1"), new ParameterMapping("invalid")),
                ParameterInfo.ForUI.GetParameterRemappingsAt(o2t2, true)
            );
        }

        [Test]
        public void TypeMerge()
        {
            var (outParam, conflicts) =
                TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Bool);
            
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float);
            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(null, AnimatorControllerParameterType.Bool);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(null, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, false, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
            
            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
        }

        (ProvidedParameter, List<ParameterInfo.ConflictType>) TypeMerge(
            AnimatorControllerParameterType? ty1,
            AnimatorControllerParameterType? ty2,
            bool animOnly1 = false,
            bool animOnly2 = false
        )
        {
            var av = CreateRoot("avatar");
            
            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            
            var obj1 = CreateChild(av, "obj1");
            var obj2 = CreateChild(av, "obj2");
            
            var t1 = obj1.AddComponent<ParamTestComponent>();
            var t2 = obj2.AddComponent<ParamTestComponent>();
            
            var p1 = new ProvidedParameter("p1", ParameterNamespace.Animator, t1, InternalPasses.Instance, ty1);
            p1.IsAnimatorOnly = animOnly1;
            var p2 = new ProvidedParameter("p1", ParameterNamespace.Animator, t2, InternalPasses.Instance, ty2);
            p2.IsAnimatorOnly = animOnly2;
            
            ParamTestComponentProvider.SetParameters(t1, p1);
            ParamTestComponentProvider.SetParameters(t2, p2);

            List<ParameterInfo.ConflictType> conflicts = new List<ParameterInfo.ConflictType>();
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(av, (ct, _1, _2) => conflicts.Add(ct)).ToList();
            
            Assert.AreEqual(1, parameters.Count());

            return (parameters[0], conflicts);
        }

        [Test]
        public void ForBuildContext()
        {
            var av = CreateRoot("avatar");
            
            var obj = CreateChild(av, "obj");
            var tc = obj.AddComponent<ParamTestComponent>();

            var ctx = CreateContext(av);
            
            ParamTestComponentProvider.SetParameters(tc, (context) =>
            {
                Assert.AreSame(ctx, context);
                return Array.Empty<ProvidedParameter>();
            });
            
            ParameterInfo.ForContext(ctx).GetParametersForObject(av).ToList();
        }
    }
}

#endif