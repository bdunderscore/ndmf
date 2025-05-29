#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using MonoMod.Utils;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    /// <summary>
    /// The CommonAvatarInfo structure provides an intermediate representation of frequently-used avatar-wide
    /// configuration in order to facilitate automatic conversion between different types of SDKs.
    /// </summary>
    [PublicAPI]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public sealed class CommonAvatarInfo
    {
        public const string Viseme_Silence = "silence";
        public const string Viseme_PP = "PP";
        public const string Viseme_FF = "FF";
        public const string Viseme_TH = "TH";
        public const string Viseme_DD = "DD";
        public const string Viseme_kk = "kk";
        public const string Viseme_CH = "CH";
        public const string Viseme_SS = "SS";
        public const string Viseme_nn = "nn";
        public const string Viseme_RR = "RR";
        public const string Viseme_aa = "aa";
        public const string Viseme_E = "E";
        public const string Viseme_ih = "ih";
        public const string Viseme_oh = "oh";
        public const string Viseme_ou = "ou";
        public const string Viseme_laugh = "laugh";

        public readonly static ImmutableList<string> KnownVisemes = ImmutableList.Create(new string[]
        {
            Viseme_Silence,
            Viseme_PP,
            Viseme_FF,
            Viseme_TH,
            Viseme_DD,
            Viseme_kk,
            Viseme_CH,
            Viseme_SS,
            Viseme_nn,
            Viseme_RR,
            Viseme_aa,
            Viseme_E,
            Viseme_ih,
            Viseme_oh,
            Viseme_ou,
            Viseme_laugh,
        });
        
        /// <summary>
        /// The position of the user viewpoint in avatar root space. We assume a Z+ orientation.
        /// </summary>
        public Vector3? EyePosition { get; set; }

        /// <summary>
        /// The skinned mesh renderer that will be used to render viseme blendshapes, typically the face mesh.
        /// </summary>
        public SkinnedMeshRenderer? VisemeRenderer { get; set; }
        /// <summary>
        /// A dictionary of viseme key to viseme blendshape name.
        /// </summary>
        public readonly Dictionary<string, string> VisemeBlendshapes = new();
        
        /// <summary>
        /// Copies settings from `other` into this CommonAvatarInfo. If settings are present in both, `other` takes
        /// precedence.
        /// </summary>
        /// <param name="other"></param>
        public void MergeFrom(CommonAvatarInfo other)
        {
            EyePosition = other.EyePosition ?? EyePosition;
            VisemeRenderer = other.VisemeRenderer ?? VisemeRenderer;
            foreach ((var k, var v) in other.VisemeBlendshapes)
            {
                if (k != null && v != null) VisemeBlendshapes[k] = v;
            }
        }
    }
}