#nullable enable

using System.Collections.Generic;
using nadena.dev.ndmf;
using NUnit.Framework;

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
    }
}
