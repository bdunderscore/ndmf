#nullable enable

using System;
using System.Reflection;
using HarmonyLib;
using nadena.dev.ndmf.model;
using nadena.dev.ndmf.platform;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace nadena.dev.ndmf.vrchat
{
    [NDMFPlatformProvider]
    internal sealed class VRChatPlatform : INDMFPlatformProvider
    {
        [InitializeOnLoadMethod]
        private static void Init()
        {
            // TODO - need a way to save defaults
            AmbientPlatform.DefaultPlatform = Instance;
        }

        public static INDMFPlatformProvider Instance { get; } = new VRChatPlatform();

        private readonly MethodInfo? VRCSDK_ShowControlPanel;

        private VRChatPlatform()
        {
            VRCSDK_ShowControlPanel = AccessTools.Method(typeof(VRCSdkControlPanel), "ShowControlPanel");
        }

        public string QualifiedName => WellKnownPlatforms.VRChatAvatar30;
        public string DisplayName => "VRChat";
        public Texture2D Icon => null;
        public Type AvatarRootComponentType => typeof(VRCAvatarDescriptor);

        public bool HasNativeUI => VRCSDK_ShowControlPanel != null;

        public void OpenNativeUI()
        {
            VRCSDK_ShowControlPanel?.Invoke(null, null);
        }
        
        
        public CommonAvatarInfo ExtractCommonAvatarInfo(GameObject avatarRoot)
        {
            var vrcAvDesc = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            var cai = new CommonAvatarInfo();
            cai.EyePosition = vrcAvDesc.ViewPosition;
            cai.VisemeRenderer = vrcAvDesc.VisemeSkinnedMesh;
            if (cai.VisemeRenderer != null)
            {
                var names = Enum.GetNames(typeof(VRC_AvatarDescriptor.Viseme));
                for (int i = 0; i < names.Length - 1; i++)
                {
                    var name = names[i];
                    if (i == 0) name = CommonAvatarInfo.Viseme_Silence;
                    
                    if (vrcAvDesc.VisemeBlendShapes[i] != null)
                    {
                        cai.VisemeBlendshapes.Add(name, vrcAvDesc.VisemeBlendShapes[i]);
                    }
                }
            }

            return cai;
        }

        public void InitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo cai)
        {
            if (!avatarRoot.TryGetComponent<VRCAvatarDescriptor>(out var vrcAvDesc))
            {
                vrcAvDesc = avatarRoot.AddComponent<VRCAvatarDescriptor>();
            }

            if (cai.EyePosition != null)
            {
                vrcAvDesc.ViewPosition = cai.EyePosition.Value;
            }

            if (cai.VisemeRenderer != null)
            {
                vrcAvDesc.VisemeSkinnedMesh = cai.VisemeRenderer;
            }

            if (vrcAvDesc.VisemeBlendShapes.Length < (int)VRC_AvatarDescriptor.Viseme.Count)
            {
                vrcAvDesc.VisemeBlendShapes = new string[(int)VRC_AvatarDescriptor.Viseme.Count];
            }

            for (int i = 0; i < (int)VRC_AvatarDescriptor.Viseme.Count; i++)
            {
                var name = i == 0 ? CommonAvatarInfo.Viseme_Silence : ((VRC_AvatarDescriptor.Viseme)i).ToString();
                
                if (cai.VisemeBlendshapes.TryGetValue(name, out var blendshape))
                {
                    vrcAvDesc.VisemeBlendShapes[i] = blendshape;
                }
            }
        }

        public bool CanInitFromCommonAvatarInfo(GameObject avatarRoot, CommonAvatarInfo info)
        {
            return true;
        }
        
        // This logic lives in the multiplatform package for now
        // ReSharper disable once MemberCanBePrivate.Global
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        internal static Action<GameObject, bool> GeneratePortableComponentsImpl = (_, _) => { };

        public void GeneratePortableComponents(GameObject avatarRoot, bool registerUndo)
        {
            GeneratePortableComponentsImpl(avatarRoot, registerUndo);
        }
    }

    internal static class VRChatContextExtensions
    {
        public static VRCAvatarDescriptor VRChatAvatarDescriptor(this BuildContext context)
        {
            return context.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
        }
    }
}