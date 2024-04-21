#region

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf.util
{
    /// <summary>
    /// This class provides utility methods for textures.
    /// </summary>
    public static class TextureUtility
    {
        private static readonly HashSet<TextureFormat> UncompressedFormats = new HashSet<TextureFormat>
        {
            TextureFormat.Alpha8,
            TextureFormat.ARGB32,
            TextureFormat.ARGB4444,
            TextureFormat.BGRA32,
            TextureFormat.R16,
            TextureFormat.R8,
            TextureFormat.RFloat,
            TextureFormat.RG16,
            TextureFormat.RG32,
            TextureFormat.RGB24,
            TextureFormat.RGB48,
            TextureFormat.RGB565,
            TextureFormat.RGB9e5Float,
            TextureFormat.RGBA32,
            TextureFormat.RGBA4444,
            TextureFormat.RGBA64,
            TextureFormat.RGBAFloat,
            TextureFormat.RGBAHalf,
            TextureFormat.RGFloat,
            TextureFormat.RGHalf,
            TextureFormat.RHalf,
            TextureFormat.YUY2
        };

        // https://docs.unity3d.com/2022.3/Documentation/Manual/class-TextureImporterOverride.html
        private static readonly HashSet<TextureFormat> WindowsFormats = new HashSet<TextureFormat>
        {
            TextureFormat.BC4,
            TextureFormat.BC5,
            TextureFormat.BC6H,
            TextureFormat.BC7,
            TextureFormat.DXT1,
            TextureFormat.DXT1Crunched,
            TextureFormat.DXT5,
            TextureFormat.DXT5Crunched,
        };

        // https://docs.unity3d.com/2022.3/Documentation/Manual/class-TextureImporterOverride.html
        // Contains "partial" formats to simplify.
        private static readonly HashSet<TextureFormat> AndroidFormats = new HashSet<TextureFormat>
        {
            TextureFormat.ASTC_4x4,
            TextureFormat.ASTC_5x5,
            TextureFormat.ASTC_6x6,
            TextureFormat.ASTC_8x8,
            TextureFormat.ASTC_10x10,
            TextureFormat.ASTC_12x12,
            TextureFormat.ASTC_HDR_4x4,
            TextureFormat.ASTC_HDR_5x5,
            TextureFormat.ASTC_HDR_6x6,
            TextureFormat.ASTC_HDR_8x8,
            TextureFormat.ASTC_HDR_10x10,
            TextureFormat.ASTC_HDR_12x12,
            TextureFormat.ETC2_RGB,
            TextureFormat.ETC2_RGBA1,
            TextureFormat.ETC2_RGBA8,
            TextureFormat.ETC2_RGBA8Crunched,
            TextureFormat.ETC_RGB4,
            TextureFormat.ETC_RGB4Crunched,
            TextureFormat.EAC_R,
            TextureFormat.EAC_R_SIGNED,
            TextureFormat.EAC_RG,
            TextureFormat.EAC_RG_SIGNED,
        };

        /// <summary>
        /// Check if a texture format is for uncompressed.
        /// </summary>
        /// <param name="format"></param>
        /// <returns>true when the format is uncompressed format.</returns>
        public static bool IsUncompressedFormat(TextureFormat format)
        {
            return UncompressedFormats.Contains(format);
        }

        public static bool IsSupportedFormat(TextureFormat format, BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return WindowsFormats.Contains(format) || IsUncompressedFormat(format);
                case BuildTarget.Android:
                    return AndroidFormats.Contains(format) || IsUncompressedFormat(format);
                default:
                    throw new System.NotSupportedException($"Unsupported build target: {buildTarget}");
            }
        }
    }
}
