#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace nadena.dev.ndmf.animator
{
    /// <summary>
    ///     The IPlatformAnimatorBindings interface is used to provide platform-specific bindings for the animator service.
    ///     This is used, for example, to identify which animator controllers are referenced by the avatar's
    ///     platform-specific components, and to process platform-specific state behaviours.
    /// </summary>
    public interface IPlatformAnimatorBindings
    {
        /// <summary>
        ///     If true, the motion asset should be maintained as-is without replacement or modification.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        bool IsSpecialMotion(Motion m)
        {
            return false;
        }

        /// <summary>
        ///     Returns any animator controllers that are referenced by platform-specific assets (e.g. VRCAvatarDescriptor).
        ///     The bool flag indicates whether the controller is overridden (true) or left as default (false).
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        IEnumerable<(object, RuntimeAnimatorController, bool)> GetInnateControllers(GameObject root)
        {
            return Array.Empty<(object, RuntimeAnimatorController, bool)>();
        }

        /// <summary>
        ///     Updates any innate controllers to reference new animator controllers.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="controllers"></param>
        void CommitControllers(GameObject root, IDictionary<object, RuntimeAnimatorController> controllers)
        {
            
        }

        /// <summary>
        /// Invoked after a StateMachineBehavior is cloned, to allow for any platform-specific modifications.
        /// For example, in VRChat, this is used to replace the layer indexes with virtual layer indexes in the
        /// VRChatAnimatorLayerControl behavior.
        /// 
        /// Note that, if we're re-activating the virtual animator controller after committing, this will be re-invoked
        /// with the same behaviour it had previously cloned. This allows for again converting between virtual and
        /// physical layer indexes.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="behaviour"></param>
        void VirtualizeStateBehaviour(CloneContext context, StateMachineBehaviour behaviour)
        {
        }

        /// <summary>
        /// Invoked when a StateMachineBehavior is being committed, to allow for any platform-specific modifications.
        /// For example, in VRChat, this is used to replace the virtual layer indexes with the actual layer indexes in the
        /// VRChatAnimatorLayerControl behavior.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="behaviour"></param>
        /// <returns>True to retain this state behavior, false to remove it on commit</returns>
        bool CommitStateBehaviour(CommitContext context, StateMachineBehaviour behaviour)
        {
            return true;
        }

        /// <summary>
        ///     Invoked when path remappings are being processed to apply any changed mappings to state behaviors.
        ///     In VRChat, this is used to remap object paths in VRCAnimatorPlayAudio
        /// </summary>
        /// <param name="behaviour">The behaviour to remap</param>
        /// <param name="remapPath">
        ///     A function which remaps old paths to new paths (or to null, if the corresponding object was
        ///     deleted)
        /// </param>
        void RemapPathsInStateBehaviour(StateMachineBehaviour behaviour, Func<string, string?> remapPath)
        {
        }
    }
}