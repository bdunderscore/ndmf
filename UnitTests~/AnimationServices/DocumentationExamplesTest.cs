using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if NDMF_VRCSDK3_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace UnitTests.AnimationServices
{
    public class DocumentationExamplesTest : TestBase
    {
        /// <summary>
        /// Test accessing AnimatorServicesContext and its services as shown in the documentation
        /// </summary>
        [Test]
        public void AnimatorServicesContext_Services_Available()
        {
            var root = CreateRoot("TestAvatar");
            var context = CreateContext(root);
            
            // Manually activate the context (this is what WithRequiredExtensions would do internally)
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
            // Test the service access code from the documentation example
            var controllerContext = animatorServices.ControllerContext;
            var animationIndex = animatorServices.AnimationIndex;
            var pathRemapper = animatorServices.ObjectPathRemapper;
            
            // Verify all services are available as documented
            Assert.NotNull(animatorServices);
            Assert.NotNull(controllerContext);
            Assert.NotNull(animationIndex);
            Assert.NotNull(pathRemapper);
            
            context.DeactivateAllExtensionContexts();
        }
        
        /// <summary>
        /// Test the DependsOnContext attribute pattern shown in the documentation
        /// </summary>
        [Test]
        public void DependsOnContext_Pattern_Works()
        {
            var root = CreateRoot("TestAvatar");
            var context = CreateContext(root);
            
            // Test the pass implementation from the documentation
            var testPass = new TestAnimationPass();
            
            // Manually activate the context (simulating what the plugin system does)
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            
            // Execute the pass
            testPass.ExecutePublic(context);
            
            // Verify the pass executed successfully
            Assert.True(testPass.ExecutedSuccessfully);
            
            context.DeactivateAllExtensionContexts();
        }
        
        /// <summary>
        /// Test virtual object creation as shown in the documentation
        /// </summary>
        [Test]
        public void VirtualObjectCreation_Works()
        {
            var root = CreateRoot("TestAvatar");
            var context = CreateContext(root);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controllerContext = animatorServices.ControllerContext;
            
            // Test the virtual object creation code from the docs
            var cloneContext = controllerContext.CloneContext;
            
            // Create existing controller to clone
            var unityController = new AnimatorController();
            unityController.name = "TestController";
            var virtualController = cloneContext.Clone(unityController);
            Assert.NotNull(virtualController);
            Assert.AreEqual("TestController", virtualController.Name);
            
            // Create new virtual objects as shown in docs
            var newVirtualController = VirtualAnimatorController.Create(cloneContext, "MyController");
            var newVirtualClip = VirtualClip.Create("MyClip");
            var newVirtualLayer = newVirtualController.AddLayer(LayerPriority.Default, "MyLayer");
            
            Assert.NotNull(newVirtualController);
            Assert.AreEqual("MyController", newVirtualController.Name);
            Assert.NotNull(newVirtualClip);
            Assert.AreEqual("MyClip", newVirtualClip.Name);
            Assert.NotNull(newVirtualLayer);
            Assert.AreEqual("MyLayer", newVirtualLayer.Name);
            
            context.DeactivateAllExtensionContexts();
        }
        
        /// <summary>
        /// Test virtual clip creation with curves as shown in documentation
        /// </summary>
        [Test]
        public void VirtualClipCreation_WithCurves_Works()
        {
            var root = CreateRoot("TestAvatar");
            var targetObject = CreateChild(root, "TargetObject");
            var context = CreateContext(root);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var pathRemapper = animatorServices.ObjectPathRemapper;
            
            // Test the virtual clip creation code from the docs
            var virtualClip = VirtualClip.Create("MyClip");
            
            // Add animation curves using virtual paths as shown in docs
            var binding = EditorCurveBinding.FloatCurve(
                pathRemapper.GetVirtualPathForObject(targetObject), 
                typeof(Transform), 
                "localPosition.x"
            );
            virtualClip.SetFloatCurve(binding, AnimationCurve.Linear(0, 0, 1, 10));
            
            // Verify the curve was added
            var bindings = virtualClip.GetFloatCurveBindings().ToArray();
            Assert.AreEqual(1, bindings.Length);
            Assert.AreEqual(binding.path, bindings[0].path);
            Assert.AreEqual(binding.type, bindings[0].type);
            Assert.AreEqual(binding.propertyName, bindings[0].propertyName);
            
            context.DeactivateAllExtensionContexts();
        }
        
        /// <summary>
        /// Test virtual paths and ObjectPathRemapper usage as shown in documentation
        /// </summary>
        [Test]
        public void VirtualPaths_And_ObjectPathRemapper_Work()
        {
            var root = CreateRoot("TestAvatar");
            var someGameObject = CreateChild(root, "SomeObject");
            var context = CreateContext(root);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var pathRemapper = animatorServices.ObjectPathRemapper;
            
            // Test the path remapper usage from docs
            string virtualPath = pathRemapper.GetVirtualPathForObject(someGameObject);
            Assert.AreEqual("SomeObject", virtualPath);
            
            // Test that virtual path remains constant even if object is renamed
            someGameObject.name = "RenamedObject";
            string virtualPathAfterRename = pathRemapper.GetVirtualPathForObject(someGameObject);
            Assert.AreEqual("SomeObject", virtualPathAfterRename, "Virtual path should remain constant");
            
            // Test object replacement
            var newObject = CreateChild(root, "NewObject");
            pathRemapper.ReplaceObject(someGameObject, newObject);
            
            // Test recording new object tree
            var newPrefabObject = CreateChild(root, "NewPrefabObject");
            pathRemapper.RecordObjectTree(newPrefabObject.transform);
            string newObjectPath = pathRemapper.GetVirtualPathForObject(newPrefabObject);
            Assert.AreEqual("NewPrefabObject", newObjectPath);
            
            context.DeactivateAllExtensionContexts();
        }
        
#if NDMF_VRCSDK3_AVATARS
        /// <summary>
        /// Test VRChat layer access as shown in the documentation
        /// </summary>
        [Test]
        public void VRChatLayerAccess_Works()
        {
            var root = CreateRoot("TestAvatar");
            var context = CreateContext(root);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controllerContext = animatorServices.ControllerContext;
            
            // Test VRChat layer access from the docs
            if (controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fxController))
            {
                Assert.NotNull(fxController);
                
                // Add a new layer as shown in docs
                var newLayer = fxController.AddLayer(LayerPriority.Default, "MyCustomLayer");
                Assert.NotNull(newLayer);
                Assert.AreEqual("MyCustomLayer", newLayer.Name);
                
                // Test accessing other layers as mentioned in docs
                Assert.True(controllerContext.Controllers.ContainsKey(VRCAvatarDescriptor.AnimLayerType.Base));
                Assert.True(controllerContext.Controllers.ContainsKey(VRCAvatarDescriptor.AnimLayerType.Additive));
                Assert.True(controllerContext.Controllers.ContainsKey(VRCAvatarDescriptor.AnimLayerType.Gesture));
                Assert.True(controllerContext.Controllers.ContainsKey(VRCAvatarDescriptor.AnimLayerType.Action));
            }
            
            context.DeactivateAllExtensionContexts();
        }
        
        /// <summary>
        /// Test the complete toggle animation example from the documentation
        /// </summary>
        [Test]
        public void CompleteToggleAnimationExample_Works()
        {
            var root = CreateRoot("TestAvatar");
            var someObject = CreateChild(root, "SomeObject");
            var context = CreateContext(root);
            
            // Execute the complete example from the documentation
            var togglePass = new AddToggleAnimationPass();
            
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            togglePass.ExecutePublic(context);
            context.DeactivateAllExtensionContexts();
            
            // Verify the toggle animation was created
            var vrcDesc = root.GetComponent<VRCAvatarDescriptor>();
            var fxLayer = vrcDesc.baseAnimationLayers.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            
            Assert.NotNull(fxLayer.animatorController);
            var controller = fxLayer.animatorController as AnimatorController;
            Assert.NotNull(controller);
            
            // Check that the toggle layer was added
            var toggleLayer = controller.layers.FirstOrDefault(l => l.name == "MyObjectToggle");
            Assert.NotNull(toggleLayer, "Toggle layer should have been added");
            
            // Check that parameter was added
            var parameter = controller.parameters.FirstOrDefault(p => p.name == "ToggleMyObject");
            Assert.NotNull(parameter, "Toggle parameter should have been added");
            Assert.AreEqual(AnimatorControllerParameterType.Bool, parameter.type);
            
            // Check states exist
            Assert.AreEqual(2, toggleLayer.stateMachine.states.Length, "Should have two states (On/Off)");
            var offState = toggleLayer.stateMachine.states.FirstOrDefault(s => s.state.name == "Off");
            var onState = toggleLayer.stateMachine.states.FirstOrDefault(s => s.state.name == "On");
            Assert.NotNull(offState.state, "Off state should exist");
            Assert.NotNull(onState.state, "On state should exist");
            
            // Check default state
            Assert.AreEqual("Off", toggleLayer.stateMachine.defaultState.name);
            
            // Check transitions exist
            Assert.Greater(offState.state.transitions.Length, 0, "Off state should have transitions");
            Assert.Greater(onState.state.transitions.Length, 0, "On state should have transitions");
        }
        
        /// <summary>
        /// Test layer management operations from the documentation
        /// </summary>
        [Test]
        public void LayerManagement_Operations_Work()
        {
            var root = CreateRoot("TestAvatar");
            var context = CreateContext(root);
            var animatorServices = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            var controllerContext = animatorServices.ControllerContext;
            
            if (controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var virtualController))
            {
                // Test adding a new layer as shown in docs
                var newLayer = virtualController.AddLayer(LayerPriority.Default, "MyLayer");
                newLayer.DefaultWeight = 1.0f;
                newLayer.BlendingMode = AnimatorLayerBlendingMode.Override;
                
                Assert.NotNull(newLayer);
                Assert.AreEqual("MyLayer", newLayer.Name);
                Assert.AreEqual(1.0f, newLayer.DefaultWeight);
                Assert.AreEqual(AnimatorLayerBlendingMode.Override, newLayer.BlendingMode);
                
                // Test modifying existing layer
                var existingLayer = virtualController.Layers.FirstOrDefault(l => l.Name == "MyLayer");
                Assert.NotNull(existingLayer);
                existingLayer.DefaultWeight = 0.5f;
                existingLayer.BlendingMode = AnimatorLayerBlendingMode.Additive;
                
                Assert.AreEqual(0.5f, existingLayer.DefaultWeight);
                Assert.AreEqual(AnimatorLayerBlendingMode.Additive, existingLayer.BlendingMode);
                
                // Test removing a layer
                int layerCountBefore = virtualController.Layers.Count();
                virtualController.RemoveLayer(newLayer);
                int layerCountAfter = virtualController.Layers.Count();
                
                Assert.AreEqual(layerCountBefore - 1, layerCountAfter, "Layer should have been removed");
            }
            
            context.DeactivateAllExtensionContexts();
        }
