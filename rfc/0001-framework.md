# Summary
[summary]: #summary

Avatar build plugin framework

# Motivation
[motivation]: #motivation

With the release of packages like Modular Avatar, VRCFury, and Avatar Optimizer, it's clear that there's a lot of 
interest in build-time avatar transformations. However, these packages have shown to have compatibility issues, in
part due to needing to build independent implementations of run-at-play-time logic. Additionally, for third-party 
authors it's unclear how to extend these packages with additional functionality (eg - building a new plugin on top of 
Modular Avatar or VRCFury). This framework will serve as a way to improve compatibility between these plugins, and
provide common services to make building new build-time transformations easier.

# Guide-level explanation
[guide-level-explanation]: #guide-level-explanation

The Avatar Build Plugin Framework allows you to easily build a new non-destructive plugin to apply a change to an avatar
at build or play time. At its core, it runs a series of AvatarBuildPlugin implementations at appropriate times in the
build cycle. An AvatarBuildPlugin looks like the following:

```csharp
// assembly-info.cs
[assembly: DefineAvatarBuildPlugin("com.example.mydomain.myplugin", typeof(MyBuildPlugin))]

// MyBuildPlugin.cs
[PluginPhase(Phase.Transform)]
public class MyBuildPlugin : AvatarBuildPlugin {
    public override string DisplayName => "Make Avatar Smaller Plugin";
     
    public void OnProcessAvatar(BuildContext context) {
        // Do something to the avatar, e.g.:
        context.AvatarRootTransform.localScale = Vector3.one * 0.5f;
    }
}

```

The AvatarBuildPlugin framework provides a number of services to make it easier to write plugins. In particular, it
allows you to specify certain ordering constraints:

```csharp

[PluginOrdering("com.example.otherplugin.OtherOptimizationPlugin", Type=ConstraintType.RunsBefore, Required=false)]
[PluginPhase(Phase.Optimize)]
public class MyOptimizationPlugin : AvatarBuildPlugins {
    // ...
}

```

Automatically saves temporary assets:

```csharp

public void OnProcessAvatar(BuildContext context) {
    // ...
    MeshFilter filter = ...;
    Mesh myMesh = filter.sharedMesh;
    if (context.IsPersistent(myMesh)) {
        myMesh = Object.Instantiate(myMesh);
    }
    AlterMesh(myMesh);
    filter.sharedMesh = myMesh;
    // The new mesh will automatically be saved to a temporary asset, and deleted after the build completes.
}

```

And provides mechanisms for passing data between plugins:

```csharp
[PluginOrdering("nadena.dev.modular-avatar", Type=ConstraintType.RunsBefore)]

[PluginPhase(Phase.Generate)]
public class MyBuildPlugin : AvatarBuildPlugin {
    public override string QualifiedName = "com.example.mydomain.myplugin";
    
    public void OnProcessAvatar(BuildContext context) {
        context.Get<ModularAvatarHooks>().AfterCommitAnimations += () => { ... };
    }
}
```

You can even have the framework update animation paths automatically for you after moving around objects:
```csharp
[PluginPhase(Phase.Transform)]
[UseService(typeof(AnimationAdjuster))]
public class MyBuildPlugin : AvatarBuildPlugin {
    // ...
    
    public void OnProcessAvatar(BuildContext context) {
        // ...
        myObject.transform.parent = someOtherObject.transform; // magically updates animation paths!
    }
}
```

# Reference-level explanation
[reference-level-explanation]: #reference-level-explanation

## Plugin lifecycle
[plugin-lifecycle]: #plugin-lifecycle

Plugins are defined using the `DefineAvatarBuildPlugin` assembly attribute. The arguments for this attribute look like
the following:
```
[assembly:DefineAvatarBuildPlugin(string qualifiedName, Type implementingClass[, OnDemand=bool])]
```

