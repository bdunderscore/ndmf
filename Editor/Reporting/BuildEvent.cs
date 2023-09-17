#region

using System;
using System.Collections.Immutable;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.reporting
{
    internal class BuildEvent
    {
        internal BuildEvent()
        {
        }

        public sealed class BuildStarted : BuildEvent
        {
            public GameObject AvatarRoot { get; }
            public string AvatarRootName { get; }

            public BuildStarted(GameObject avatarRoot)
            {
                AvatarRoot = avatarRoot;
                AvatarRootName = avatarRoot.name;
            }
        }

        public sealed class BuildEnded : BuildEvent
        {
            public double ElapsedTimeMS { get; }
            public bool Successful { get; }

            public BuildEnded(double elapsedTimeMs, bool successful)
            {
                ElapsedTimeMS = elapsedTimeMs;
                Successful = successful;
            }
        }

        public sealed class PassExecuted : BuildEvent
        {
            public string QualifiedName { get; }
            public double PassExecutionTime { get; }

            public ImmutableDictionary<Type, double> PassActivationTimes { get; }
            public ImmutableDictionary<Type, double> PassDeactivationTimes { get; }

            public PassExecuted(string qualifiedName, double passExecutionTime,
                ImmutableDictionary<Type, double> passActivationTimes,
                ImmutableDictionary<Type, double> passDeactivationTimes)
            {
                QualifiedName = qualifiedName;
                PassExecutionTime = passExecutionTime;
                PassActivationTimes = passActivationTimes;
                PassDeactivationTimes = passDeactivationTimes;
            }
        }

        public delegate void OnEventDelegate(BuildEvent buildEvent);

        public static event OnEventDelegate OnBuildEvent;

        public static ImmutableList<BuildEvent> LastBuildEvents { get; private set; } = ImmutableList<BuildEvent>.Empty;

        internal static void Dispatch(BuildEvent buildEvent)
        {
            OnBuildEvent?.Invoke(buildEvent);
            if (buildEvent is BuildStarted)
            {
                LastBuildEvents = ImmutableList<BuildEvent>.Empty;
            }

            LastBuildEvents = LastBuildEvents.Add(buildEvent);
        }
    }
}