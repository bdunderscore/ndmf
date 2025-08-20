# AnimatorServices Guide

The AnimatorServices system provides powerful tools for manipulating Unity Animator Controllers during the NDMF build process. This system allows you to work with "virtualized" animation objects that can be safely modified without affecting the original assets until the build is complete.

## Getting Started

### Requesting AnimatorServicesContext

To use the AnimatorServices system, your plugin needs to activate the `AnimatorServicesContext` extension. There are two ways to do this:

#### Using WithRequiredExtensions

```csharp
[assembly: ExportsPlugin(typeof(MyAnimationPlugin))]

public class MyAnimationPlugin : Plugin<MyAnimationPlugin>
{
    protected override void Configure()
    {
        InPhase(BuildPhase.Transforming)
            .WithRequiredExtensions(new[] { typeof(AnimatorServicesContext) }, seq =>
            {
                seq.Run("My Animation Pass", ctx =>
                {
                    // Access the AnimatorServicesContext
                    var animatorServices = ctx.Extension<AnimatorServicesContext>();
                    var controllerContext = animatorServices.ControllerContext;
                    var animationIndex = animatorServices.AnimationIndex;
                    var pathRemapper = animatorServices.ObjectPathRemapper;
                    
                    // Your animation manipulation code here
                });
            });
    }
}
```

#### Using DependsOnContext Attribute

```csharp
[DependsOnContext(typeof(AnimatorServicesContext))]
public class MyAnimationPass : Pass<MyAnimationPass>
{
    protected override void Execute(BuildContext context)
    {
        // Access the AnimatorServicesContext
        var animatorServices = context.Extension<AnimatorServicesContext>();
        var controllerContext = animatorServices.ControllerContext;
        var animationIndex = animatorServices.AnimationIndex;
        var pathRemapper = animatorServices.ObjectPathRemapper;
        
        // Your animation manipulation code here
    }
}
```

### Important Requirements While AnimatorServicesContext is Active

When the AnimatorServicesContext is active, there are several important constraints you must observe:

1. **Use Virtual Paths for New Animations**: The ObjectPathRemapper takes a snapshot of object paths when activated. Any new animations must use these virtual paths, not the current hierarchy paths. Use `ObjectPathRemapper.GetVirtualPathForObject()` to get the correct path.

2. **Object Removal Protocol**: If you want to remove an object from the hierarchy, you must call `ObjectPathRemapper.ReplaceObject()` first before removing it.

3. **Register New Objects**: When adding new objects that will be used in animations, use `ObjectPathRemapper.RecordObjectTree()` to register them, or use `GetVirtualPathForObject()` which automatically registers objects.

## Converting Unity Animation Types to Virtual Types

The AnimatorServices system works with "Virtual" versions of Unity's animation types. Here's how to convert between them:

### Creating Virtual Objects

```csharp
// Get the CloneContext from VirtualControllerContext
var cloneContext = controllerContext.CloneContext;

// Convert existing AnimatorController to VirtualAnimatorController
AnimatorController unityController = GetSomeController();
var virtualController = cloneContext.Clone(unityController);

// Create new virtual objects
var newVirtualController = VirtualAnimatorController.Create(cloneContext, "MyController");
var newVirtualClip = VirtualClip.Create("MyClip");
var newVirtualLayer = newVirtualController.AddLayer(LayerPriority.Default, "MyLayer");
```

### Working with Virtual Animation Clips

```csharp
// Create a new virtual clip
var virtualClip = VirtualClip.Create("MyClip");

// Add animation curves (use virtual paths!)
var binding = EditorCurveBinding.FloatCurve(
    pathRemapper.GetVirtualPathForObject(someGameObject), 
    typeof(Transform), 
    "localPosition.x"
);
virtualClip.SetFloatCurve(binding, AnimationCurve.Linear(0, 0, 1, 10));

// Add the clip to a state
var state = layer.StateMachine.AddState("MyState", motion: virtualClip);
```

## Working with VirtualControllerContext

The `VirtualControllerContext` provides access to platform-specific animation layers, particularly useful for VRChat avatar manipulation.

### Accessing VRChat Animation Layers

```csharp
var controllerContext = animatorServices.ControllerContext;

// Access specific VRChat layers (requires VRCSDK3)
#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;

// Get the FX layer controller
if (controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fxController))
{
    // Add a new layer to FX
    var newLayer = fxController.AddLayer(LayerPriority.Default, "MyCustomLayer");
    
    // Create states and transitions
    var idleState = newLayer.StateMachine.AddState("Idle");
    var activeState = newLayer.StateMachine.AddState("Active", motion: myVirtualClip);
    
    // Add transition with conditions
    var transition = idleState.AddTransition(activeState);
    transition.AddCondition(AnimatorConditionMode.If, 0, "MyParameter");
}

// Access other layers
var baseController = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.Base];
var additiveController = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.Additive];
var gestureController = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.Gesture];
var actionController = controllerContext.Controllers[VRCAvatarDescriptor.AnimLayerType.Action];
#endif
```

### Adding and Removing Layers

```csharp
// Add a new layer
var newLayer = virtualController.AddLayer(LayerPriority.Default, "MyLayer");
newLayer.Weight = 1.0f;
newLayer.BlendingMode = AnimatorLayerBlendingMode.Override;

// Remove a layer
virtualController.RemoveLayer("UnwantedLayer");

// Modify existing layer
var existingLayer = virtualController.Layers.FirstOrDefault(l => l.Name == "ExistingLayer");
if (existingLayer != null)
{
    existingLayer.Weight = 0.5f;
    existingLayer.BlendingMode = AnimatorLayerBlendingMode.Additive;
}
```

