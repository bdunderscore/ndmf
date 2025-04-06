﻿#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using MonoMod.Utils;
using UnityEngine;

namespace nadena.dev.ndmf.platform
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal sealed class CommonAvatarInfo
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
        
        public Vector3? EyePosition { get; set; }

        public SkinnedMeshRenderer? VisemeRenderer { get; set; }
        public readonly Dictionary<string, string> VisemeBlendshapes = new();


        public void MergeFrom(CommonAvatarInfo other)
        {
            EyePosition ??= other.EyePosition;
            VisemeRenderer ??= other.VisemeRenderer;
            VisemeBlendshapes.AddRange(other.VisemeBlendshapes);
        }
    }
}