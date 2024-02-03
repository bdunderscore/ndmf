Sequencing refers to the process of determining which plugin passes to execute, and in what sequence.
When making this determination, we want to consider the following goals:

* Understandability. Plugin developers should be able to easily understand _why_ their plugins are being executed in a 
particular order, and how to change it. They should also clearly understand when constraints are in conflict.
* Stability. Just because a particular sequence is unconstrained, doesn't mean anyone expected it to happen. Sane 
soft-constraints should be in place to help prevent strange things from happening.

## Constraints

Constraints are a way for a plugin author to express what order things need to happen in. The following constraints are
defined, in order of "binding strength":

* After constraints: These constraints that a particular pass should run ASAP after another pass, provided
all other constraints are satisfied. Only one runs-soon-after constraint can be specified per 'after' pass.
* Successor constraints are internally defined by the order in which phases are defined in the containing plugin. Unless
a runs-soon-after constraint intervenes, or the successor's other constraints are unsatisfied, we prefer to run the 
successor pass immediately after the prior pass.
* NotBefore/NotAfter constraints: These declare that one pass must run after/before
the other, but express no strong preference as to how quickly this will happen.

### Constraint solving

At a high level, all constraints are converted into 'NotBefore' constraints, and then a topological sort is performed to
determine which order passes execute in. However, this toposort is influenced by a number of heuristics.

In particular, to satisfy After and Successor constraints, we maintain a 'successor stack'. This stack contains
all passes for which their successor relationship (or one or more After constraints) has not yet been
satisfied. When a pass is scheduled, we check whether the current top of the stack is now satisfied; if so we then pop
it. Then, if the scheduled pass has a successor or is referenced by an After constraint, we push it onto the
stack.

Note that there is another conundrum when it comes to runs-soon-after constraints: What happens if a pass X
is After Y, and NotBefore Z? We'd like Z to be scheduled before Y to keep X close to Y, but the above logic does not
guarantee this. We choose not to handle this in the constraint solver. If it's essential that this ordering be 
maintained, then the plugin author of pass X should declare a pseudopass with runs-after Y and runs-before Z to enforce
this ordering.

### Constraint targets

A constraint can target a pass, phase, or plugin. If a constraint targets a pass, then the declared operation will
happen after/before the specified pass. Execution phases themselves act as passes, and can be targeted by constraints,
just like passes (in this case, the constraint referents must be in the same parent phase).

When targeting plugins, the effect is a bit special - the constraint is applied to all passes or phases in the same
parent phase. This is useful for plugins that want to declare a global ordering constraint, such as "always run after
Modular Avatar", without worrying too much about the details of what passes are in Modular Avatar.

### Missing references

Runs-before/Runs-after constraints are considered optional - if the targeted pass does not exist, the constraint is
ignored. However, runs-soon-after 

## Phases

A "phase" is essentially a group of passes, with its own independent dependency ordering. Overall execution is then
organized into a tree, where phases are nodes, and passes are leaves. Phases themselves can be the target of
constraints.

The main purpose of phases is to provide clearly delineated places for other plugins to insert their own passes. For
example, NDMF declares a number of built-in phases:

* Resolving
* Generating
* Transforming
* Optimizing

Other plugins, such as Modular Avatar, might offer phases within their own processing.

### Constraints in phases

All constraints are phase-local. That is: 
* Successor constraints are generated only between passes that are on the same phase.
* After/NotBefore/NotAfter constraints referring to _passes_ or _phases_ must refer to a pass or phase with the same
containing phase.
* After/NotBefore/NotAfter constraints referring to _plugins_ will only constrain against other passes/phases belonging
to the same plugin within the same phase.

The latter, in particular, means that if you declare, say, NotBefore Modular Avatar, you might still run before Modular
Avatar's passes, if they are in a different phase.

## Declaration syntax

All passes and phases are (typically singleton) objects. A typical pass might look like this:

```csharp

class MyPass : Pass<MyPass> {
    protected override void Execute(BuildContext context) {
        // Do stuff
    }
}
```

Dependency ordering is declared at the plugin level:

```csharp

[RequiresExtension(typeof(SomeExtension))]
[OptionalExtension("com.example.SomeOtherExtension"))]
class SomeOtherPlugin : Plugin<MyPlugin> {
    /// ...
    public static Type ExposedPass => typeof(SomeInternalPass);
}

class MyPlugin : Plugin<MyPlugin> {
    public override string QualifiedName => "my.plugin";
    
    protected override void Configure() {
        InPhase(Phases.Generating)
          .AfterPlugin("nadena.dev.modular-avatar")
          .Run<MyPass>()
          .Run<MyOtherPass>()
          // Inline passes are supported, but cannot be named in a dependency reference
          .Run("my inline pass", context => { /* ... */ })
          .WaitFor(SomeOtherPlugin.ExposedPass) 
          .Run<MyFinalPass>() // runs ASAP after ExposedPass
          .BeforePlugin("some.other.plugin");
        
        // This is a separate _sequence_ of passes. No successor edge is generated, so this sequence might end up being
        // scheduled before the other sequence.
        InPhase(Phases.Generating)
          .Run(MyIndependentPass.Instance);
    }
}

```
