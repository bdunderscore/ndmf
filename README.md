![GitHub release (with filter)](https://img.shields.io/github/v/release/bdunderscore/ndmf)
![GitHub release (by tag)](https://img.shields.io/github/downloads/bdunderscore/ndmf/latest/total)
![GitHub all releases](https://img.shields.io/github/downloads/bdunderscore/ndmf/total?label=total%20downloads)
[![Documentation](https://img.shields.io/badge/docs-latest-blue)](https://ndmf.nadena.dev)

# Non-Destructive Modular Framework ("なでもふ")

This package is a framework for building non-destructive editor plugins for VRChat avatars. It provides the following facilities:

* Plugin execution sequencing based on high level phases, and explicit runs-before/runs-after relationships between processing passes
* Support for applying transformations when entering play mode
* Support for applying transformations when building avatars
* Support for saving temporary/generated assets, and cleaning up those assets once the avatar build is completed.
* Support for adjusting animation paths after objects are moved

Future plans include:
* Support for caching generated assets across subsequent builds
* Support for platforms other than VRChat.


## Getting started

You can find detailed information in [the documentation](https://ndmf.nadena.dev).

To get started quickly, a minimal plugin definition looks a bit like this:

```csharp
[assembly: ExportsPlugin(typeof(MyPlugin))]

namespace nadena.dev.ndmf.sample
{
    public class MyPlugin : Plugin<MyPlugin>
    {
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming).Run("Do something", ctx => { /* ... */ });
        }
    }
}
```

You can see a functional example here: https://github.com/bdunderscore/ndmf/blob/main/Editor/Samples~/SetViewpointPlugin.cs

## Execution model

NDMF models execution using "Plugins" and "Passes". A plugin is meant to be an end-user-visible extension, such as Modular Avatar or AAO, while a pass is an internal step in the execution of that plugin. Breaking your execution into smaller passes allows better control of the order of execution between passes.

Passes are grouped into execution phases, which execute in the following order:
* Resolving - This is intended to run before any editor extensions modify the avatar, and is useful for rehydrating components with serialized state that need to refer to the pre-transformation avatar (e.g. if you have a path serialized to a string which you need to resolve to an object before objects start moving around)
* Generating - This is intended to run before editor extensions which primarily generate new objects and components for use by other systems.
* Transforming - This is intended as the "general-purpose" execution phase, where most extensions which transform avatars run.
* Optimizing - This is intended as an execution phase for optimization plugins which aren't intended to modify the avatar in a semantically-meaningful way.

Within each phase, passes are always executed in the order in which they are declared in the plugin definition. However, depending on dependency declarations, passes from other plugins can be injected between your passes.

### Dependency declarations

Plugins and passes can both declare runs-before and runs-after dependencies on other plugins and passes. These ordering constraints can either be "weak" or "wait-for" dependencies.

Each call to `InPhase` starts a new "Sequence" of passes that run in order. If you call `InPhase` multiple times, the passes you declare in each sequence do not depend on each other and might run in any order, unless you declare dependencies to prevent that.

A typical dependency declaration might look a bit like this:

```
        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
              .AfterPlugin("com.example.some-plugin")
              .BeforePlugin(typeof(SomeMandatoryPlugin))
              .AfterPass(typeof(SomePass))
              .WaitFor(typeof(RunsJustBeforePass))
              .Run(...)
              .BeforePass(typeof(SomeSpecificPass));
        }
```

When using AfterPlugin and BeforePlugin, all passes in the sequence will run after or before the plugin in question. If the plugin is missing, this is not an error, and will be ignored.

You can only declare ordering constraints on specific passes if you have access to their type. Anonymous passes (ones defined by passing a delegate) cannot be specified as a dependency.
The difference between AfterPass and WaitFor is that NDMF will try to schedule your pass immediately after whatever it is `WaitFor`ing, while with AfterPass NDMF will prefer to let the plugin that declared the original pass run to completion first.

## Context data

The `BuildContext` object is passed to all passes when executing them, and contains references to key objects in the avatar (the root GameObject, Transform, and Avatar Descriptor). It also carries some useful state.

The `BuildContext.GetState<T>()` function can be used to attach arbitrary state to the build context, which will be passed from one pass to the next. State attached this way will be created (using a zero-argument constructor) if not yet present.

## Extension contexts

An extension context is a callback which is executed before and after a group of passes which need its services. For example, the `TrackObjectRenamesContext` will track when objects are renamed, and apply those renames to any animations on the avatar. The goal is to be able to amortize the cost of this context across multiple passes which need its services (or which at least don't interfere with the extension context).

Passes can declare required and compatible contexts, e.g.:

```csharp
    abstract class MAPass : PluginPass
    {
        public override IImmutableSet<Type> RequiredContexts =>
            ImmutableHashSet<Type>.Empty.Add(typeof(ModularAvatarContext));
        
        public override IImmutableSet<object> CompatibleContexts =>
            ImmutableHashSet<object>.Empty.Add(typeof(TrackObjectRenamesContext));

        protected BuildContext MAContext(build_framework.BuildContext context)
        {
            return context.Extension<ModularAvatarContext>().BuildContext;
        }
    }
```

A required context instructs the framework to "activate" this context before executing the pass. The context will then be "deactivated" before executing the next pass that is not compatible with that context, or when the build is completed. The context object can then be accessed by calling `BuildContext.Extension<ExtensionName>()`.
