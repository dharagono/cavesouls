using UnityEditor;
using UnityEngine;

namespace CaveSouls.Core.Editor
{
    /// <summary>
    /// Automatically configures imported Souls-like FBX assets and PBR textures under Assets/_Project/Art/:
    /// - Converts child objects prefixed with "UCX_" into convex MeshColliders and removes their MeshRenderer.
    /// - Configures LODGroup when _LOD0, _LOD1, and _LOD2 are present.
    /// - Sets Normal Maps (*_Normal*) to TextureImporterType.NormalMap and Mask Maps (*_MaskMap*) to linear (sRGB = false).
    /// </summary>
    public sealed class SoulsModelImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            TextureImporter importer = (TextureImporter)assetImporter;

            if (assetPath.Contains("Assets/_Project/Art/UI/"))
            {
                if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize < 4096)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 4096;
                }
                return;
            }

            if (assetPath.Contains("Assets/_Project/Art/Textures/"))
            {
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;

                if (assetPath.Contains("_Normal"))
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                }
                else if (assetPath.Contains("_MaskMap"))
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                }
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Contains("Assets/_Project/Art/Models/")) return;

            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            Renderer lod0Renderer = null;
            Renderer lod1Renderer = null;
            Renderer lod2Renderer = null;

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform tr = allTransforms[i];
                if (tr == null) continue;

                if (tr.name.StartsWith("UCX_"))
                {
                    if (tr.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    {
                        MeshCollider mc = tr.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }

                    if (tr.TryGetComponent(out MeshRenderer mr))
                    {
                        Object.DestroyImmediate(mr);
                    }
                }
                else if (tr.name.EndsWith("_LOD0"))
                {
                    tr.TryGetComponent(out lod0Renderer);
                }
                else if (tr.name.EndsWith("_LOD1"))
                {
                    tr.TryGetComponent(out lod1Renderer);
                }
                else if (tr.name.EndsWith("_LOD2"))
                {
                    tr.TryGetComponent(out lod2Renderer);
                }
            }

            if (lod0Renderer != null && lod1Renderer != null && lod2Renderer != null)
            {
                if (!root.TryGetComponent(out LODGroup lodGroup))
                {
                    lodGroup = root.AddComponent<LODGroup>();
                }

                LOD[] lods = new LOD[3];
                lods[0] = new LOD(0.50f, new[] { lod0Renderer });
                lods[1] = new LOD(0.20f, new[] { lod1Renderer });
                lods[2] = new LOD(0.03f, new[] { lod2Renderer });
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }
    }
}