#endif
    }
    
    /// <summary>
    /// Test pass implementation using DependsOnContext attribute from the documentation
    /// </summary>
    [DependsOnContext(typeof(AnimatorServicesContext))]
    public class TestAnimationPass : Pass<TestAnimationPass>
    {
        public bool ExecutedSuccessfully { get; private set; }
        
        protected override void Execute(BuildContext context)
        {
            ExecutePublic(context);
        }
        
        public void ExecutePublic(BuildContext context)
        {
            // This code is from the documentation example
            var animatorServices = context.Extension<AnimatorServicesContext>();
            var controllerContext = animatorServices.ControllerContext;
            var animationIndex = animatorServices.AnimationIndex;
            var pathRemapper = animatorServices.ObjectPathRemapper;
            
            // Verify all services are available
            Assert.NotNull(animatorServices);
            Assert.NotNull(controllerContext);
            Assert.NotNull(animationIndex);
            Assert.NotNull(pathRemapper);
            
            ExecutedSuccessfully = true;
        }
    }
    
#if NDMF_VRCSDK3_AVATARS
    /// <summary>
    /// Complete toggle animation pass implementation from the documentation
    /// </summary>
    [DependsOnContext(typeof(AnimatorServicesContext))]
    public class AddToggleAnimationPass : Pass<AddToggleAnimationPass>
    {
        protected override void Execute(BuildContext context)
        {
            ExecutePublic(context);
        }
        
        public void ExecutePublic(BuildContext context)
        {
            var animatorServices = context.Extension<AnimatorServicesContext>();
            var controllerContext = animatorServices.ControllerContext;
            var pathRemapper = animatorServices.ObjectPathRemapper;
            
            // Get the FX layer
            if (!controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fxController))
                return;
            
            // Find the target object
            var targetObject = context.AvatarRootObject.transform.Find("SomeObject");
            if (targetObject == null) return;
            
            // Get virtual path for the object
            string virtualPath = pathRemapper.GetVirtualPathForObject(targetObject.gameObject);
            
            // Add parameter using the Parameters property (ImmutableDictionary)
            fxController.Parameters = fxController.Parameters.Add("ToggleMyObject", 
                new AnimatorControllerParameter 
                { 
                    name = "ToggleMyObject", 
                    type = AnimatorControllerParameterType.Bool 
                });
            
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
            
            // Add transitions using the Transitions property (ImmutableList)
            var toOn = VirtualStateTransition.Create();
            toOn.SetDestination(onState);
            toOn.Conditions = toOn.Conditions.Add(new AnimatorCondition
            {
                mode = AnimatorConditionMode.If,
                parameter = "ToggleMyObject",
                threshold = 0
            });
            toOn.Duration = 0;
            offState.Transitions = offState.Transitions.Add(toOn);
            
            var toOff = VirtualStateTransition.Create();
            toOff.SetDestination(offState);
            toOff.Conditions = toOff.Conditions.Add(new AnimatorCondition
            {
                mode = AnimatorConditionMode.IfNot,
                parameter = "ToggleMyObject",
                threshold = 0
            });
            toOff.Duration = 0;
            onState.Transitions = onState.Transitions.Add(toOff);
        }
    }
#endif
}