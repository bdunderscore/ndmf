using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;

namespace nadena.dev.ndmf.util
{
    public static class GlobalTransformations
    {
        /// <summary>
        ///     Removes all empty layers that are safe to remove from the AnimatorServicesContext.
        /// </summary>
        /// <param name="ctx"></param>
        public static void RemoveEmptyLayers(this AnimatorServicesContext ctx)
        {
            foreach (var controller in ctx.ControllerContext.GetAllControllers())
            {
                RemoveEmptyLayers(controller);
            }
        }

        /// <summary>
        ///     Remove all empty layers that are safe to remove from the VirtualAnimatorController.
        /// </summary>
        /// <param name="vac"></param>
        public static void RemoveEmptyLayers(VirtualAnimatorController vac)
        {
            var isFirst = true;
            vac.RemoveLayers(layer =>
            {
                if (isFirst)
                {
                    isFirst = false;
                    return false;
                }

                return LayerIsEmpty(layer);
            });
        }

        private static bool LayerIsEmpty(VirtualLayer arg)
        {
            return arg.SyncedLayerIndex < 0 && (arg.StateMachine == null ||
                                                (arg.StateMachine.States.Count == 0 &&
                                                 arg.StateMachine.StateMachines.Count == 0));
        }

        /// <summary>
        ///     If different controllers in the AnimatorServicesContext have parameters with the same name but different types,
        ///     or if a condition in a transition references a parameter with the wrong type, this method will adjust the types
        ///     and transitions to use float parameters where necessary.
        /// </summary>
        /// <param name="asc"></param>
        public static void HarmonizeParameterTypes(this AnimatorServicesContext asc)
        {
            HarmonizeParameterTypes(asc.ControllerContext.GetAllControllers().ToList());
        }


        internal static void HarmonizeParameterTypes(List<VirtualAnimatorController> controllers)
        {
            Dictionary<string, AnimatorControllerParameterType> parameterTypes = new();

            foreach (var controller in controllers)
            {
                foreach (var (name, acp) in controller.Parameters)
                {
                    if (!parameterTypes.TryGetValue(name, out var type))
                    {
                        parameterTypes[name] = acp.type;
                    }
                    else if (type != acp.type)
                    {
                        parameterTypes[name] = AnimatorControllerParameterType.Float;
                    }
                }
            }

            // Blend trees can only consume float parameters. Record those requirements before updating
            // parameter declarations and transition conditions so both are harmonized against the final type.
            foreach (var blendTree in controllers.SelectMany(controller =>
                         controller.AllReachableNodes().OfType<VirtualBlendTree>()))
            {
                switch (blendTree.BlendType)
                {
                    case BlendTreeType.Direct:
                        foreach (var child in blendTree.Children)
                        {
                            RequireFloat(child.DirectBlendParameter);
                        }

                        break;
                    case BlendTreeType.Simple1D:
                        RequireFloat(blendTree.BlendParameter);
                        break;
                    case BlendTreeType.SimpleDirectional2D:
                    case BlendTreeType.FreeformDirectional2D:
                    case BlendTreeType.FreeformCartesian2D:
                        RequireFloat(blendTree.BlendParameter);
                        RequireFloat(blendTree.BlendParameterY);
                        break;
                }
            }

            foreach (var controller in controllers)
            {
                var newParams = controller.Parameters;
                foreach (var (name, acp) in controller.Parameters)
                {
                    var newType = parameterTypes[name];
                    var newAcp = new AnimatorControllerParameter
                    {
                        name = acp.name,
                        defaultBool = acp.defaultBool,
                        defaultFloat = newType == AnimatorControllerParameterType.Float
                            ? acp.type switch
                            {
                                AnimatorControllerParameterType.Bool => acp.defaultBool ? 1 : 0,
                                AnimatorControllerParameterType.Int => acp.defaultInt,
                                _ => acp.defaultFloat
                            }
                            : acp.defaultFloat,
                        defaultInt = acp.defaultInt,
                        type = newType
                    };
                    newParams = newParams.SetItem(name, newAcp);
                }

                controller.Parameters = newParams;

                foreach (var node in controller.AllReachableNodes())
                {
                    if (node is VirtualState s)
                    {
                        HarmonizeTransitions(s, parameterTypes);
                    }
                    else if (node is VirtualStateMachine vsm)
                    {
                        HarmonizeStateMachine(vsm, parameterTypes);
                    }
                }
            }

            void RequireFloat(string parameterName)
            {
                if (!string.IsNullOrEmpty(parameterName))
                {
                    parameterTypes[parameterName] = AnimatorControllerParameterType.Float;
                }
            }
        }

        private static void HarmonizeTransitions(VirtualState virtualState,
            Dictionary<string, AnimatorControllerParameterType> parameterTypes)
        {
            virtualState.Transitions = HarmonizeTransitions(virtualState.Transitions, parameterTypes);
        }

        private static void HarmonizeStateMachine(VirtualStateMachine virtualStateMachine,
            Dictionary<string, AnimatorControllerParameterType> parameterTypes)
        {
            virtualStateMachine.AnyStateTransitions =
                HarmonizeTransitions(virtualStateMachine.AnyStateTransitions, parameterTypes);
            virtualStateMachine.EntryTransitions =
                HarmonizeTransitions(virtualStateMachine.EntryTransitions, parameterTypes);
            virtualStateMachine.StateMachineTransitions =
                virtualStateMachine.StateMachineTransitions.ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => HarmonizeTransitions(kvp.Value, parameterTypes)
                );
        }

