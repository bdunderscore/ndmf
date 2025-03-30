using System.Collections;
using System.Collections.Generic;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnitTests;
using UnityEngine;

public class LayersLostOnReactivation : TestBase
{
    [Test]
    public void TestLayersOverReactivation()
    {
        var prefab = CreatePrefab("LayersLostOnReactivation.prefab");

        var context = CreateContext(prefab);

        context.ActivateExtensionContext<VirtualControllerContext>();
        context.DeactivateAllExtensionContexts();
        context.ActivateExtensionContext<VirtualControllerContext>();
        context.DeactivateAllExtensionContexts();

        findFxLayer(prefab, "Base Layer");
    }
}
