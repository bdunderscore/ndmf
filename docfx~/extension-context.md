# Extension Contexts

Extension contexts are designed to help improve build performance by amortizing some kind of processing across
multiple passes. For example, if you have multiple passes which need to do the same analysis across all animation
controllers, you can use an extension context to do that analysis once, then share the results with all passes.

Passes declare that they either require or are compatible with specific extension context. When a pass declares that it
requires an extension context, the extension context will be "activated" before the pass is executed. The extension
context will then be "deactivated" when any pass that is not compatible with it is executed. This allows for any
deferred operations (e.g. updating animations after objects move around) to be performed.

The compatibility declaration is important as - for example - there might be deferred work that needs to be performed
before a pass that is oblivious to the extension context can be allowed to execute.

You can declare that a pass requires or is compatible with an extension context like so:

```csharp

sequence.WithCompatibleExtension("foo.bar.ExtensionClass", seq2 => {
    seq2.WithRequiredExtension(typeof(OtherExtensionClass), seq3 => {
        seq3.Run(typeof(MyPass));
    });
});

```