#if NDMF_VRCSDK3_AVATARS

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.VRChat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnitTests.MipStreamingChecks
{
    [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
    public class CheckMipStreamingPassTests : TestBase
    {
        private const string TestDir = "Packages/nadena.dev.ndmf/UnitTests/MipStreamingChecks/";

        private List<ErrorContext> RunPassAndCapture(GameObject avatar)
        {
            var errors = ErrorReport.CaptureErrors(() =>
            {
                var ctx = CreateContext(avatar);
                ctx.ActivateExtensionContextRecursive<AnimatorServicesContext>();
                CheckMipStreamingPass.Instance.TestExecute(ctx);
            });

            return errors;
        }

        [Test]
        public void NoErrorWhenMipStreamingOn()
        {
            var root = CreateRoot("mip-on-root");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TestDir + "mip-streaming-on.png");
            Assert.NotNull(tex, "Test texture mip-streaming-on.png missing");

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();
            var mat = TrackObject(new Material(Shader.Find("Standard")));
            mat.SetTexture("_MainTex", tex);
            mr.sharedMaterial = mat;

            var errors = RunPassAndCapture(root);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void ErrorWhenAssetTextureMissingMip()
        {
            var root = CreateRoot("mip-off-root");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TestDir + "mip-streaming-off.png");
            Assert.NotNull(tex, "Test texture mip-streaming-off.png missing");

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();
            var mat = TrackObject(new Material(Shader.Find("Standard")));
            mat.SetTexture("_MainTex", tex);
            mr.sharedMaterial = mat;

            var errors = RunPassAndCapture(root);

            Assert.IsNotEmpty(errors);
            Assert.IsTrue(errors.Any(e => ((SimpleError)e.TheError).TitleKey == "Errors:MipStreamingMissingOnAsset"),
                "Expected an error tagged Errors:MipStreamingMissingOnAsset");
        }

        [Test]
        public void ErrorWhenTempTextureMissingMip()
        {
            var root = CreateRoot("mip-off-temp-root");

            var tempTex = TrackObject(new Texture2D(2, 2));

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();
            var mat = TrackObject(new Material(Shader.Find("Standard")));
            mat.SetTexture("_MainTex", tempTex);
            mr.sharedMaterial = mat;

            var errors = RunPassAndCapture(root);

            Assert.IsNotEmpty(errors);
            Assert.IsTrue(errors.Any(e => ((SimpleError)e.TheError).TitleKey == "Errors:MipStreamingMissingOnTempAsset"),
                "Expected an error tagged Errors:MipStreamingMissingOnTempAsset");
        }

        [Test]
        public void NoCrashWithNullMaterialOr3DTexture()
        {
            var root = CreateRoot("mip-null-and-3d-root");

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();

            // Null material slot
            mr.sharedMaterials = new Material[] { null };

            // Material with a runtime Texture3D - use the Texture3DTest shader so the shader declares a 3D sampler
            var mat = TrackObject(new Material(Shader.Find("Hidden/NDMF/Texture3DTest")));
            var tex3d = TrackObject(new Texture3D(2, 2, 2, TextureFormat.RGBA32, false));
            mat.SetTexture("_VolumeTex", tex3d);

            // assign second material (renderer supports multiple slots)
            mr.sharedMaterials = new[] { null, mat };

            // Should not throw
            Assert.DoesNotThrow(() => RunPassAndCapture(root));
        }

        [Test]
        public void RenderTextureWithAndWithoutMipmaps_DoesNotError()
        {
            var root = CreateRoot("mip-rendertexture-root");

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();

            // Test both with mipmaps enabled and disabled
            foreach (var useMips in new[] { true, false })
            {
                var rt = new RenderTexture(16, 16, 0)
                {
                    useMipMap = useMips,
                    autoGenerateMips = useMips
                };
                TrackObject(rt);
                rt.Create();

                var mat = TrackObject(new Material(Shader.Find("Standard")));
                mat.SetTexture("_MainTex", rt);
                mr.sharedMaterial = mat;

                var errors = RunPassAndCapture(root);
                Assert.IsEmpty(errors, $"RenderTexture with useMipMap={useMips} produced unexpected errors");
            }
        }

        [Test]
        public void NoErrorWhenIncludingNoMipsTestTexture()
        {
            var root = CreateRoot("mip-no-mips-root");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TestDir + "no-mips.png");
            Assert.NotNull(tex, "Test texture no-mips.png missing");

            var child = CreateChild(root, "r");
            var mr = child.AddComponent<MeshRenderer>();
            var mat = TrackObject(new Material(Shader.Find("Standard")));
            mat.SetTexture("_MainTex", tex);
            mr.sharedMaterial = mat;

            var errors = RunPassAndCapture(root);
            Assert.IsEmpty(errors);
        }
    }
}
#endif
