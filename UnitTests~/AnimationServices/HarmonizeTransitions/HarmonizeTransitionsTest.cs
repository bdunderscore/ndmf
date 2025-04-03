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
}
