#region

using System;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase.Editor.BuildPipeline;

#endregion

namespace nadena.dev.ndmf.VRChat
{
    /// <summary>
    /// When domain reload is disabled, the VRChat physbones gizmo can leave outdated data in the VRCPhysBone component
    /// when entering play mode. Force reinitialize the components to work around this issue.
    ///
    /// Note that we only do this when entering play mode, as this is runtime state, not serialized state.
    /// </summary>
    [ExcludeFromDocs]
    [Obsolete(
        "This class was accidentally made public; do not reference it directly. It will be made private in a future release")]
    public class ForceReinitPhysBonesHook : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => int.MaxValue;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            if (Application.isPlaying)
            {
                foreach (var physBone in avatarGameObject.GetComponentsInChildren<VRCPhysBone>(true))
                {
                    physBone.InitTransforms(true);
                    physBone.InitParameters();
                }

                foreach (var collider in avatarGameObject.GetComponentsInChildren<VRCPhysBoneColliderBase>(true))
                {
                    collider.UpdateShape();
                }

                foreach (var contact in avatarGameObject.GetComponentsInChildren<ContactBase>(true))
                {
                    contact.UpdateShape();
                }
            }

            return true;
        }
    }
}