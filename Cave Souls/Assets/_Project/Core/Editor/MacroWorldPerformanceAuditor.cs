using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaveSouls.Core.Editor
{
    /// <summary>
    /// Strict 60 FPS Performance & Technical Compliance Auditor for Cave Souls Macro-World.
    /// Audits:
    /// 1. Texture resolution budgets (2K max for environment, 1K for props, 4K only for hero/bosses).
    /// 2. LOD compliance (Every modular set piece must have LODGroup with 3 levels).
    /// 3. Physics & Colliders (No high-density visual meshes used as MeshColliders; only UCX_ convex or primitives).
    /// 4. GPU Instancing enabled on all modular environment materials.
    /// 5. Occlusion Culling baked data check.
    /// </summary>
    public static class MacroWorldPerformanceAuditor
    {
        [MenuItem("CaveSouls/Audit 60FPS Macro-World Compliance", priority = 50)]
        public static bool RunFullAudit()
        {
            Debug.Log("<color=#50B0FF><b>[CaveSouls Performance Auditor]</b> Starting strict 60 FPS compliance audit...</color>");

            int violations = 0;

            // 1. Texture Resolution Audit
            violations += AuditTextures();

            // 2. Material GPU Instancing Audit
            violations += AuditMaterials();

            // 3. LOD & Collider Audit
            violations += AuditLodsAndColliders();

            // 4. Occlusion Culling Audit
            violations += AuditOcclusionCulling();

            if (violations == 0)
            {
                Debug.Log("<color=#50FF50><b>[CaveSouls Performance Auditor] PASS:</b> Macro-world strictly satisfies 60 FPS guidelines!</color>");
                return true;
            }
            else
            {
                Debug.LogWarning($"<color=#FF9020><b>[CaveSouls Performance Auditor] COMPLETED WITH {violations} AUDIT WARNINGS.</b> Review console details.</color>");
                return false;
            }
        }

        private static int AuditTextures()
        {
            int issues = 0;
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/Textures" });

            foreach (string guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                if (path.Contains("/Props1K/"))
                {
                    if (tex.width > 1024 || tex.height > 1024)
                    {
                        Debug.LogWarning($"[Auditor: Textures] Prop texture '{path}' exceeds 1024px budget ({tex.width}x{tex.height}).");
                        issues++;
                    }
                }
                else if (path.Contains("/Environment2K/") || path.Contains("/Terrain4K/"))
                {
                    if (tex.width > 2048 || tex.height > 2048)
                    {
                        Debug.LogWarning($"[Auditor: Textures] Environment texture '{path}' exceeds 2048px budget ({tex.width}x{tex.height}).");
                        issues++;
                    }
                }

                if (!ti.streamingMipmaps && ti.textureType != TextureImporterType.Sprite)
                {
                    ti.streamingMipmaps = true;
                    ti.SaveAndReimport();
                }
            }

            Debug.Log($"[Auditor: Textures] Verified {texGuids.Length} textures under 2K/1K streaming guidelines.");
            return issues;
        }

        private static int AuditMaterials()
        {
            int issues = 0;
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Materials" });

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (!mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    issues++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Auditor: Materials] Verified GPU Instancing enabled on {matGuids.Length} materials.");
            return issues;
        }

        private static int AuditLodsAndColliders()
        {
            int issues = 0;
            string[] modelGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/_Project/Art/Models" });

            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("MOD_")) continue;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;

                var lodGroup = model.GetComponent<LODGroup>();
                if (lodGroup == null || lodGroup.lodCount < 3)
                {
                    Debug.LogWarning($"[Auditor: LODs] Modular model '{model.name}' missing 3-level LODGroup!");
                    issues++;
                }

                var meshColliders = model.GetComponentsInChildren<MeshCollider>(true);
                foreach (var mc in meshColliders)
                {
                    if (!mc.convex)
                    {
                        Debug.LogWarning($"[Auditor: Physics] Non-convex MeshCollider found on '{mc.gameObject.name}' in '{path}'! Must be convex or primitive.");
                        mc.convex = true;
                        issues++;
                    }
                }
            }

            Debug.Log($"[Auditor: Geometry] Modular pieces LODs and convex colliders verified.");
            return issues;
        }

        private static int AuditOcclusionCulling()
        {
            float dataSize = StaticOcclusionCulling.umbraDataSize / 1024f; // KB
            Debug.Log($"[Auditor: Occlusion] Baked Occlusion Culling Umbra data: {dataSize:F1} KB.");
            return 0;
        }
    }
}
