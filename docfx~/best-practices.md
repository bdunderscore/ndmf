## Best Practices

### Split your plugin into multiple phases when it contains multiple features

When your plugin contains multiple features that can be logically separated,
consider splitting them into different phases.

A single component or plugin can generate, transform, and optimize at once.
If your plugin performs multiple operations, you should consider splitting the generating, transforming, and optimizing phases rather than running everything in a single phase.

### Clean up your components when no longer needed

After your process is complete, you should consider calling [`Object.DestroyImmediate`] on your components.
If you destroy your components, later plugins (especially optimization plugins) won't need to consider your plugin's components.

In some rare cases, you may want to keep your components data on the [state][`BuildContext.GetState`], but this is not common and should be done with caution.

### Don't assume other plugins won't modify objects

Other plugins may run after or between your Sequences.
This is true even when you use [`Sequence.BeforePlugin`] or [`Sequence.AfterPlugin`] constraints, because multiple plugins may request to run just before or after the same plugin.

Therefore, you shouldn't assume your GameObjects, components, or other assets will remain unchanged after your process.

For example, you should not cache assets you retrieved from the avatar between Sequences, because other plugins may update or replace them.

A common mistake is reading settings and assets from the avatar and generating new objects in the generating phase, then assigning them in the transforming phase.
You should instead assign objects in the same Sequences during the generating phase, or read and generate in the same Sequences during the transforming phase, depending on your use case.

### Avoid cloning objects if they're already cloned

[`BuildContext.IsTemporaryAsset`] can be used to check if an object is already a temporary clone.
When possible, you should avoid cloning objects that are already temporary clones to reduce memory usage and improve performance.

Please note that temporary clones are not recursively cloned. You should still clone child objects or referenced assets as needed.

### Avoid passing data between phases or sequences with lambda captures

When writing plugins, you can create local variables on the [`Configure` method][`Plugin.Configure`] and capture them in inline passes created with [run with lambda expression][`Sequence.Run`].
However, you should avoid this pattern to pass data between phases or sequences.

One plugin instance may be used for multiple avatar builds, and single local variables may be shared between different builds.
Instead, you should use the build context's [`GetState` method][`BuildContext.GetState`] to store and retrieve data between phases or sequences.
You may also use [Extension Contexts](~/extension-context.md) for some kinds of data.

<details>
<summary>Examples</summary>

Bad Example

```csharp
public class MyPlugin : Plugin<MyPlugin>
{
    protected override void Configure()
    {
        GameObject generatedObject = null;
        InPhase(BuildPhase.Generating)
            .Run("Pass", ctx =>
            {
                generatedObject = new GameObject("Generated");
            });
    }
}
```

Good Example

```csharp
public class MyPlugin : Plugin<MyPlugin>
{
    public class MyState
    {
        public GameObject GeneratedObject;
    }

    protected override void Configure()
    {
        InPhase(BuildPhase.Generating)
            .Run("Pass", ctx =>
            {
                ctx.GetState<MyState>().GeneratedObject = new GameObject("Generated");
            });
    }
}
```

</details>

### Create your own state type rather than using existing state types

The states are distinguished by their types.
Therefore, you should create your own state type rather than using existing types like `Dictionary<string, object>` or `List<GameObject>`.
This helps avoid conflicts with other plugins that may use the same state type for different purposes.

[`Object.DestroyImmediate`]: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Object.DestroyImmediate.html
[`Sequence.Run`]: xref:nadena.dev.ndmf.fluent.Sequence.Run(System.String,nadena.dev.ndmf.fluent.InlinePass,System.String,System.Int32)
[`Sequence.BeforePlugin`]: xref:nadena.dev.ndmf.fluent.Sequence.BeforePlugin(System.String,System.String,System.Int32)
[`Sequence.AfterPlugin`]: xref:nadena.dev.ndmf.fluent.Sequence.AfterPlugin(System.String,System.String,System.Int32)
[`BuildContext.IsTemporaryAsset`]: xref:nadena.dev.ndmf.BuildContext.IsTemporaryAsset(UnityEngine.Object)
[`BuildContext.GetState`]: xref:nadena.dev.ndmf.BuildContext.GetState*
[`Plugin.Configure`]: xref:nadena.dev.ndmf.Plugin`1.Configure*