## Using AnimationIndex and ObjectPathRemapper

### Path Remapping and Object Tracking

The `ObjectPathRemapper` is crucial for handling object hierarchy changes during the build process:

```csharp
var pathRemapper = animatorServices.ObjectPathRemapper;

// Get the virtual path for a GameObject
string virtualPath = pathRemapper.GetVirtualPathForObject(myGameObject);

// Record a new object tree (for prefab instantiation)
GameObject newPrefab = PrefabUtility.InstantiatePrefab(somePrefab) as GameObject;
pathRemapper.RecordObjectTree(newPrefab.transform);

// Replace an object before removing it
pathRemapper.ReplaceObject(oldObject, newObject);

// Get final path mappings (typically done automatically on deactivation)
var pathMappings = pathRemapper.GetVirtualToRealPathMap();
```

### Animation Indexing and Querying

The `AnimationIndex` provides efficient ways to find and modify animations:

```csharp
var animationIndex = animatorServices.AnimationIndex;

// Find all clips that affect a specific object path
var clipsForObject = animationIndex.GetClipsForObjectPath("MyObject/ChildObject");

// Find clips for a specific binding
var binding = EditorCurveBinding.FloatCurve("MyObject", typeof(Transform), "localPosition.x");
var clipsForBinding = animationIndex.GetClipsForBinding(binding);

// Apply changes to all clips affecting an object
animationIndex.ProcessClipsForObjectPath("MyObject", (clip, binding) =>
{
    // Modify the clip/binding as needed
    if (binding.propertyName == "localPosition.x")
    {
        var curve = clip.GetFloatCurve(binding);
        // Modify curve...
        clip.SetFloatCurve(binding, curve);
    }
});
```

### Advanced Path Remapping

```csharp
// Create custom path remapping rules
var pathMappings = new Dictionary<string, string?>
{
    ["OldPath/Object"] = "NewPath/Object",  // Rename path
    ["DeletedPath/Object"] = null,          // Delete path (remove from animations)
    ["AnotherPath"] = "ReplacementPath"     // Replace path
};

// Apply the remapping (this happens automatically on deactivation)
animationIndex.RewritePaths(pathMappings);
```

## Complete Example: Adding a Toggle Animation

Here's a complete example that demonstrates adding a toggle animation to a VRChat avatar:

```csharp
[DependsOnContext(typeof(AnimatorServicesContext))]
public class AddToggleAnimationPass : Pass<AddToggleAnimationPass>
{
    protected override void Execute(BuildContext context)
    {
        var animatorServices = context.Extension<AnimatorServicesContext>();
        var controllerContext = animatorServices.ControllerContext;
        var pathRemapper = animatorServices.ObjectPathRemapper;
        
        #if NDMF_VRCSDK3_AVATARS
        // Get the FX layer
        if (!controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fxController))
            return;
        
        // Find the target object
        var targetObject = context.AvatarRootObject.transform.Find("SomeObject");
        if (targetObject == null) return;
        
        // Get virtual path for the object
        string virtualPath = pathRemapper.GetVirtualPathForObject(targetObject.gameObject);
        
        // Add parameter
        fxController.AddParameter("ToggleMyObject", AnimatorControllerParameterType.Bool);
        
        // Create animation clips
        var onClip = VirtualClip.Create("MyObject_On");
        var offClip = VirtualClip.Create("MyObject_Off");
        
        // Set up the animation curves using virtual paths
        var enabledBinding = EditorCurveBinding.FloatCurve(virtualPath, typeof(GameObject), "m_IsActive");
        onClip.SetFloatCurve(enabledBinding, AnimationCurve.Constant(0, 1/60f, 1));
        offClip.SetFloatCurve(enabledBinding, AnimationCurve.Constant(0, 1/60f, 0));
        
        // Add layer
        var toggleLayer = fxController.AddLayer(LayerPriority.Default, "MyObjectToggle");
        
        // Create states
        var offState = toggleLayer.StateMachine.AddState("Off", motion: offClip);
        var onState = toggleLayer.StateMachine.AddState("On", motion: onClip);
        
        // Set default state
        toggleLayer.StateMachine.DefaultState = offState;
        
        // Add transitions
        var toOn = offState.AddTransition(onState);
        toOn.AddCondition(AnimatorConditionMode.If, 0, "ToggleMyObject");
        toOn.Duration = 0;
        
        var toOff = onState.AddTransition(offState);
        toOff.AddCondition(AnimatorConditionMode.IfNot, 0, "ToggleMyObject");
        toOff.Duration = 0;
        #endif
    }
}
```

## Best Practices

1. **Always use virtual paths** when creating new animations while AnimatorServicesContext is active
2. **Register new objects** before using them in animations
3. **Use the CloneContext** when converting Unity objects to Virtual objects
4. **Defer heavy path remapping** until necessary - the system handles most remapping automatically on deactivation
5. **Check for platform availability** when working with platform-specific features like VRChat layers
6. **Clean up properly** - the system handles most cleanup automatically when the context is deactivated

## Troubleshooting

- **"Path not found" errors**: Make sure you're using `GetVirtualPathForObject()` for new animations
- **Objects not being tracked**: Call `RecordObjectTree()` for newly instantiated prefabs
- **Changes not persisting**: Ensure the AnimatorServicesContext remains active during your modifications
- **Path collisions**: Use unique names or check paths with `GetVirtualPathForObject()` before creating objects