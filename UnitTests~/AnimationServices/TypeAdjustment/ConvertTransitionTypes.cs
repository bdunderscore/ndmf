using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.util;
using NUnit.Framework;
using UnitTests;
using UnityEditor.Animations;
using UnityEngine;

public class ConvertTransitionTypes : TestBase
{
    private AnimatorController MergeAnimators(params string[] controllers)
    {
        var cloneContext = new CloneContext(GenericPlatformAnimatorBindings.Instance);
        var vac = VirtualAnimatorController.Create(cloneContext);
        
        foreach (var controller in controllers)
        {
            var ac = LoadAsset<AnimatorController>(controller + ".controller");
            var cloned = cloneContext.CloneDistinct(ac);
            foreach (var l in cloned.Layers)
            {
                vac.AddLayer(LayerPriority.Default, l);
            }

            foreach ((var k, var v) in cloned.Parameters)
            {
                if (vac.Parameters.TryGetValue(k, out var existing))
                {
                    if (existing.type != v.type)
                    {
                        existing = new AnimatorControllerParameter()
                        {
                            name = existing.name,
                            type = AnimatorControllerParameterType.Float,
                        };
                        vac.Parameters = vac.Parameters.SetItem(k, existing);
                    }
                }
                else
                {
                    vac.Parameters = vac.Parameters.Add(k, v);
                }
            }
        }

        var root = CreateRoot("root");
        var context = CreateContext(root);
        var asc = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
        asc.ControllerContext.Controllers[1] = vac;
        
        asc.HarmonizeParameterTypes();
        
        var commitContext = new CommitContext();
        return commitContext.CommitObject(vac);
    }
    