        private static ImmutableList<T> HarmonizeTransitions<T>(IEnumerable<T> transitions,
            Dictionary<string, AnimatorControllerParameterType> parameterTypes)
            where T : VirtualTransitionBase
        {
            return transitions.SelectMany(Harmonize).ToImmutableList();

            IEnumerable<T> Harmonize(T transition)
            {
                if (transition.Conditions.All(c => !NeedsConversion(c)))
                {
                    return new[] { transition };
                }

                // Some conditions need to be expanded into multiple branches (specifically, the NotEqual branch).
                // As such, we construct a list, and may double its length each time we encounter a condition that needs
                // to be expanded.
                transition = (T)transition.Clone();
                var conditions = transition.Conditions;
                transition.Conditions = ImmutableList<AnimatorCondition>.Empty;
                var transitions = ImmutableList<T>.Empty.Add(transition);

                foreach (var condition in conditions)
                {
                    if (!NeedsConversion(condition))
                    {
                        foreach (var t in transitions)
                        {
                            t.Conditions = t.Conditions.Add(condition);
                        }

                        continue;
                    }

                    switch (condition.mode)
                    {
                        case AnimatorConditionMode.Greater:
                        case AnimatorConditionMode.Less:
                            if (parameterTypes[condition.parameter] != AnimatorControllerParameterType.Bool)
                            {
                                //shouldn't happen...?
                                foreach (var t in transitions)
                                {
                                    t.Conditions = t.Conditions.Add(condition);
                                }

                                break;
                            }

                            if (condition.mode == AnimatorConditionMode.Greater && condition.threshold >= 1.0f)
                            {
                                // Never satisfiable
                                return Array.Empty<T>();
                            }

                            if (condition.mode == AnimatorConditionMode.Less && condition.threshold <= 0.0f)
                            {
                                // Never satisfiable
                                return Array.Empty<T>();
                            }

                            if (condition.mode == AnimatorConditionMode.Greater && condition.threshold >= 0.0f)
                            {
                                var newCondition = condition;
                                newCondition.mode = AnimatorConditionMode.If;
                                foreach (var t in transitions)
                                {
                                    t.Conditions = t.Conditions.Add(newCondition);
                                }
                            }
                            else if (condition.mode == AnimatorConditionMode.Less && condition.threshold <= 1.0f)
                            {
                                var newCondition = condition;
                                newCondition.mode = AnimatorConditionMode.IfNot;
                                foreach (var t in transitions)
                                {
                                    t.Conditions = t.Conditions.Add(newCondition);
                                }
                            }
                            else
                            {
                                // always satisfied
                                break;
                            }

                            break;
                        case AnimatorConditionMode.Equals:
                            foreach (var t in transitions)
                            {
                                t.Conditions = t.Conditions.Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Greater,
                                    threshold = condition.threshold - 0.1f
                                }).Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Less,
                                    threshold = condition.threshold + 0.1f
                                });
                            }

                            break;
                        case AnimatorConditionMode.If:
                            foreach (var t in transitions)
                            {
                                t.Conditions = t.Conditions.Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Greater,
                                    threshold = 0.5f
                                });
                            }

                            break;
                        case AnimatorConditionMode.IfNot:
                            foreach (var t in transitions)
                            {
                                t.Conditions = t.Conditions.Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Less,
                                    threshold = 0.5f
                                });
                            }

                            break;
                        case AnimatorConditionMode.NotEqual:
                        {
                            var newTransitions = ImmutableList<T>.Empty;
                            foreach (var t in transitions)
                            {
                                var t2 = (T)t.Clone();
                                t.Conditions = t.Conditions.Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Greater,
                                    threshold = condition.threshold + 0.1f
                                });
                                t2.Conditions = t2.Conditions.Add(new AnimatorCondition
                                {
                                    parameter = condition.parameter,
                                    mode = AnimatorConditionMode.Less,
                                    threshold = condition.threshold - 0.1f
                                });
                                newTransitions = newTransitions.Add(t).Add(t2);
                            }

                            transitions = newTransitions;
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                
                return transitions;
            }

            bool NeedsConversion(AnimatorCondition ac)
            {
                if (!parameterTypes.TryGetValue(ac.parameter, out var ty)) return false;
                if (ty == AnimatorControllerParameterType.Trigger) return false; // unsupported

                return !ConditionCompatibleWithType(ac, ty);
            }
        }


        private static bool ConditionCompatibleWithType(AnimatorCondition ac, AnimatorControllerParameterType ty)
        {
            switch (ac.mode)
            {
                case AnimatorConditionMode.Equals:
                case AnimatorConditionMode.NotEqual:
                    return ty == AnimatorControllerParameterType.Int;
                case AnimatorConditionMode.Greater:
                case AnimatorConditionMode.Less:
                    return ty == AnimatorControllerParameterType.Int || ty == AnimatorControllerParameterType.Float;
                case AnimatorConditionMode.If:
                case AnimatorConditionMode.IfNot:
                    return ty == AnimatorControllerParameterType.Bool;
                default:
                    return true; // unknown condition
            }
        }
    }
}
