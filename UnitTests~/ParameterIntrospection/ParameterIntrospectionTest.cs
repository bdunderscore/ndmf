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
        public void EditorOnly()
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

            var tc2 = obj2.AddComponent<ParamTestComponent>();
            var p2 = new ProvidedParameter("p2", ParameterNamespace.Animator, tc2, InternalPasses.Instance,
                AnimatorControllerParameterType.Bool);
            ParamTestComponentProvider.SetParameters(tc2, p2);
            
            obj1.tag = "EditorOnly";
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(av).ToList();
            Assert.AreEqual(1, parameters.Count());
            Assert.AreEqual(p2, parameters[0]);
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

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, false, false, true);
            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, false, false, false, true);
            Assert.AreEqual(1, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Int, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Int, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Int, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Bool, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int, false, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, true, false, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Float, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, false, true, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);

            (outParam, conflicts) = TypeMerge(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, true, true, true, true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, outParam.ParameterType);
        }

        (ProvidedParameter, List<ParameterInfo.ConflictType>) TypeMerge(
            AnimatorControllerParameterType? ty1,
            AnimatorControllerParameterType? ty2,
            bool animOnly1 = false,
            bool animOnly2 = false,
            bool allowMismatch1 = false,
            bool allowMismatch2 = false
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
            p1.ExpandTypeOnConflict = allowMismatch1;
            var p2 = new ProvidedParameter("p1", ParameterNamespace.Animator, t2, InternalPasses.Instance, ty2);
            p2.IsAnimatorOnly = animOnly2;
            p2.ExpandTypeOnConflict = allowMismatch2;
            
            ParamTestComponentProvider.SetParameters(t1, p1);
            ParamTestComponentProvider.SetParameters(t2, p2);

            List<ParameterInfo.ConflictType> conflicts = new List<ParameterInfo.ConflictType>();
            
            var parameters = ParameterInfo.ForUI.GetParametersForObject(av, (ct, _1, _2) => conflicts.Add(ct)).ToList();
            
            Assert.AreEqual(1, parameters.Count());

            return (parameters[0], conflicts);
        }

        [Test]
        public void DefaultValueMerge()
        {
            var outParam = DefaultValueMerge(null, null);
            Assert.AreEqual(null, outParam.DefaultValue);

            outParam = DefaultValueMerge(null, 0);
            Assert.AreEqual(0, outParam.DefaultValue);

            outParam = DefaultValueMerge(0, null);
            Assert.AreEqual(0, outParam.DefaultValue);

            outParam = DefaultValueMerge(0, 0);
            Assert.AreEqual(0, outParam.DefaultValue);

            outParam = DefaultValueMerge(0, 1);
            Assert.AreEqual(0, outParam.DefaultValue);

            outParam = DefaultValueMerge(1, 0);
            Assert.AreEqual(1, outParam.DefaultValue);

            outParam = DefaultValueMerge(1, 1);
            Assert.AreEqual(1, outParam.DefaultValue);
        }

        ProvidedParameter DefaultValueMerge(
            float? defaultValueParent,
            float? defaultValueChild
        )
        {
            var av = CreateRoot("avatar");

            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var parent = CreateChild(av, "parent");
            var child = CreateChild(parent, "child");

            var tParent = parent.AddComponent<ParamTestComponent>();
            var tChild = child.AddComponent<ParamTestComponent>();

            var pParent = new ProvidedParameter("param", ParameterNamespace.Animator, tParent, InternalPasses.Instance, AnimatorControllerParameterType.Float);
            pParent.DefaultValue = defaultValueParent;
            var pChild = new ProvidedParameter("param", ParameterNamespace.Animator, tChild, InternalPasses.Instance, AnimatorControllerParameterType.Float);
            pChild.DefaultValue = defaultValueChild;

            ParamTestComponentProvider.SetParameters(tParent, pParent);
            ParamTestComponentProvider.SetParameters(tChild, pChild);

            var parameters = ParameterInfo.ForUI.GetParametersForObject(av).ToList();

            Assert.AreEqual(1, parameters.Count());

            return parameters[0];
        }

        [Test]
        public void NumericBitWidthClamp()
        {
            var p = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Int);
            Assert.AreEqual(null, p.NumericBitWidth);

            p.NumericBitWidth = 0;
            Assert.AreEqual(2, p.NumericBitWidth);

            p.NumericBitWidth = 20;
            Assert.AreEqual(8, p.NumericBitWidth);

            p.NumericBitWidth = 5;
            Assert.AreEqual(5, p.NumericBitWidth);

            p.NumericBitWidth = null;
            Assert.AreEqual(null, p.NumericBitWidth);
        }

        [Test]
        public void BitUsage()
        {
            var p = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Int);
            p.WantSynced = true;
            Assert.AreEqual(8, p.BitUsage);

            p.NumericBitWidth = 4;
            Assert.AreEqual(4, p.BitUsage);

            p.ParameterType = AnimatorControllerParameterType.Float;
            Assert.AreEqual(4, p.BitUsage);

            p.ParameterType = AnimatorControllerParameterType.Bool;
            Assert.AreEqual(1, p.BitUsage);

            p.ParameterType = AnimatorControllerParameterType.Int;
            p.WantSynced = false;
            Assert.AreEqual(0, p.BitUsage);
        }

        [Test]
        public void DefaultSignedness()
        {
            var p = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Int);
            Assert.AreEqual(ParameterSignedness.Unsigned, p.Signedness);

            p.ParameterType = AnimatorControllerParameterType.Float;
            Assert.AreEqual(ParameterSignedness.Signed, p.Signedness);

            p.Signedness = ParameterSignedness.Unsigned;
            Assert.AreEqual(ParameterSignedness.Unsigned, p.Signedness);

            p.ParameterType = AnimatorControllerParameterType.Int;
            Assert.AreEqual(ParameterSignedness.Unsigned, p.Signedness);
        }

        [Test]
        public void NumericFormatEquality()
        {
            var p1 = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Bool);
            var p2 = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Bool);
            p1.NumericBitWidth = 4;
            p1.Signedness = ParameterSignedness.Signed;
            Assert.AreEqual(p1, p2);
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());

            p1.ParameterType = AnimatorControllerParameterType.Int;
            p2.ParameterType = AnimatorControllerParameterType.Int;
            Assert.AreNotEqual(p1, p2);

            p1.NumericBitWidth = null;
            p1.StoredSignedness = null;
            p2.NumericBitWidth = 8;
            p2.Signedness = ParameterSignedness.Unsigned;
            Assert.AreEqual(p1, p2);
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());
        }

        [Test]
        public void BitWidthMerge()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.NumericBitWidth);
            Assert.AreEqual(8, outParam.BitUsage);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, 4);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(4, outParam.NumericBitWidth);
            Assert.AreEqual(4, outParam.BitUsage);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(4, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, 4);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(4, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 0, 2);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(2, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, 6);
            CollectionAssert.AreEqual(new[] { ParameterInfo.ConflictType.BitWidthMismatch }, conflicts);
            Assert.AreEqual(4, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, 6, 4);
            CollectionAssert.AreEqual(new[] { ParameterInfo.ConflictType.BitWidthMismatch }, conflicts);
            Assert.AreEqual(6, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Bool, 4, 6);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(1, outParam.BitUsage);
        }

        [Test]
        public void BitWidthMergeWithAnimatorOnly()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, 6, animOnlyParent: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(6, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, null, animOnlyParent: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, 4, 6, animOnlyChild: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(4, outParam.NumericBitWidth);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, 6, animOnlyChild: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.NumericBitWidth);
        }

        [Test]
        public void SignednessMerge()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Unsigned, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, null, ParameterSignedness.Signed);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, ParameterSignedness.Signed, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, ParameterSignedness.Signed, ParameterSignedness.Signed);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, ParameterSignedness.Signed, ParameterSignedness.Unsigned);
            CollectionAssert.AreEqual(new[] { ParameterInfo.ConflictType.SignednessMismatch }, conflicts);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Bool, null, null, ParameterSignedness.Signed, ParameterSignedness.Unsigned);
            Assert.AreEqual(0, conflicts.Count);
        }

        [Test]
        public void SignednessMergeWithAnimatorOnly()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, ParameterSignedness.Signed, ParameterSignedness.Unsigned, animOnlyParent: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Unsigned, outParam.Signedness);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Int, null, null, ParameterSignedness.Signed, ParameterSignedness.Unsigned, animOnlyChild: true);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(ParameterSignedness.Signed, outParam.Signedness);
        }

        [Test]
        public void LerpDurationClamp()
        {
            var p = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Float);
            Assert.AreEqual(null, p.LerpDuration);

            p.LerpDuration = -1f;
            Assert.AreEqual(0f, p.LerpDuration);

            p.LerpDuration = 500f;
            Assert.AreEqual(300f, p.LerpDuration);

            p.LerpDuration = 1.5f;
            Assert.AreEqual(1.5f, p.LerpDuration);

            p.LerpDuration = float.NaN;
            Assert.AreEqual(null, p.LerpDuration);

            p.LerpDuration = float.PositiveInfinity;
            Assert.AreEqual(null, p.LerpDuration);
        }

        [Test]
        public void LerpDurationEquality()
        {
            var p1 = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Bool);
            var p2 = new ProvidedParameter("param", ParameterNamespace.Animator, null, InternalPasses.Instance, AnimatorControllerParameterType.Bool);
            p1.LerpDuration = 1f;
            Assert.AreEqual(p1, p2);
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());

            p1.ParameterType = AnimatorControllerParameterType.Float;
            p2.ParameterType = AnimatorControllerParameterType.Float;
            Assert.AreNotEqual(p1, p2);

            p2.LerpDuration = 1f;
            Assert.AreEqual(p1, p2);
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());
        }

        [Test]
        public void LerpDurationMerge()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, lerpDurationChild: 1f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(1f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, lerpDurationParent: 1f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(1f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, lerpDurationParent: 1f, lerpDurationChild: 1f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(1f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, lerpDurationParent: 300f, lerpDurationChild: 500f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(300f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, lerpDurationParent: 1f, lerpDurationChild: 2f);
            CollectionAssert.AreEqual(new[] { ParameterInfo.ConflictType.LerpDurationMismatch }, conflicts);
            Assert.AreEqual(1f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Bool, null, null, lerpDurationParent: 1f, lerpDurationChild: 2f);
            Assert.AreEqual(0, conflicts.Count);
        }

        [Test]
        public void LerpDurationMergeWithAnimatorOnly()
        {
            var (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, animOnlyParent: true, lerpDurationParent: 1f, lerpDurationChild: 2f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(2f, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, animOnlyParent: true, lerpDurationParent: 1f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(null, outParam.LerpDuration);

            (outParam, conflicts) = NumericFormatMerge(AnimatorControllerParameterType.Float, null, null, animOnlyChild: true, lerpDurationParent: 1f, lerpDurationChild: 2f);
            Assert.AreEqual(0, conflicts.Count);
            Assert.AreEqual(1f, outParam.LerpDuration);
        }

        (ProvidedParameter, List<ParameterInfo.ConflictType>) NumericFormatMerge(
            AnimatorControllerParameterType type,
            int? bitWidthParent,
            int? bitWidthChild,
            ParameterSignedness? signednessParent = null,
            ParameterSignedness? signednessChild = null,
            bool animOnlyParent = false,
            bool animOnlyChild = false,
            float? lerpDurationParent = null,
            float? lerpDurationChild = null
        )
        {
            var av = CreateRoot("avatar");

            var desc = av.GetComponent<VRCAvatarDescriptor>();
            desc.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();

            var parent = CreateChild(av, "parent");
            var child = CreateChild(parent, "child");

            var tParent = parent.AddComponent<ParamTestComponent>();
            var tChild = child.AddComponent<ParamTestComponent>();

            var pParent = new ProvidedParameter("param", ParameterNamespace.Animator, tParent, InternalPasses.Instance, type);
            pParent.IsAnimatorOnly = animOnlyParent;
            pParent.WantSynced = true;
            pParent.NumericBitWidth = bitWidthParent;
            pParent.StoredSignedness = signednessParent;
            pParent.LerpDuration = lerpDurationParent;
            var pChild = new ProvidedParameter("param", ParameterNamespace.Animator, tChild, InternalPasses.Instance, type);
            pChild.IsAnimatorOnly = animOnlyChild;
            pChild.WantSynced = true;
            pChild.NumericBitWidth = bitWidthChild;
            pChild.StoredSignedness = signednessChild;
            pChild.LerpDuration = lerpDurationChild;

            ParamTestComponentProvider.SetParameters(tParent, pParent);
            ParamTestComponentProvider.SetParameters(tChild, pChild);

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