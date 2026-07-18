#nullable enable

#region

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.platform;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    internal enum BuildStepKind
    {
        DeactivateExtension,
        ActivateExtension,
        ExecutePass,
        Complete
    }

    internal sealed class BuildStep
    {
        internal int Index { get; }
        internal string PhaseName { get; }
        internal BuildStepKind Kind { get; }
        internal ConcretePass? Pass { get; }
        internal Type? ExtensionType { get; }

        internal string PluginName => Pass?.Plugin.DisplayName ?? "NDMF";
        internal string PluginQualifiedName => Pass?.Plugin.QualifiedName ?? "NDMF";
        internal bool IsSkipped => Pass?.Skipped == true;

        internal string DisplayName => Kind switch
        {
            BuildStepKind.DeactivateExtension => $"Deactivate {ExtensionType!.FullName}",
            BuildStepKind.ActivateExtension => $"Activate {ExtensionType!.FullName}",
            BuildStepKind.ExecutePass => Pass!.Description,
            BuildStepKind.Complete => "Build complete",
            _ => throw new ArgumentOutOfRangeException()
        };

        internal string StructuralKey
        {
            get
            {
                if (Kind == BuildStepKind.Complete) return "complete";

                return string.Join("|", PhaseName, Pass!.InstantiatedPass.QualifiedName, Kind.ToString(),
                    ExtensionType?.AssemblyQualifiedName ?? "");
            }
        }

        private BuildStep(
            int index,
            string phaseName,
            BuildStepKind kind,
            ConcretePass? pass,
            Type? extensionType
        )
        {
            Index = index;
            PhaseName = phaseName;
            Kind = kind;
            Pass = pass;
            ExtensionType = extensionType;
        }

        internal static BuildStep ForExtension(
            int index,
            BuildPhase phase,
            BuildStepKind kind,
            ConcretePass pass,
            Type extensionType
        )
        {
            return new BuildStep(index, phase.Name, kind, pass, extensionType);
        }

        internal static BuildStep ForPass(int index, BuildPhase phase, ConcretePass pass)
        {
            return new BuildStep(index, phase.Name, BuildStepKind.ExecutePass, pass, null);
        }

        internal static BuildStep Complete(int index)
        {
            return new BuildStep(index, "Complete", BuildStepKind.Complete, null, null);
        }
    }

    internal sealed class BuildStepPlan
    {
        internal ImmutableList<BuildStep> Steps { get; }
        internal int CompleteStepIndex => Steps.Count - 1;

        internal BuildStepPlan(PluginResolver resolver)
        {
            var steps = new List<BuildStep>();

            foreach (var (phase, passes) in resolver.Passes)
            {
                foreach (var pass in passes)
                {
                    foreach (var extensionType in pass.DeactivatePlugins)
                    {
                        steps.Add(BuildStep.ForExtension(
                            steps.Count,
                            phase,
                            BuildStepKind.DeactivateExtension,
                            pass,
                            extensionType
                        ));
                    }

                    foreach (var extensionType in pass.ActivatePlugins)
                    {
                        steps.Add(BuildStep.ForExtension(
                            steps.Count,
                            phase,
                            BuildStepKind.ActivateExtension,
                            pass,
                            extensionType
                        ));
                    }

                    steps.Add(BuildStep.ForPass(steps.Count, phase, pass));
                }
            }

            steps.Add(BuildStep.Complete(steps.Count));
            Steps = steps.ToImmutableList();
        }

        internal static BuildStepPlan Resolve(INDMFPlatformProvider platform)
        {
            return new BuildStepPlan(new PluginResolver(platform));
        }
    }

    internal sealed class BuildStepGroup
    {
        internal string PhaseName { get; }
        internal string PluginName { get; }
        internal string PluginQualifiedName { get; }
        internal ImmutableList<BuildStep> Steps { get; }
        internal bool IsFoldout => Steps.Count > 1;

        internal BuildStepGroup(string phaseName, string pluginName, IEnumerable<BuildStep> steps)
        {
            PhaseName = phaseName;
            PluginName = pluginName;
            Steps = steps.ToImmutableList();
            PluginQualifiedName = Steps[0].PluginQualifiedName;
        }
    }

    internal static class BuildStepGrouping
    {
        internal static ImmutableList<BuildStepGroup> Group(IReadOnlyList<BuildStep> steps)
        {
            var matchingTransition = FindMatchingTransitions(steps);
            var groups = ImmutableList.CreateBuilder<BuildStepGroup>();

            var start = 0;
            while (start < steps.Count)
            {
                var first = steps[start];
                if (first.Kind == BuildStepKind.Complete)
                {
                    groups.Add(new BuildStepGroup(first.PhaseName, first.PluginName, new[] { first }));
                    start++;
                    continue;
                }

                var phaseName = first.PhaseName;
                var pluginName = first.PluginName;
                var pluginQualifiedName = first.PluginQualifiedName;
                var end = start;
                while (end + 1 < steps.Count &&
                       steps[end + 1].Kind != BuildStepKind.Complete &&
                       steps[end + 1].PhaseName == phaseName &&
                       steps[end + 1].PluginQualifiedName == pluginQualifiedName)
                {
                    end++;
                }

                // A context activated by this plugin may be torn down in the transition block
                // immediately preceding the next plugin. Keep that adjacent teardown with this group.
                var transitionIndex = end + 1;
                var lastAdjacentDeactivation = -1;
                while (transitionIndex < steps.Count &&
                       steps[transitionIndex].PhaseName == phaseName &&
                       steps[transitionIndex].Kind == BuildStepKind.DeactivateExtension)
                {
                    var activationIndex = matchingTransition[transitionIndex];
                    if (activationIndex >= start && activationIndex <= end)
                    {
                        lastAdjacentDeactivation = transitionIndex;
                    }

                    transitionIndex++;
                }

                if (lastAdjacentDeactivation >= 0)
                {
                    end = lastAdjacentDeactivation;
                }

                var groupStart = start;
                for (var index = start + 1; index <= end; index++)
                {
                    var step = steps[index];
                    var counterpart = matchingTransition[index];
                    var crossesStart = step.Kind == BuildStepKind.DeactivateExtension &&
                                       counterpart < groupStart;
                    var crossesEnd = step.Kind == BuildStepKind.ActivateExtension &&
                                     (counterpart < 0 || counterpart > end);

                    if (!crossesStart && !crossesEnd) continue;

                    groups.Add(CreateGroup(steps, groupStart, index - 1, phaseName, pluginName));
                    groupStart = index;
                }

                groups.Add(CreateGroup(steps, groupStart, end, phaseName, pluginName));
                start = end + 1;
            }

            return groups.ToImmutable();
        }

        private static BuildStepGroup CreateGroup(
            IReadOnlyList<BuildStep> steps,
            int start,
            int end,
            string phaseName,
            string pluginName
        )
        {
            var groupSteps = new List<BuildStep>(end - start + 1);
            for (var index = start; index <= end; index++)
            {
                groupSteps.Add(steps[index]);
            }

            return new BuildStepGroup(phaseName, pluginName, groupSteps);
        }

        private static int[] FindMatchingTransitions(IReadOnlyList<BuildStep> steps)
        {
            var matchingTransition = Enumerable.Repeat(-1, steps.Count).ToArray();
            var activeContexts = new Dictionary<Type, Stack<int>>();

            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                if (step.ExtensionType == null) continue;

                if (step.Kind == BuildStepKind.ActivateExtension)
                {
                    if (!activeContexts.TryGetValue(step.ExtensionType, out var activations))
                    {
                        activations = new Stack<int>();
                        activeContexts.Add(step.ExtensionType, activations);
                    }

                    activations.Push(index);
                }
                else if (step.Kind == BuildStepKind.DeactivateExtension &&
                         activeContexts.TryGetValue(step.ExtensionType, out var activations) &&
                         activations.Count > 0)
                {
                    var activationIndex = activations.Pop();
                    matchingTransition[activationIndex] = index;
                    matchingTransition[index] = activationIndex;
                }
            }

            return matchingTransition;
        }
    }

    [Serializable]
    internal sealed class BuildStepBookmark
    {
        [SerializeField] private bool _isSet;
        [SerializeField] private string _structuralKey = "";
        [SerializeField] private BuildStepKind _kind;
        [SerializeField] private int _originalIndex = -1;

        internal bool IsSet => _isSet;
        internal int OriginalIndex => _originalIndex;

        internal void Set(BuildStep step)
        {
            _isSet = true;
            _structuralKey = step.StructuralKey;
            _kind = step.Kind;
            _originalIndex = step.Index;
        }

        internal void Clear()
        {
            _isSet = false;
            _structuralKey = "";
            _originalIndex = -1;
        }

        internal bool TryResolve(BuildStepPlan plan, out int index)
        {
            index = -1;
            if (!_isSet) return false;

            var matches = plan.Steps
                .Where(step => step.StructuralKey == _structuralKey && step.Kind == _kind)
                .Select(step => step.Index)
                .ToList();

            if (matches.Count == 1)
            {
                index = matches[0];
                return true;
            }

            if (matches.Count > 1 && matches.Contains(_originalIndex))
            {
                index = _originalIndex;
                return true;
            }

            return false;
        }
    }
}