    [Test]
    public void IntConversions()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "int transitions");
        AssertTransitions(layer, "int", "gt1", 0, ("int", AnimatorConditionMode.Greater, 1));
        AssertTransitions(layer, "int", "lt1", 0, ("int", AnimatorConditionMode.Less, 1));
        AssertTransitions(layer, "int", "eq1", 0,
            ("int", AnimatorConditionMode.Greater, 0.9f),
            ("int", AnimatorConditionMode.Less, 1.1f)
        );
        AssertTransitions(layer, "int", "ne1", 0, ("int", AnimatorConditionMode.Greater, 1.1f));
        AssertTransitions(layer, "int", "ne1", 1, ("int", AnimatorConditionMode.Less, 0.9f));
        AssertTransitions(layer, "int", "ne_multi", 0,
            ("int", AnimatorConditionMode.Greater, 1.1f),
            ("int2", AnimatorConditionMode.Greater, 2.1f)
        );
        AssertTransitions(layer, "int", "ne_multi", 1,
            ("int", AnimatorConditionMode.Greater, 1.1f),
            ("int2", AnimatorConditionMode.Less, 1.9f)
        );
        AssertTransitions(layer, "int", "ne_multi", 2,
            ("int", AnimatorConditionMode.Less, 0.9f),
            ("int2", AnimatorConditionMode.Greater, 2.1f)
        );
        AssertTransitions(layer, "int", "ne_multi", 3,
            ("int", AnimatorConditionMode.Less, 0.9f),
            ("int2", AnimatorConditionMode.Less, 1.9f)
        );
    }

    [Test]
    public void BoolConversions()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "bool transitions");
        AssertTransitions(layer, "bool", "true", 0, ("bool", AnimatorConditionMode.Greater, 0.5f));
        AssertTransitions(layer, "bool", "false", 0, ("bool", AnimatorConditionMode.Less, 0.5f));
    }

    [Test]
    public void FloatUnchanged()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "float transitions");
        AssertTransitions(layer, "float", "gt", 0, ("float", AnimatorConditionMode.Greater, 123));
        AssertTransitions(layer, "float", "lt", 0, ("float", AnimatorConditionMode.Less, 123));
    }

    [Test]
    public void AnyState()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "anystate");
        var anyStateTransitions = layer.stateMachine.anyStateTransitions;

        AssertSingleTransition(anyStateTransitions[0], ("int", AnimatorConditionMode.Greater, 0.1f));
        AssertSingleTransition(anyStateTransitions[1], ("int", AnimatorConditionMode.Less, -0.1f));
    }
    
    
    [Test]
    public void Entry()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "entry");
        var transitions = layer.stateMachine.entryTransitions;

        AssertSingleTransition(transitions[0], ("int", AnimatorConditionMode.Greater, 0.1f));
        AssertSingleTransition(transitions[1], ("int", AnimatorConditionMode.Less, -0.1f));
    }

    [Test]
    public void PreservesTransitionConfig()
    {
        var ac = MergeAnimators("ac1", "ac2");

        var layer = ac.layers.First(l => l.name == "preserve_config");

        var state = FindStateInLayer(layer, "foo");
        Assert.AreEqual(123, layer.stateMachine.anyStateTransitions[0].duration);
        Assert.AreEqual(123, state.transitions[0].exitTime);
    }

    [Test]
    public void ConversionWhenInconsistent()
    {
        var fx = MergeAnimators("ac1", "ac2");

        var p_types = fx.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int3"]);
        Assert.AreEqual(AnimatorControllerParameterType.Trigger, p_types["trigger"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["bool"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["int"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["float"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["int2"]);
    }

    [Test]
    public void SubStateMachineHandling()
    {
        var fx = MergeAnimators("ac1", "ac2");

        var layer = fx.layers.First(l => l.name == "sub_state_machine");
        
        AssertSingleTransition(layer.stateMachine.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));

        var ssm1 = layer.stateMachine.stateMachines[0].stateMachine;
        AssertSingleTransition(ssm1.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));
        
        var ssm2 = ssm1.stateMachines[0].stateMachine;
        AssertSingleTransition(ssm2.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));
    }
    
    [Test]
    public void NoConversionWhenConsistent()
    {
        var fx = MergeAnimators("ac1", "ac1");

        var p_types = fx.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int3"]);
        Assert.AreEqual(AnimatorControllerParameterType.Trigger, p_types["trigger"]);
        Assert.AreEqual(AnimatorControllerParameterType.Bool, p_types["bool"]);
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["float"]);
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int2"]);
        
        var layer = fx.layers.First(l => l.name == "int transitions");
        AssertTransitions(layer, "int", "eq1", 0, ("int", AnimatorConditionMode.Equals, 1f));
    }

    [Test]
    public void SubStateMachineExitTransitions()
    {
        var fx = MergeAnimators("ac1", "ac2");
        
        var layer = fx.layers.First(l => l.name == "sub_state_machine");

        var rootStateMachine = layer.stateMachine;
        var ssm1 = layer.stateMachine.stateMachines[0].stateMachine;
        var exitTransitions = rootStateMachine.GetStateMachineTransitions(ssm1);
        
        AssertSingleTransition(exitTransitions[0], ("int", AnimatorConditionMode.Greater, 0.1f));
        AssertSingleTransition(exitTransitions[1], ("int", AnimatorConditionMode.Less, -0.1f));
    }

    [Test]
    public void CrossLayerTypeConsistency()
    {
        var root = CreateRoot("root");
        var context = CreateContext(root);
        var asc = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();

        VirtualAnimatorController value = asc.ControllerContext.Clone(LoadAsset<AnimatorController>("cltc_0.controller"));
        asc.ControllerContext.Controllers[1] = value;
        VirtualAnimatorController value1 = asc.ControllerContext.Clone(LoadAsset<AnimatorController>("cltc_1.controller"));
        asc.ControllerContext.Controllers[2] = value1;
        
        asc.HarmonizeParameterTypes();

        var fx = asc.ControllerContext.Controllers[1];
        
        var fx_types = fx.Parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.Key, p.Value.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Float, fx_types["bool"]);
        
        var fx_layer = new CommitContext().CommitObject(fx.Layers.First(l => l.Name == "l"));
        AssertSingleTransition(fx_layer.stateMachine.anyStateTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));

        var action = asc.ControllerContext.Controllers[2];
        
        var action_types = action.Parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.Key, p.Value.type)
        ).ToImmutableDictionary();
        Assert.AreEqual(AnimatorControllerParameterType.Float, action_types["bool"]);

        var action_layer = new CommitContext().CommitObject(action.Layers.First(l => l.Name == "l"));
        AssertSingleTransition(action_layer.stateMachine.anyStateTransitions[0], ("bool", AnimatorConditionMode.Greater, 0));
    }
    
    void AssertTransitions(AnimatorControllerLayer layer, string src, string dest, int index,
        params (string, AnimatorConditionMode, float)[] conditions)
    {
        var srcState = FindStateInLayer(layer, src);

        var transitions = srcState.transitions.Where(t2 => t2.destinationState.name == dest)
            .ToArray();
        var t = transitions[index];
        
        AssertSingleTransition(t, conditions);
    }

    private static void AssertSingleTransition<T>(T t,
        params (string, AnimatorConditionMode, float)[] conditions
    ) where T: AnimatorTransitionBase
    {
        Assert.AreEqual(t.conditions.Length, conditions.Length);

        for (int i = 0; i < conditions.Length; i++)
        {
            var t_cond = t.conditions[i];
            var (e_param, e_mode, e_thresh) = conditions[i];
            
            Assert.AreEqual(e_param, t_cond.parameter);
            Assert.AreEqual(e_mode, t_cond.mode);
            Assert.Less(Mathf.Abs(t_cond.threshold - e_thresh), 0.001f);
        }
    }
}