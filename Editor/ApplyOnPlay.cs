/*
 * MIT License
 *
 * Copyright (c) 2022 bd_
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR // workaround issues with docfx

using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;

namespace nadena.dev.ndmf
{
    [InitializeOnLoad]
    internal static class ApplyOnPlay
    {
        private const string MENU_NAME = "Tools/Avatar Toolkit/Apply on Play";

        /**
         * We need to process avatars before lyuma's av3 emulator wakes up and processes avatars; it does this in Awake,
         * so we have to do our processing in Awake as well. This seems to work fine when first entering play mode, but
         * if you subsequently enable an initially-disabled avatar, processing from within Awake causes an editor crash.
         *
         * To workaround this, we initially process in awake; then, after OnPlayModeStateChanged is invoked (ie, after
         * all initially-enabled components have Awake called), we switch to processing from Start instead.
         */
        private static ApplyOnPlayGlobalActivator.OnDemandSource armedSource =
            ApplyOnPlayGlobalActivator.OnDemandSource.Awake;

        static ApplyOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ApplyOnPlayGlobalActivator.OnDemandProcessAvatar = MaybeProcessAvatar;
            //EditorApplication.delayCall += () => Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        private static void MaybeProcessAvatar(ApplyOnPlayGlobalActivator.OnDemandSource source,
            MonoBehaviour component)
        {
            if (Settings.ApplyOnPlay && source == armedSource && component != null)
            {
                var avatar = RuntimeUtil.FindAvatarInParents(component.transform);
                if (avatar == null) return;
                AvatarProcessor.ProcessAvatar(avatar.gameObject);
            }
        }

        [MenuItem(MENU_NAME)]
        private static void ToggleApplyOnPlay()
        {
            //ModularAvatarSettings.applyOnPlay = !ModularAvatarSettings.applyOnPlay;
            //Menu.SetChecked(MENU_NAME, ModularAvatarSettings.applyOnPlay);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                armedSource = ApplyOnPlayGlobalActivator.OnDemandSource.Start;
            }
            else if (obj == PlayModeStateChange.EnteredEditMode)
            {
                armedSource = ApplyOnPlayGlobalActivator.OnDemandSource.Awake;
                AvatarProcessor.CleanTemporaryAssets();
            }
        }
    }
}
#endif