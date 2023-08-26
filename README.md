# Avatar Build Plugin Framework (working title)

This package is a framework for building non-destructive editor plugins for VRChat avatars. It provides the following facilities:

* Plugin execution sequencing based on high level phases, and explicit runs-before/runs-after relationships between processing passes
* Support for applying transformations when entering play mode
* Support for applying transformations when building avatars
* Support for saving temporary/generated assets, and cleaning up those assets once the avatar build is completed.
* Support for adjusting animation paths after objects are moved

Future plans include:
* A unified error reporting UI
* Support for caching generated assets across subsequent builds
* Support for platforms other than VRChat.

## Project status

This project is currently in a pre-alpha state. Expect large-scale refactoring, including renaming of the project itself, in the coming weeks. The framework is however functional, and you can take a look at the draft PRs for [Modular Avatar](https://github.com/bdunderscore/modular-avatar/pull/406) and [AAO](https://github.com/anatawa12/AvatarOptimizer/pull/375) to see examples of usage.

## Getting started

A minimal plugin definition looks a bit like this:

```csharp
[assembly: ExportsPlugin(typeof MyPlugin)]

namespace com.mydomain {
  class MyPlugin : Plugin {
    public override string QualifiedName => "com.mydomain.myplugin";
    public override ImmutableList<Pass> Passes => ImmutableList<PluginPass>.Empty
      .Add(new MyPass());
  }

  class MyPass : PluginPass {
    public override void Process(BuildContext context) {
      // do something to the avatar at context.AvatarRootTransform
    }
  }
}
```

## Execution model

ABPF models execution using "Plugins" and "Passes". A plugin is meant to be an end-user-visible extension, such as Modular Avatar or AAO, while a pass is an internal step in the execution of that plugin. Breaking your execution into smaller passes allows better control of the order of execution between passes.

Passes are grouped into execution phases, which execute in the following order:
* Resolving - This is intended to run before any editor extensions modify the avatar, and is useful for rehydrating components with serialized state that need to refer to the pre-transformation avatar (e.g. if you have a path serialized to a string which you need to resolve to an object before objects start moving around)
* Generating - This is intended to run before editor extensions which primarily generate new objects and components for use by other systems.
* Transforming - This is intended as the "general-purpose" execution phase, where most extensions which transform avatars run.
* Optimization - This is intended as an execution phase for optimization plugins which aren't intended to modify the avatar in a semantically-meaningful way.

Within each phase, passes are always executed in the order in which they are declared in the plugin definition. However, depending on dependency declarations, passes from other plugins can be injected between your passes.

Plugins and passes can both declare runs-before and runs-after dependencies on other plugins and passes. These dependencies are applied independently for each phase. For example, consider this example:

```
 Plugin A runs-before plugin B
 Plugin A passes: (Resolving:A1, Resolving:A2, Generating: A1)
 Plugin B passes: (Resolving:B1, Generating: B2)
 Resulting order: (Resolving:A1, Resolving:A2, Resolving:B1, Generating:A1, Generating:B2)
```

When you declare a runs-before or runs-after at the _pass_ level, you can insert a pass in between passes from another plugin. As such, generally you should define your passes in such a way that it's safe to inject additional transformations before or after. If it's not, consider merging passes, or not exposing the pass name.

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
