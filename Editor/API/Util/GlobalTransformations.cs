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
            Dictionary<string, AnimatorControllerParameterType> parameterTypes = new();

            foreach (var controller in asc.ControllerContext.GetAllControllers())
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

            foreach (var controller in asc.ControllerContext.GetAllControllers())
            {
                foreach (var (name, acp) in controller.Parameters)
                {
                    acp.type = parameterTypes[name];
                }

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
                            throw new NotImplementedException("Unreachable code reached???");
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