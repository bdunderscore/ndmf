# Execution Model

NDMF supports a rich set of ordering constraints that can control what order processing occurs in.

## High level model

At a high level, there are a few main concepts behind NDMF's sequencing model.

First, we have plugins. Plugins are intended to be the main end-user-visible unit of sequencing. Each plugin then
contains some number of _passes_, which are organized into _sequences_, which are further within _build phases_.

A pass is simply a callback which is executed at a particular point in the build. A sequence is a collection of passes
that occur in a particular order. Finally, build phases provide a coarse way of grouping sequences together.

## Build phases

The following build phases are defined:

#### Resolving

The resolving phase is intended for use by passes which perform very early processing of components and
avatar state, before any large-scale changes have been made. For example, Modular Avatar uses this phase
to resolve string-serialized object passes to their destinations, and to clone animation controllers before
any changes are made to them.

NDMF also has a built-in phase in Resolving, which removes EditorOnly objects. For more information,
see nadena.dev.ndmf.builtin.RemoveEditorOnlyPass.

#### Generating

The generating phase is intended for use by asses which generate components used by later plugins. For
example, if you want to generate components that will be used by Modular Avatar, this would be the place
to do it.

#### Transforming

The transforming phase is intended for general-purpose avatar transformations. Most of Modular Avatar's
logic runs here.

#### Optimizing

The optimizing phase is intended for pure optimizations that need to run late in the build process.

## Sequences and pass constraints

When declaring passes, you first create a sequence, then declare passes within that sequence. If necessary,
you can apply additional constraints, which let you inject additional passes at almost arbitrary points in the build
process.

```csharp

public class MyPlugin : Plugin<MyPlugin>
{
    public override string DisplayName => "Baby's first plugin";

    protected override void Configure()
    {
        Sequence seq = InPhase(BuildPhase.Transforming);
        seq
            .AfterPass(typeof(SomePriorPass))
            .Run(typeof(Pass1))
            .BeforePass(typeof(SomeOtherPass))
            .Then.Run(typeof(Pass2));
    }
}
```

Sequences enforce that passes are executed in the order they are declared. Generally, NDMF will try to run passes in a
sequence right after each other, unless some constraints prevent that from happening.

If you declare multiple sequences in the same build phase, those sequences might be executed in any order relative to
each other (or even interleaved!).

### Constraint types

There are several types of constraints that can be applied to passes:

#### Before/AfterPlugin constraints.

These declare that this particular _sequence_ runs, in its entirety, before or after _all_ processing by some other
plugin. Note that this does not enforce that the other plugin is loaded; it only enforces that if the other plugin is
loaded, it will run before or after this sequence.

```csharp

sequence.BeforePlugin("other.plugin.name");
sequence.AfterPlugin(typeof(OtherPlugin));

```

Because this is intended to help resolve conflicts between optional dependencies, this accepts string names as well as
types. If you use a string name, it must be the fully-qualified name of the plugin (by default, this will be the
fully-qualified type name of the plugin).

#### Before/AfterPass constraints.

These declare that a single pass within a larger sequence runs before or after another pass.

```csharp

sequence.AfterPass(typeof(OtherPass))
    .Run(typeof(MyPass))
    .BeforePass(typeof(SomeOtherPass));

```

Note that the declaration order mirrors the order in which these passes will be executed. In the above example,
the execution order will be OtherPass, MyPass, SomeOtherPass - though, other passes might happen in between.

#### WaitFor constraints

The WaitFor constraint is similar to AfterPass, but NDMF will attempt to run the pass as soon as possible after the
specified pass. This is useful when you want to insert some processing between two passes within another plugin.

```csharp

sequence.WaitFor(typeof(OtherPass))
    .Run(typeof(MyPass));

```

Note that this does not guarantee that the pass will run _immediately_ after the specified pass. If you have other
dependencies on `MyPass` that are not yet satisfied, then `MyPass` will not run until those dependencies are satisfied.

Additionally, there might be multiple `WaitFor` dependencies, in which case the order in which these are executed -
absent other constraints - is undefined.