The implementing class is required to define a public default constructor. If the `OnDemand` argument is true, the
plugin will only be invoked if some other plugin defines a required ordering constraint on this plugin (see the section
on [asset handling](#asset-handling) for an example of where this is used).

Plugins are instantiated for each build, and passed a `BuildContext` object which provides access to the avatar being
transformed as well as a place to store arbitrary context metadata.

If a plugin throws an exception, the build will fail. For the initial release, we will expose the exception in a visible
dialog box, but in the future I would like to port over a more robust error reporting system (like the one Modular Avatar
is using).

## Build Context

The build context provides a number of services to plugins. The definition looks a bit like this:
```csharp

public sealed class BuildContext {
    public VRCAvatarDescriptor AvatarDescriptor { get; }
    public Transform AvatarRootTransform { get; }
    public GameObject AvatarRootObject { get; }
    
    public T Get<T>() where T : new();
    public T Service<T>() where T : IAvatarBuildService;
    
    // other utility methods...
}
```

The `Get` method can be used to establish a context for passing data between different plugins (alternatively, custom
components are also an option). On first access, the `Get` method will instantiate a new instance of the requested
type, and on subsequent accesses the same instance will be returned.

## Dependencies
Plugins can specify ordering constraints. These can specify that a plugin runs before or after another plugin, or within
a particular "phase". The framework will perform a topological sort to generate a plugin ordering, resolving ties by
lexicographical order. A UI will be provided to allow users to adjust this ordering if desired.

Phases are pre-defined categories for plugins to place themselves into. The following phases are currently defined, and
run in the following order:
1. Phase.Validate - Intended for plugins which perform some kind of validation before doing heavyweight processing
2. Phase.Generate - Intended for plugins which generate avatar components that will later be used by other phases. For
example, this is where you should generate VRCFury or Modular Avatar components.
3. Phase.Transform - Intended for plugins which transform the avatar in some way, possibly in response to configuration
components. This is where e.g. Modular Avatar or VRCFury would live.
4. Phase.Optimize - Intended for plugins which perform optimization, such as Avatar Optimizer or liltoon's shader
optimization processing.

More phases may be defined in the future, but (obviously) the ordering of existing phases cannot change.

## Asset Handling

Asset handling is a particularly error-prone aspect of writing build plugins. The framework provides a number of
services to try to make things a bit safer.

When all plugins have finished running, the framework will automatically walk the avatar and all referenced assets;
any unsaved assets will be saved to a new temporary asset, which will be deleted upon build completion.

If desired (e.g. for better compatibility with built-in Unity helpers), assets can be explicitly saved by calling
`BuildContext.SaveAsset(Object obj)`.

The framework also allows users to determine if an asset is persistent (and therefore should not be altered) by calling
`BuildContext.IsPersistent(Object obj)`. For animators, a `BuildContext.CloneAnimator` is also provided to help reduce
the error-prone tedium of cloning animators manually.

## Services

A service allows certain processing to be amortized across multiple plugins. For example, adjusting animation paths
when objects are moved requires first enumerating all objects, collecting their paths, then later making an appropriate
adjustment to all animations. This can be expensive, and it'd be ideal to amortize it across multiple steps.

A service class looks like this:

```csharp

class MyService : IAvatarBuildService {
    public void OnBeginScope(BuildContext context) {
        // ...
    }
    
    public void OnEndScope(BuildContext context) {
        // ...
    }    
}

```

Services can be requested using the `[UseService]` annotation. A service _begins_ when a plugin requests a service
and _ends_ before a plugin is executed which does not request that service. Plugins which want to avoid ending a service
but also not have a hard dependency can declare a service with `Required=false`, and/or using a fully qualified name
as string instead of a `typeof`: `[UseService("nadena.dev.avatarplugin.AnimationAdjuster", Required=false)]`.

You can access any in-scope services by calling e.g. `buildContext.Service<AnimationAdjuster>()`

### Built-in service: Animation Adjuster

The animation adjuster allows for all animations on the avatar to be automatically corrected when objects are moved
around in the hierarchy. When the service begins, it records the path of all GameObjects in the avatar, and makes note
of all of the animations as well (and their relative paths, if relevant). When its scope ends, it applies these changes
to all animations.

In some cases, there may be components on the avatar that hold animations other than unity built-in components. To
correct these as well, the animation adjuster provides an `IAnimationHolder` interface:

```csharp

public sealed class AnimationMapping {
    public Motion AdjustMotion(Motion sourceMotion, bool relativeToThisObject);
    public string AdjustPath(string originalPath, bool relativeToThisObject);
}

interface IAnimationHolder {
    void AdjustAnimations(AnimationMapping mapping);
}

```

The animation adjuster also provides a public API with some useful helpers:

```csharp

public class AnimationAdjuster : IAvatarBuildService {
    public void Commit();
    public string GetOriginalPath(GameObject target, GameObject relativeTo = null);
}

```

# Drawbacks
[drawbacks]: #drawbacks

This functionality overlaps a bit with the VRCSDK's built-in plugin ordering system. However, that system is quite
limited: there is no support for applying at play mode, and plugin ordering is simply based on integer ordering.
There is no guidance as to what numbers should be selected, and so conflicts are inevitable. Additionally, there's a lot
of subtle pitfalls to writing a build-time plugin. Saving assets can result in editor crashes if you're not careful, for
example, and there's the need to override the existing logic to delete IEditorOnly components.

# Rationale and alternatives
[rationale-and-alternatives]: #rationale-and-alternatives

This design has made some choices that are a bit more complex than may be absolutely necessary. The use of a
topologically sorted dependency ordering is more complex than a simple priority order, but has better extensibility
and makes it much more clear how to order your own new plugins.

The introduction of services is another point of complexity; however, as multiple plugins have already reimplemented the
same animation-adjustment functionality, it makes sense to provide a way to centralize it.

Finally, the choice of using an implicit mechanism for committing newly created assets is one which could have instead
only used a more explicit `context.SaveAsset` call. The motivation for using an implicit mechanism is twofold: One, it
makes it easier and less error-prone to write new plugins, and two, the current Modular Avatar architecture (based on
explicit asset management) has resulted in a proliferation of temporary/trash subassets that make debugging difficult.
Having an explicit reachability pass resolves both of these.

# Prior art
[prior-art]: #prior-art

- [Modular Avatar](https://github.com/bdunderscore/modular-avatar)
- [VRCFury](https://github.com/VRCFury/VRCFury)
- [Avatar Optimizer](https://github.com/anatawa12/AvatarOptimizer)
- [Lazy Optimizer](https://github.com/euan142/LazyOptimiser)
- [liltoon](https://github.com/lilxyzw/lilToon)'s shader optimization pass

# Unresolved questions
[unresolved-questions]: #unresolved-questions

- Is the animation transformation API sufficiently general?
- Do we have the right passes here? Should we have a way to e.g. break up the monolithic ModularAvatar and allow
other plugins to interpose themselves via the plugin ordering framework rather than ad-hoc hooks?
- Are we closing any one-way doors here? Should we make any future-proofing changes to this API?

# Future possibilities
[future-possibilities]: #future-possibilities

There are a number of natural ways this system could be extended.

- Localization: Providing a common interface for loading language files and applying them to editor UI would be
very helpful for plugin authors.
- Error reporting: Modular Avatar has an error reporting framework that I'd like to make available for other
plugin authors. For now, however, I'd like to keep the scope of this proposal reasonable, so this will be a later
extension.
- Caching: Reprocessing the same assets multiple times as you run test builds can waste a lot of time. By being able to
preserve generated assets across builds (and discard them when their inputs change), we could save on build time.