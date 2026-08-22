#nullable enable

using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.reporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnitTests
{
    internal sealed class NDMF0003RequiredContext : IExtensionContext
    {
        internal static readonly List<string> DeactivationOrder = new();

        public void OnActivate(BuildContext context)
        {
        }

        public void OnDeactivate(BuildContext context)
        {
            DeactivationOrder.Add(nameof(NDMF0003RequiredContext));
        }
    }

    [DependsOnContext(typeof(NDMF0003RequiredContext))]
    internal sealed class NDMF0003DependentContext : IExtensionContext
    {
        public void OnActivate(BuildContext context)
        {
        }

        public void OnDeactivate(BuildContext context)
        {
            NDMF0003RequiredContext.DeactivationOrder.Add(nameof(NDMF0003DependentContext));
        }
    }

    internal sealed class NDMF0015ErrorContext : IExtensionContext
    {
        private sealed class BuildError : IError
        {
            public ErrorSeverity Severity => ErrorSeverity.Error;

            public UnityEngine.UIElements.VisualElement CreateVisualElement(ErrorReport report)
            {
                return new UnityEngine.UIElements.VisualElement();
            }

            public string ToMessage()
            {
                return "NDMF-0015";
            }

            public void AddReference(ObjectReference obj)
            {
            }
        }

        public void OnActivate(BuildContext context)
        {
        }

        public void OnDeactivate(BuildContext context)
        {
            ErrorReport.ReportError(new BuildError());
        }
    }

    public class BuildContextLifecycleTests : TestBase
    {
        [Test]
        public void NDMF0003FinishDeactivatesDependentContextsBeforeTheirRequirements()
        {
            var context = new BuildContext(CreateRoot("NDMF-0003"), null);
            NDMF0003RequiredContext.DeactivationOrder.Clear();

            try
            {
                context.ActivateExtensionContextRecursive<NDMF0003DependentContext>();
                context.Finish();

                Assert.That(NDMF0003RequiredContext.DeactivationOrder, Is.EqualTo(new[]
                {
                    nameof(NDMF0003DependentContext),
                    nameof(NDMF0003RequiredContext)
                }));
            }
            finally
            {
                NDMF0003RequiredContext.DeactivationOrder.Clear();
            }
        }

        [Test]
        public void NDMF0015FinishReportsAnUnsuccessfulBuildWhenErrorsWereRecorded()
        {
            var context = new BuildContext(CreateRoot("NDMF-0015"), null);
            context.ActivateExtensionContext<NDMF0015ErrorContext>();

            context.Finish();

            var completion = BuildEvent.LastBuildEvents.OfType<BuildEvent.BuildEnded>().Single();
            Assert.That(completion.Successful, Is.False);
        }
    }
}
