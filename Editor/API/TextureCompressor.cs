#region

using nadena.dev.ndmf.util;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#endregion

namespace nadena.dev.ndmf
{
    /// <summary>
    /// This class allows you to set a particular Texture Compressor instance as the current one to be used for static
    /// methods. This is primarily intended for unit testing.
    /// </summary>
    public sealed class TextureCompressorScope : IDisposable
    {
        private readonly TextureCompressor _oldCompressor;

        public TextureCompressorScope(TextureCompressor compressor)
        {
            _oldCompressor = TextureCompressor.ActiveCompressor;
            TextureCompressor.ActiveCompressor = compressor;
        }

        public void Dispose()
        {
            TextureCompressor.ActiveCompressor = _oldCompressor;
        }
    }

    internal struct TextureCompression
    {
        public TextureFormat Format;
        public TextureCompressionQuality Quality;
    }

    /// <summary>
    /// The TextureCompressor keeps compression settings of registered textures; this is used to compress textures when saving textures to AssetContainer.
    /// </summary>
    public sealed class TextureCompressor
    {
        // Reference texture => compression settings
        private readonly Dictionary<Texture2D, TextureCompression> _tex2compression =
            new Dictionary<Texture2D, TextureCompression>();

        private const TextureFormat DEFAULT_TEXTURE_FORMAT_PC = TextureFormat.DXT5;
        private const TextureFormat DEFAULT_TEXTURE_FORMAT_ANDROID = TextureFormat.ASTC_6x6;
        private const TextureCompressionQuality DEFAULT_COMPRESSION_QUALITY = TextureCompressionQuality.Best;

        static internal TextureCompressor ActiveCompressor;

        /// <summary>
        /// Register a texture to override texture format and quality for later compression.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="format"></param>
        /// <param name="quality"></param>
        public static void RegisterCompression(Texture2D tex, TextureFormat format, TextureCompressionQuality quality)
        {
            RegisterCompression(tex, format, quality, EditorUserBuildSettings.activeBuildTarget);
        }

        /// <summary>
        /// For internal use only.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="format"></param>
        /// <param name="quality"></param>
        /// <param name="buildTarget"></param>
        internal static void RegisterCompression(Texture2D tex, TextureFormat format, TextureCompressionQuality quality, BuildTarget buildTarget)
        {
            if (ActiveCompressor == null) return;

            if (tex == null) throw new NullReferenceException("tex must not be null");

            if (!TextureUtility.IsSupportedFormat(format, buildTarget))
            {
                throw new NotSupportedException($"Unsupported texture format for {buildTarget}: {format}");
            }

            ActiveCompressor._tex2compression[tex] = new TextureCompression()
            {
                Format = format,
                Quality = quality,
            };
        }

        /// <summary>
        /// Compress a texture with registered compression settings.
        /// </summary>
        /// <param name="tex"></param>
        /// <param name="buildTarget"></param>
        public static void CompressTexture(Texture2D tex, BuildTarget buildTarget)
        {
            if (ActiveCompressor == null) return;

            if (tex == null) throw new NullReferenceException("tex must not be null");

            if (!ActiveCompressor._tex2compression.TryGetValue(tex, out var compression))
            {
                var isAndroid = buildTarget == BuildTarget.Android;
                compression = new TextureCompression()
                {
                    Format = isAndroid ? DEFAULT_TEXTURE_FORMAT_ANDROID : DEFAULT_TEXTURE_FORMAT_PC,
                    Quality = DEFAULT_COMPRESSION_QUALITY
                };
            }
            EditorUtility.CompressTexture(tex, compression.Format, compression.Quality);
        }

        /// <summary>
        /// Check if a texture format is compressed.
        /// </summary>
        /// <param name="tex"></param>
        /// <returns>true when the format is for compression.</returns>
        public static bool IsCompressedTexture(Texture2D tex)
        {
            return !TextureUtility.IsUncompressedFormat(tex.format);
        }

        /// <summary>
        /// Check if a texture is explicitly uncompressed.
        /// </summary>
        /// <param name="tex"></param>
        /// <returns>true when the texture's format is for uncompression and the format is registered.</returns>
        public static bool IsExplicitlyUncompressedTexture(Texture2D tex)
        {
            if (IsCompressedTexture(tex)) return false;

            if (ActiveCompressor == null) throw new NullReferenceException("active compressor must not be null");

            if (tex == null) throw new NullReferenceException("tex must not be null");

            if (ActiveCompressor._tex2compression.TryGetValue(tex, out var compression))
            {
                return TextureUtility.IsUncompressedFormat(compression.Format);
            }
            return false;
        }
    }
}
