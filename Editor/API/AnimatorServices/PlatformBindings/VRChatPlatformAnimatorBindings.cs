﻿#if NDMF_VRCSDK3_AVATARS
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     Platform bindings for VRChat.
    /// </summary>
    public sealed class VRChatPlatformAnimatorBindings : IPlatformAnimatorBindings
    {
        public static readonly VRChatPlatformAnimatorBindings Instance = new();
        
        private const string SAMPLE_PATH_PACKAGE =
            "Packages/com.vrchat.avatars";

        private const string CONTROLLER_PATH_PACKAGE =
            "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers";

        private HashSet<Motion>? _specialMotions;

        private VRChatPlatformAnimatorBindings()
        {
        }

        private AnimatorController? GetFallbackController(VRCAvatarDescriptor.AnimLayerType ty)
        {
            string? name;
            switch (ty)
            {
                case VRCAvatarDescriptor.AnimLayerType.Action:
                    name = "ActionLayer";
                    break;
                case VRCAvatarDescriptor.AnimLayerType.Additive:
                    name = "IdleLayer";
                    break;
                case VRCAvatarDescriptor.AnimLayerType.Base:
                    name = "LocomotionLayer";
                    break;
                case VRCAvatarDescriptor.AnimLayerType.Gesture:
                    name = "HandsLayer";
                    break;
                case VRCAvatarDescriptor.AnimLayerType.Sitting:
                    name = "SittingLayer";
                    break;
                case VRCAvatarDescriptor.AnimLayerType.FX:
                    name = "FaceLayer";
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
                name = "/vrc_AvatarV3" + name + ".controller";

                return AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH_PACKAGE + name);
            }

            return null;
        }
        
        public bool IsSpecialMotion(Motion m)
        {
            if (_specialMotions == null)
            {
                // https://creators.vrchat.com/avatars/#proxy-animations
                _specialMotions = new HashSet<Motion>(
                    AssetDatabase.FindAssets("t:AnimationClip", new[] { SAMPLE_PATH_PACKAGE })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<Motion>)
                        .Where(asset => asset.name.StartsWith("proxy_"))
                );
            }

            return _specialMotions.Contains(m);
        }

        public IEnumerable<(object, RuntimeAnimatorController, bool)> GetInnateControllers(GameObject root)
        {
            foreach (var result in GenericPlatformAnimatorBindings.Instance.GetInnateControllers(root))
            {
                yield return result;
            }
            
            if (!root.TryGetComponent<VRCAvatarDescriptor>(out var vrcAvatarDescriptor)) yield break;

            if (vrcAvatarDescriptor.baseAnimationLayers == null ||
                vrcAvatarDescriptor.baseAnimationLayers.All(l => l.isDefault))
            {
                // Initialize the VRChat avatar descriptor. Unfortunately the only way to do this is to run the editor for
                // it. Ick.
                var editor = Editor.CreateEditor(vrcAvatarDescriptor);
                var onEnable = AccessTools.Method(editor.GetType(), "OnEnable");
                onEnable?.Invoke(editor, null);
                Object.DestroyImmediate(editor);
            }

            // Make sure customizeAnimationLayers is set if we think they've been customized - otherwise the SDK
            // likes to reset them automatically.
            vrcAvatarDescriptor.customizeAnimationLayers = true;
            
            // TODO: Fallback layers
            foreach (var layer in vrcAvatarDescriptor.baseAnimationLayers ??
                                  Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
            {
                var ac = layer.isDefault ? null : layer.animatorController;

                // Can't use ?? here as we sometimes have unity-null objects sneaking in...
                if (ac == null) ac = GetFallbackController(layer.type);

                if (ac == null) continue;

                yield return (layer.type, ac, layer.isDefault);
            }

            foreach (var layer in vrcAvatarDescriptor.specialAnimationLayers ??
                                  Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>())
            {
                var ac = layer.isDefault ? null : layer.animatorController;

                // Can't use ?? here as we sometimes have unity-null objects sneaking in...
                if (ac == null) ac = GetFallbackController(layer.type);

                if (ac == null) continue;

                yield return (layer.type, ac, layer.isDefault);
            }
        }

        public void CommitControllers(
            GameObject root,
            IDictionary<object, RuntimeAnimatorController> controllers
        )
        {
            if (!root.TryGetComponent<VRCAvatarDescriptor>(out var vrcAvatarDescriptor)) return;

            EditLayers(vrcAvatarDescriptor.baseAnimationLayers);
            EditLayers(vrcAvatarDescriptor.specialAnimationLayers);

            GenericPlatformAnimatorBindings.Instance.CommitControllers(root, controllers);

            vrcAvatarDescriptor.customizeAnimationLayers = true;

            void EditLayers(VRCAvatarDescriptor.CustomAnimLayer[] layers)
            {
                for (var i = 0; i < layers.Length; i++)
                {
                    if (controllers.TryGetValue(layers[i].type, out var controller))
                    {
                        layers[i].animatorController = controller;
                        layers[i].isDefault = false;
                    }
                }
            }
        }
        
        public void VirtualizeStateBehaviour(CloneContext context, StateMachineBehaviour behaviour)
        {
            var key = context.ActiveInnateLayerKey;

            switch (behaviour)
            {
                case VRCAnimatorLayerControl alc:
                    // null or equals
                    if (key?.Equals(ConvertLayer(alc.playable)) != false)
                    {
                        alc.layer = context.CloneSourceToVirtualLayerIndex(alc.layer);
                    }

                    break;
            }
        }

        public bool CommitStateBehaviour(CommitContext context, StateMachineBehaviour behaviour)
        {
            var key = context.ActiveInnateLayerKey;

            if (key is IVirtualizeAnimatorController vac)
            {
                key = vac.TargetControllerKey;
            }

            switch (behaviour)
            {
                case VRCAnimatorLayerControl alc:
                    if (key?.Equals(ConvertLayer(alc.playable)) != false)
                    {
                        alc.layer = context.VirtualToPhysicalLayerIndex(alc.layer);
                    }

                    return alc.layer != -1;
            }

            return true;
        }

        public void RemapPathsInStateBehaviour(StateMachineBehaviour behaviour, Func<string, string?> remapPath)
        {
            if (behaviour is VRCAnimatorPlayAudio playAudio)
            {
                playAudio.SourcePath = remapPath(playAudio.SourcePath) ?? "";
            }
        }

        private object ConvertLayer(VRC_AnimatorLayerControl.BlendableLayer playable)
        {
            switch (playable)
            {
                case VRC_AnimatorLayerControl.BlendableLayer.Action: return VRCAvatarDescriptor.AnimLayerType.Action;
                case VRC_AnimatorLayerControl.BlendableLayer.Additive:
                    return VRCAvatarDescriptor.AnimLayerType.Additive;
                case VRC_AnimatorLayerControl.BlendableLayer.FX: return VRCAvatarDescriptor.AnimLayerType.FX;
                case VRC_AnimatorLayerControl.BlendableLayer.Gesture: return VRCAvatarDescriptor.AnimLayerType.Gesture;
                default: throw new ArgumentOutOfRangeException("Unknown blendable layer type: " + playable);
            }
        }
    }
}
#endif