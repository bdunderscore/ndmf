using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.localization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace nadena.dev.ndmf.VRChat
{
    [DependsOnContext(typeof(AnimatorServicesContext))]
    internal class CheckMipStreamingPass : Pass<CheckMipStreamingPass>
    {
        internal void TestExecute(BuildContext context)
        {
            Execute(context);
        }
        
        protected override void Execute(BuildContext context)
        {
            var examined = new HashSet<Object>();
            List<Texture> warningTextures = new();
            
            var asc = context.Extension<AnimatorServicesContext>();

            // Identify all referenced materials
            var materials = context.AvatarRootObject.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Concat(
                    asc.AnimationIndex.GetPPtrReferencedObjects.OfType<Material>()
                )
                .Distinct()
                .ToList();
            
            // reuse the list across loops to reduce GC activity
            List<int> texNamePropIds = new();

            foreach (var mat in materials)
            {
                if (mat?.shader == null) continue;
                if (!examined.Add(mat)) continue;

                texNamePropIds.Clear();
                mat.GetTexturePropertyNameIDs(texNamePropIds);
                foreach (var prop in texNamePropIds)
                {
                    try
                    {
                        var tex = mat.GetTexture(prop);
                        if (tex == null) continue;

                        if (!examined.Add(tex)) continue;

                        if (tex.mipmapCount <= 1) continue;

                        var sTexture = new SerializedObject(tex);
                        var sStreamingMipmaps = sTexture.FindProperty("m_StreamingMipmaps");
                        if (sStreamingMipmaps?.boolValue == false)
                        {
                            var path = AssetDatabase.GetAssetPath(tex);
                            var isPersistent = EditorUtility.IsPersistent(tex);
                            var invalidPath = string.IsNullOrEmpty(path)
                                              || !(path.StartsWith("Assets/") || path.StartsWith("Packages/"));

                            if (isPersistent && invalidPath)
                            {
                                // Might be a built-in texture
                                continue;
                            }
                            warningTextures.Add(tex);
                        }
                    }
                    catch (Exception)
                    {
                        // Don't break the build
                        continue;
                    }
                }
            }

            var persistentWarningTextures = new List<Texture>();
            var temporaryWarningTextures = new List<Texture>();
            foreach (var tex in warningTextures)
            {
                if (context.IsTemporaryAsset(tex))
                {
                    temporaryWarningTextures.Add(tex);
                }
                else
                {
                    persistentWarningTextures.Add(tex);
                }
            }

            if (temporaryWarningTextures.Count > 0)
            {
                ErrorReport.ReportError(NDMFLocales.L, ErrorSeverity.NonFatal, "Errors:MipStreamingMissingOnTempAsset",
                    temporaryWarningTextures
                );
            }

            if (persistentWarningTextures.Count > 0)
            {
                Action autofix = () =>
                {
                    foreach (var tex in persistentWarningTextures)
                    {
                        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter importer)
                        {
                            Undo.RecordObject(importer, "Set mipmap streaming on texture");
                            importer.streamingMipmaps = true;
                            EditorUtility.SetDirty(importer);
                            importer.SaveAndReimport();
                        }
                    }
                };
                ErrorReport.ReportError(new InlineErrorWithAutofix(
                    autofix,
                    NDMFLocales.L, ErrorSeverity.NonFatal, "Errors:MipStreamingMissingOnAsset",
                    persistentWarningTextures
                ));
            }
        }
    }
}