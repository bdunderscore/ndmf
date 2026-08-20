using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.util;
using NUnit.Framework;
using UnitTests;
using UnityEditor.Animations;
using UnityEngine;

public class HarmonizeTransitionsTest : TestBase
{
    [Test]
    public void BlendTreeParametersAreConvertedToFloat()
    {
        var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
        var vac = VirtualAnimatorController.Create(cloneContext);
        vac.Parameters = ImmutableDictionary.Create<string, AnimatorControllerParameter>()
            .Add("oneD", Parameter("oneD", AnimatorControllerParameterType.Bool, defaultBool: true))
            .Add("twoDX", Parameter("twoDX", AnimatorControllerParameterType.Int, defaultInt: 3))
            .Add("twoDY", Parameter("twoDY", AnimatorControllerParameterType.Bool))
            .Add("direct", Parameter("direct", AnimatorControllerParameterType.Int));

        var oneD = VirtualBlendTree.Create("1D");
        oneD.BlendType = BlendTreeType.Simple1D;
        oneD.BlendParameter = "oneD";

        var twoD = VirtualBlendTree.Create("2D");
        twoD.BlendType = BlendTreeType.FreeformCartesian2D;
        twoD.BlendParameter = "twoDX";
        twoD.BlendParameterY = "twoDY";

        var root = VirtualBlendTree.Create("Direct");
        root.BlendType = BlendTreeType.Direct;
        root.Children = root.Children
            .Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = oneD,
                DirectBlendParameter = "direct"
            })
            .Add(new VirtualBlendTree.VirtualChildMotion
            {
                Motion = twoD,
                DirectBlendParameter = "direct"
            });

        vac.AddLayer(LayerPriority.Default, "Blend Trees").StateMachine!.AddState("State", motion: root);

        GlobalTransformations.HarmonizeParameterTypes(new List<VirtualAnimatorController> { vac });

        Assert.AreEqual(AnimatorControllerParameterType.Float, vac.Parameters["oneD"].type);
        Assert.AreEqual(AnimatorControllerParameterType.Float, vac.Parameters["twoDX"].type);
        Assert.AreEqual(AnimatorControllerParameterType.Float, vac.Parameters["twoDY"].type);
        Assert.AreEqual(AnimatorControllerParameterType.Float, vac.Parameters["direct"].type);
        Assert.AreEqual(1, vac.Parameters["oneD"].defaultFloat);
        Assert.AreEqual(3, vac.Parameters["twoDX"].defaultFloat);
    }

    [Test]
    public void BlendTreeParameterTypeIsAppliedBeforeTransitionCorrection()
    {
        var controller = LoadAsset<AnimatorController>("test_harmonize.controller");
        var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
        var vac = cloneContext.Clone(controller);

        vac.Parameters = ImmutableDictionary.Create<string, AnimatorControllerParameter>()
            .Add("test", Parameter("test", AnimatorControllerParameterType.Bool));

        var blendTree = VirtualBlendTree.Create("1D");
        blendTree.BlendType = BlendTreeType.Simple1D;
        blendTree.BlendParameter = "test";
        vac.Layers.First().StateMachine!.DefaultState!.Motion = blendTree;

        GlobalTransformations.HarmonizeParameterTypes(new List<VirtualAnimatorController> { vac });

        Assert.AreEqual(AnimatorControllerParameterType.Float, vac.Parameters["test"].type);
        foreach (var condition in vac.Layers.First().StateMachine!.DefaultState!.Transitions
                     .SelectMany(transition => transition.Conditions))
        {
            Assert.That(condition.mode,
                Is.EqualTo(AnimatorConditionMode.Greater).Or.EqualTo(AnimatorConditionMode.Less));
        }
    }

    [Test]
    public void TestBoolTransitionAdjustments()
    {
        var controller = LoadAsset<AnimatorController>("test_harmonize.controller");
        var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
        var vac = cloneContext.Clone(controller);
        
        vac.Parameters = ImmutableDictionary.Create<string, AnimatorControllerParameter>().Add("test", new() {
            name = "test",
            type = AnimatorControllerParameterType.Bool,
        });
        
        GlobalTransformations.HarmonizeParameterTypes(new List<VirtualAnimatorController>() { vac });

        foreach (var t in vac.Layers.First().StateMachine!.DefaultState!.Transitions)
        {
            switch (t.DestinationState!.Name.Split("_")[0])
            {
                case "NEVER":
                    Assert.Fail("Transition to NEVER should not exist");
                    break;
                case "ALWAYS":
                    Assert.AreEqual(0, t.Conditions.Count);
                    break;
                case "IF":
                    Assert.AreEqual(1, t.Conditions.Count);
                    Assert.AreEqual(AnimatorConditionMode.If, t.Conditions[0].mode);
                    break;
                case "IFNOT":
                    Assert.AreEqual(1, t.Conditions.Count);
                    Assert.AreEqual(AnimatorConditionMode.IfNot, t.Conditions[0].mode);
                    break;
                default:
                    Assert.Fail("Unknown state name: " + t.DestinationState.Name);
                    break;
            }
        }
    }

    private static AnimatorControllerParameter Parameter(
        string name,
        AnimatorControllerParameterType type,
        bool defaultBool = false,
        int defaultInt = 0
    )
    {
        return new AnimatorControllerParameter
        {
            name = name,
            type = type,
            defaultBool = defaultBool,
            defaultInt = defaultInt
        };
    }
}
