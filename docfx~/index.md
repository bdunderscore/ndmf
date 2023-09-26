# NDM Framework

NDM Framework ("Nademof" for short) is a framework for running non-destructive build plugins when building avatars for
VRChat (and, eventually, for other VRSNS platforms).

## Why is this needed?

While the VRChat SDK has support for running callbacks at build time, it does not provide support for running callbacks
when entering play mode. If each plugin were to develop its own logic for running in play mode, this would lead to
compatibility issues (and, in fact, this has happened already!)

NDM Framework's primary goal is to improve compatibility when multiple nondestructive plugins are loaded. It also aims
to make writing nondestructive build plugins easier.

## How do I get started?

Getting started is easy; a simple nondestructive plugin can look like this:

```csharp

[assembly: ExportsPlugin(typeof(MyPlugin))]

public class MyPlugin : Plugin<MyPlugin>
{
    public override string DisplayName => "Baby's first plugin";

    protected override void Configure()
    {
        InPhase(BuildPhase.Transforming).Run("Do the thing", ctx =>
        {
            Debug.Log("Hello world!");
        });
    }
}

```

NDM Framework supports more advanced features such as dependency ordering, but this is enough to get started.

For more information, see the [API documentation](api/index.html) or the other articles linked from the sidebar.

## Support model

NDMF is currently in an alpha state. The API is not fully stable yet - hopefully soon, though!
