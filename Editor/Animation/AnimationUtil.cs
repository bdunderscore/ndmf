using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace nadena.dev.build_framework.animation
{
    public static class AnimationUtil
    {
        private const string SAMPLE_PATH_PACKAGE =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private const string SAMPLE_PATH_LEGACY = "Assets/VRCSDK/Examples3/Animation/Controllers";

        private const string GUID_GESTURE_HANDSONLY_MASK = "b2b8bad9583e56a46a3e21795e96ad92";

        
        public static AnimatorController DeepCloneAnimator(this BuildContext context, RuntimeAnimatorController controller)
        {
            if (controller == null) return null;

            var merger = new AnimatorCombiner(context, controller.name + " (clone)");
            switch (controller)
            {
                case AnimatorController ac:
                    merger.AddController("", ac, null);
                    break;
                case AnimatorOverrideController oac:
                    merger.AddOverrideController("", oac, null);
                    break;
                default:
                    throw new Exception("Unknown RuntimeAnimatorContoller type " + controller.GetType());
            }

            return merger.Finish();
        }
        
        internal static void CloneAllControllers(BuildContext context)
        {
            // Ensure all of the controllers on the avatar descriptor point to temporary assets.
            // This helps reduce the risk that we'll accidentally modify the original assets.
            
            context.AvatarDescriptor.baseAnimationLayers = CloneLayers(context, context.AvatarDescriptor.baseAnimationLayers);
            context.AvatarDescriptor.specialAnimationLayers = CloneLayers(context, context.AvatarDescriptor.specialAnimationLayers);
        }

        private static VRCAvatarDescriptor.CustomAnimLayer[] CloneLayers(
            BuildContext context,
            VRCAvatarDescriptor.CustomAnimLayer[] layers
        )
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer.animatorController != null && !context.IsTemporaryAsset(layer.animatorController))
                {
                    layer.animatorController = context.DeepCloneAnimator(layer.animatorController);                    
                }

                layers[i] = layer;
            }

            return layers;
        }

        public static AnimatorController GetOrInitializeController(
            this BuildContext context,
            VRCAvatarDescriptor.AnimLayerType type)
        {
            return FindLayer(context.AvatarDescriptor.baseAnimationLayers)
                   ?? FindLayer(context.AvatarDescriptor.specialAnimationLayers);

            AnimatorController FindLayer(VRCAvatarDescriptor.CustomAnimLayer[] layers)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    if (layer.type == type)
                    {
                        if (layer.animatorController == null || layer.isDefault)
                        {
                            layer.animatorController = ResolveLayerController(layer);
                            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture)
                            {
                                layer.mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                                    AssetDatabase.GUIDToAssetPath(GUID_GESTURE_HANDSONLY_MASK)
                                );
                            }

                            layers[i] = layer;
                        }

                        return layer.animatorController as AnimatorController;
                    }
                }

                return null;
            }
        }
        
        
        private static AnimatorController ResolveLayerController(VRCAvatarDescriptor.CustomAnimLayer layer)
        {
            AnimatorController controller = null;

            if (!layer.isDefault && layer.animatorController != null &&
                layer.animatorController is AnimatorController c)
            {
                controller = c;
            }
            else
            {
                string name;
                switch (layer.type)
                {
                    case VRCAvatarDescriptor.AnimLayerType.Action:
                        name = "Action";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Additive:
                        name = "Idle";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Base:
                        name = "Locomotion";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Gesture:
                        name = "Hands";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Sitting:
                        name = "Sitting";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.FX:
                        name = "Face";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.TPose:
                        name = "UtilityTPose";
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.IKPose:
                        name = "UtilityIKPose";
                        break;
                    default:
                        name = null;
                        break;
                }

                if (name != null)
                {
                    name = "/vrc_AvatarV3" + name + "Layer.controller";

                    controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_PACKAGE + name);
                    if (controller == null)
                    {
                        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SAMPLE_PATH_LEGACY + name);
                    }
                }
            }

            return controller;
        }
    }
}