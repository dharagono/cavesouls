using System.Collections.Generic;
using System.IO;
using CaveSouls.Core;
using CaveSouls.Features.Audio;
using CaveSouls.Features.Player;
using CaveSouls.Features.UI;
using CaveSouls.Features.WorldAndProgression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CaveSouls.Core.Editor
{
    /// <summary>
    /// Master Macro-World Level Designer & Assembler for "Cave Souls" (03_PrimordialMudBed.unity).
    /// Constructs a continuous, vertically super-imposed interconnected world inspired by Dark Souls 1:
    /// - Layer 0: "El Lecho de Lodo Primordial" (Tutorial mud basin, lore remains, elevator shaft to Hub)
    /// - Layer 1: "El Santuario Neutral" (Central Hub, bonfire nexus, Apex Seal mechanism, 3 diverging paths, Layer 0 shortcut)
    /// - Zone A (Ascending): "Las Agujas de Piedra" (Spiral stone walkways, Puente de la Aguja at Y=+44m, Spire Lord Altar)
    /// - Zone B (Descending): "El Abismo y las Cavernas" (Chasm, wooden scaffolding, bioluminescent crystals, secret bridge to Zone C, Abyss Lord Altar)
    /// - Zone C (Labyrinthine): "La Ciudad de los Petrificados" (Gothic ruins, petrified corpses, City Lord Altar)
    /// - Layer 3: "El Ápice" (Cathedral colonnade under ceiling light, Boss Fog Gate, final boss arena)
    /// Enforces strict 60 FPS optimizations:
    /// - 2K max textures for environment, 1K for props, streaming mipmaps
    /// - 3-level LODs on all modular set pieces with convex UCX_ physics
    /// - Umbra Occlusion Culling baked for static cave geometry
    /// - GPU Instancing enabled on all environment materials
    /// - Hybrid Realtime/Baked lighting with Light Probes
    /// </summary>
    public static class PrimordialMudBedSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/03_PrimordialMudBed.unity";
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string ModelsDir = "Assets/_Project/Art/Models";
        private const string TexturesDir = "Assets/_Project/Art/Textures";
        private const string AudioDir = "Assets/_Project/Art/Audio/Gameplay";
        private const string SettingsDir = "Assets/_Project/Art/Settings";

        [MenuItem("CaveSouls/Build 03_PrimordialMudBed Scene")]
        public static void BuildScene()
        {
            EnsureDirectories();
            Configure4KPbrTextureImporters();
            CreateOrUpdatePbrMaterials(
                out Material mudMat,
                out Material rockMat,
                out Material petrifiedMat,
                out Material lumenGlowMat,
                out Material playerMat,
                out Material gothicMat,
                out Material woodMat,
                out Material crystalMat);

            VolumeProfile volumeProfile = CreateOrUpdateCavernVolumeProfile();
            AssembleSceneHierarchy(mudMat, rockMat, petrifiedMat, lumenGlowMat, playerMat, gothicMat, woodMat, crystalMat, volumeProfile);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaveSouls] Successfully assembled and saved 03_PrimordialMudBed macro-world!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists("Assets/_Project/Scenes")) Directory.CreateDirectory("Assets/_Project/Scenes");
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
            if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
        }

        private static void Configure4KPbrTextureImporters()
        {
            string mudDir = TexturesDir + "/Terrain4K";
            string rockDir = TexturesDir + "/Terrain4K";
            string env2kDir = TexturesDir + "/Environment2K/polyhaven-stone_brick_wall_001";
            string prop1kDir = TexturesDir + "/Props1K/polyhaven-wood_planks";

            // Normal Maps
            string[] normals =
            {
                mudDir + "/TEX_PrimordialMud_NormalGL.jpg",
                rockDir + "/TEX_CavernWallRock_NormalGL.jpg",
                env2kDir + "/stone_brick_wall_001_nor_gl_2k.jpg",
                prop1kDir + "/wood_planks_nor_gl_1k.jpg"
            };
            foreach (string p in normals)
            {
                if (File.Exists(p))
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti != null)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.sRGBTexture = false;
                        ti.maxTextureSize = p.Contains("wood_planks") ? 1024 : 2048;
                        ti.streamingMipmaps = true;
                        ti.SaveAndReimport();
                    }
                }
            }

            // Mask Maps
            string[] masks =
            {
                mudDir + "/TEX_PrimordialMud_MaskMap.png",
                rockDir + "/TEX_CavernWallRock_MaskMap.png",
                env2kDir + "/stone_brick_wall_001_MaskMap_2k.png",
                prop1kDir + "/wood_planks_MaskMap_1k.png"
            };
            foreach (string p in masks)
            {
                if (File.Exists(p))
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti != null)
                    {
                        ti.textureType = TextureImporterType.Default;
                        ti.sRGBTexture = false;
                        ti.maxTextureSize = p.Contains("wood_planks") ? 1024 : 2048;
                        ti.streamingMipmaps = true;
                        ti.SaveAndReimport();
                    }
                }
            }

            // Base Color Maps
            string[] baseColors =
            {
                mudDir + "/TEX_PrimordialMud_BaseColor.jpg",
                rockDir + "/TEX_CavernWallRock_BaseColor.jpg",
                env2kDir + "/stone_brick_wall_001_diff_2k.jpg",
                prop1kDir + "/wood_planks_diff_1k.jpg"
            };
            foreach (string p in baseColors)
            {
                if (File.Exists(p))
                {
                    var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti != null)
                    {
                        ti.textureType = TextureImporterType.Default;
                        ti.sRGBTexture = true;
                        ti.maxTextureSize = p.Contains("wood_planks") ? 1024 : 2048;
                        ti.streamingMipmaps = true;
                        ti.SaveAndReimport();
                    }
                }
            }
        }

        private static void CreateOrUpdatePbrMaterials(
            out Material mudMat,
            out Material rockMat,
            out Material petrifiedMat,
            out Material lumenGlowMat,
            out Material playerMat,
            out Material gothicMat,
            out Material woodMat,
            out Material crystalMat)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            string mudDir = TexturesDir + "/Terrain4K";
            string rockDir = TexturesDir + "/Terrain4K";
            string env2kDir = TexturesDir + "/Environment2K/polyhaven-stone_brick_wall_001";
            string prop1kDir = TexturesDir + "/Props1K/polyhaven-wood_planks";

            // 1. Mud Terrain Material
            string mudMatPath = MaterialsDir + "/MAT_URP_PrimordialMudBed.mat";
            mudMat = AssetDatabase.LoadAssetAtPath<Material>(mudMatPath);
            if (mudMat == null)
            {
                mudMat = new Material(litShader);
                AssetDatabase.CreateAsset(mudMat, mudMatPath);
            }
            mudMat.color = Color.white;
            mudMat.enableInstancing = true;
            var mudBase = AssetDatabase.LoadAssetAtPath<Texture2D>(mudDir + "/TEX_PrimordialMud_BaseColor.jpg");
            var mudNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(mudDir + "/TEX_PrimordialMud_NormalGL.jpg");
            var mudMask = AssetDatabase.LoadAssetAtPath<Texture2D>(mudDir + "/TEX_PrimordialMud_MaskMap.png");
            if (mudBase != null) mudMat.SetTexture("_BaseMap", mudBase);
            if (mudNormal != null)
            {
                mudMat.SetTexture("_BumpMap", mudNormal);
                mudMat.SetFloat("_BumpScale", 1.35f);
                mudMat.EnableKeyword("_NORMALMAP");
            }
            if (mudMask != null)
            {
                mudMat.SetTexture("_MetallicGlossMap", mudMask);
                mudMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mudMat.SetFloat("_Smoothness", 0.76f);
            mudMat.SetTextureScale("_BaseMap", Vector2.one);
            mudMat.SetTextureScale("_BumpMap", Vector2.one);

            // 2. Cavern Wall Rock Material
            string rockMatPath = MaterialsDir + "/MAT_URP_CavernWallRock.mat";
            rockMat = AssetDatabase.LoadAssetAtPath<Material>(rockMatPath);
            if (rockMat == null)
            {
                rockMat = new Material(litShader);
                AssetDatabase.CreateAsset(rockMat, rockMatPath);
            }
            rockMat.color = Color.white;
            rockMat.enableInstancing = true;
            var rockBase = AssetDatabase.LoadAssetAtPath<Texture2D>(rockDir + "/TEX_CavernWallRock_BaseColor.jpg");
            var rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(rockDir + "/TEX_CavernWallRock_NormalGL.jpg");
            var rockMask = AssetDatabase.LoadAssetAtPath<Texture2D>(rockDir + "/TEX_CavernWallRock_MaskMap.png");
            if (rockBase != null) rockMat.SetTexture("_BaseMap", rockBase);
            if (rockNormal != null)
            {
                rockMat.SetTexture("_BumpMap", rockNormal);
                rockMat.SetFloat("_BumpScale", 1.25f);
                rockMat.EnableKeyword("_NORMALMAP");
            }
            if (rockMask != null)
            {
                rockMat.SetTexture("_MetallicGlossMap", rockMask);
                rockMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            rockMat.SetFloat("_Smoothness", 0.40f);
            rockMat.SetTextureScale("_BaseMap", Vector2.one);
            rockMat.SetTextureScale("_BumpMap", Vector2.one);

            // 3. Petrified Mud Humanoid Material
            string petPath = MaterialsDir + "/MAT_URP_PetrifiedHumanoid.mat";
            petrifiedMat = AssetDatabase.LoadAssetAtPath<Material>(petPath);
            if (petrifiedMat == null)
            {
                petrifiedMat = new Material(litShader);
                AssetDatabase.CreateAsset(petrifiedMat, petPath);
            }
            petrifiedMat.color = new Color(0.18f, 0.17f, 0.16f, 1f);
            petrifiedMat.SetFloat("_Smoothness", 0.50f);
            petrifiedMat.enableInstancing = true;
            if (mudNormal != null)
            {
                petrifiedMat.SetTexture("_BumpMap", mudNormal);
                petrifiedMat.EnableKeyword("_NORMALMAP");
            }

            // 4. Player Explorer Material
            string playerMatPath = MaterialsDir + "/MAT_URP_PlayerExplorer.mat";
            playerMat = AssetDatabase.LoadAssetAtPath<Material>(playerMatPath);
            if (playerMat == null)
            {
                playerMat = new Material(litShader);
                AssetDatabase.CreateAsset(playerMat, playerMatPath);
            }
            playerMat.color = new Color(0.28f, 0.26f, 0.24f, 1f);
            playerMat.SetFloat("_Smoothness", 0.45f);
            playerMat.SetFloat("_Metallic", 0.15f);
            playerMat.enableInstancing = true;
            if (rockNormal != null)
            {
                playerMat.SetTexture("_BumpMap", rockNormal);
                playerMat.EnableKeyword("_NORMALMAP");
            }

            // 5. Lumen Crystal Glow Material
            string lumenPath = MaterialsDir + "/MAT_URP_LumenGlow.mat";
            lumenGlowMat = AssetDatabase.LoadAssetAtPath<Material>(lumenPath);
            if (lumenGlowMat == null)
            {
                lumenGlowMat = new Material(litShader);
                AssetDatabase.CreateAsset(lumenGlowMat, lumenPath);
            }
            lumenGlowMat.color = new Color(0.85f, 0.95f, 1f, 1f);
            lumenGlowMat.EnableKeyword("_EMISSION");
            lumenGlowMat.SetColor("_EmissionColor", new Color(0.35f, 0.80f, 1.2f, 1f) * 3.0f);
            lumenGlowMat.enableInstancing = true;

            // 6. Gothic Ruins Material (2K)
            string gothicPath = MaterialsDir + "/MAT_URP_GothicRuins.mat";
            gothicMat = AssetDatabase.LoadAssetAtPath<Material>(gothicPath);
            if (gothicMat == null)
            {
                gothicMat = new Material(litShader);
                AssetDatabase.CreateAsset(gothicMat, gothicPath);
            }
            gothicMat.color = Color.white;
            gothicMat.enableInstancing = true;
            var gothicBase = AssetDatabase.LoadAssetAtPath<Texture2D>(env2kDir + "/stone_brick_wall_001_diff_2k.jpg");
            var gothicNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(env2kDir + "/stone_brick_wall_001_nor_gl_2k.jpg");
            var gothicMask = AssetDatabase.LoadAssetAtPath<Texture2D>(env2kDir + "/stone_brick_wall_001_MaskMap_2k.png");
            if (gothicBase != null) gothicMat.SetTexture("_BaseMap", gothicBase);
            if (gothicNormal != null)
            {
                gothicMat.SetTexture("_BumpMap", gothicNormal);
                gothicMat.SetFloat("_BumpScale", 1.2f);
                gothicMat.EnableKeyword("_NORMALMAP");
            }
            if (gothicMask != null)
            {
                gothicMat.SetTexture("_MetallicGlossMap", gothicMask);
                gothicMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            gothicMat.SetFloat("_Smoothness", 0.35f);

            // 7. Wood Scaffolding Material (1K)
            string woodPath = MaterialsDir + "/MAT_URP_WoodScaffolding.mat";
            woodMat = AssetDatabase.LoadAssetAtPath<Material>(woodPath);
            if (woodMat == null)
            {
                woodMat = new Material(litShader);
                AssetDatabase.CreateAsset(woodMat, woodPath);
            }
            woodMat.color = Color.white;
            woodMat.enableInstancing = true;
            var woodBase = AssetDatabase.LoadAssetAtPath<Texture2D>(prop1kDir + "/wood_planks_diff_1k.jpg");
            var woodNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(prop1kDir + "/wood_planks_nor_gl_1k.jpg");
            var woodMask = AssetDatabase.LoadAssetAtPath<Texture2D>(prop1kDir + "/wood_planks_MaskMap_1k.png");
            if (woodBase != null) woodMat.SetTexture("_BaseMap", woodBase);
            if (woodNormal != null)
            {
                woodMat.SetTexture("_BumpMap", woodNormal);
                woodMat.SetFloat("_BumpScale", 1.1f);
                woodMat.EnableKeyword("_NORMALMAP");
            }
            if (woodMask != null)
            {
                woodMat.SetTexture("_MetallicGlossMap", woodMask);
                woodMat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            woodMat.SetFloat("_Smoothness", 0.25f);

            // 8. Bioluminescent Crystal Material
            string crystalPath = MaterialsDir + "/MAT_URP_BiolumCrystal.mat";
            crystalMat = AssetDatabase.LoadAssetAtPath<Material>(crystalPath);
            if (crystalMat == null)
            {
                crystalMat = new Material(litShader);
                AssetDatabase.CreateAsset(crystalMat, crystalPath);
            }
            crystalMat.color = new Color(0.2f, 0.85f, 0.95f, 1f);
            crystalMat.EnableKeyword("_EMISSION");
            crystalMat.SetColor("_EmissionColor", new Color(0.15f, 0.90f, 1.15f, 1f) * 4.5f);
            crystalMat.SetFloat("_Smoothness", 0.92f);
            crystalMat.enableInstancing = true;

            // Particle Dot Texture
            string dotTexPath = "Assets/_Project/Art/VFX/TEX_ParticleSoftDot.png";
            AssetDatabase.ImportAsset(dotTexPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var dotTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dotTexPath);

            // Mist & Ember Particle Materials
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader != null)
            {
                string mistPath = MaterialsDir + "/MAT_VFX_MistParticles.mat";
                Material mistMat = AssetDatabase.LoadAssetAtPath<Material>(mistPath);
                if (mistMat == null)
                {
                    mistMat = new Material(particleShader);
                    AssetDatabase.CreateAsset(mistMat, mistPath);
                }
                mistMat.SetFloat("_Surface", 1f);
                mistMat.SetFloat("_Blend", 0f);
                mistMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mistMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mistMat.SetInt("_ZWrite", 0);
                mistMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mistMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mistMat.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.30f, 0.20f));
                if (dotTex != null) mistMat.SetTexture("_BaseMap", dotTex);

                string emberPath = MaterialsDir + "/MAT_VFX_EmberParticles.mat";
                Material emberMat = AssetDatabase.LoadAssetAtPath<Material>(emberPath);
                if (emberMat == null)
                {
                    emberMat = new Material(particleShader);
                    AssetDatabase.CreateAsset(emberMat, emberPath);
                }
                emberMat.SetFloat("_Surface", 1f);
                emberMat.SetFloat("_Blend", 1f);
                emberMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                emberMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                emberMat.SetInt("_ZWrite", 0);
                emberMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                emberMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                emberMat.EnableKeyword("_BLENDMODE_ADD");
                emberMat.SetColor("_BaseColor", new Color(2.5f, 1.8f, 0.6f, 1.0f));
                if (dotTex != null) emberMat.SetTexture("_BaseMap", dotTex);
            }
        }

        private static VolumeProfile CreateOrUpdateCavernVolumeProfile()
        {
            string profilePath = SettingsDir + "/Profile_PrimordialMudBed_PostProcess.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            if (!profile.TryGet(out Tonemapping tonemapping)) tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet(out ColorAdjustments colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.active = true;
            colorAdj.postExposure.Override(0.35f);
            colorAdj.contrast.Override(22f);
            colorAdj.saturation.Override(-8f);
            colorAdj.colorFilter.Override(new Color(0.96f, 0.98f, 1.0f, 1f));

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.80f);
            bloom.tint.Override(new Color(1.0f, 0.92f, 0.82f, 1.0f));

            if (!profile.TryGet(out ShadowsMidtonesHighlights smh)) smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.active = true;
            smh.shadows.Override(new Vector4(0.88f, 0.94f, 1.05f, 0f));
            smh.midtones.Override(new Vector4(0.98f, 0.97f, 0.95f, 0f));
            smh.highlights.Override(new Vector4(1.08f, 1.00f, 0.90f, 0f));

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.Override(0.30f);
            vignette.smoothness.Override(0.42f);

            return profile;
        }

        private static void AssembleSceneHierarchy(
            Material mudMat,
            Material rockMat,
            Material petrifiedMat,
            Material lumenGlowMat,
            Material playerMat,
            Material gothicMat,
            Material woodMat,
            Material crystalMat,
            VolumeProfile volumeProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Atmosphere & Fog Settings
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.32f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 95f;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.12f, 1f);

            // 2. Global Directional Light & Volume
            GameObject dirLightObj = new GameObject("LIGHT_CavernCelestialShaft");
            Light dirLight = dirLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.88f, 0.94f, 1.0f, 1f);
            dirLight.intensity = 1.45f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.80f;
            dirLightObj.transform.position = new Vector3(8f, 95f, -4f);
            dirLightObj.transform.rotation = Quaternion.Euler(68f, 32f, 0f);

            GameObject volumeObj = new GameObject("VOLUME_GlobalPostProcessing");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.profile = volumeProfile;

            // Load modular FBX assets
            GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/MOD_Muro_Caverna_A.fbx");
            GameObject pilarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/MOD_Pilar_Ruina_B.fbx");
            GameObject sueloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/MOD_Suelo_Lodo_C.fbx");
            GameObject bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/MOD_Puente_Arco_D.fbx");
            GameObject scaffoldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/MOD_Andamio_Elevador_E.fbx");
            GameObject bonfirePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/PROP_CoiledSwordBonfire.fbx");
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/DOOR_BossMonumentalGate.fbx");
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/CHR_PlayerBaseMannequin.fbx");

            GameObject worldRoot = new GameObject("ENVIRONMENT_MACRO_WORLD");

            // ==========================================
            // LAYER 0: EL LECHO DE LODO PRIMORDIAL (Tutorial)
            // ==========================================
            GameObject layer0Root = new GameObject("GROUP_Layer0_PrimordialMudBed");
            layer0Root.transform.SetParent(worldRoot.transform);

            // Spawn Shaft Golden Spot Light
            GameObject spawnSpotObj = new GameObject("LIGHT_SpawnShaft_GoldFocus");
            spawnSpotObj.transform.SetParent(layer0Root.transform);
            Light spawnSpot = spawnSpotObj.AddComponent<Light>();
            spawnSpot.type = LightType.Spot;
            spawnSpot.color = new Color(1.0f, 0.82f, 0.45f, 1f);
            spawnSpot.intensity = 8.5f;
            spawnSpot.range = 30f;
            spawnSpot.spotAngle = 55f;
            spawnSpot.innerSpotAngle = 25f;
            spawnSpot.shadows = LightShadows.Soft;
            spawnSpotObj.transform.position = new Vector3(0f, 15f, -4.8f);
            spawnSpotObj.transform.rotation = Quaternion.Euler(88f, 0f, 0f);

            CreateFillLight("LIGHT_SpawnBasin_WarmFill", new Vector3(0f, -0.2f, -4.8f), new Color(1.0f, 0.75f, 0.35f), 4.2f, 9f, layer0Root.transform);
            CreateFillLight("LIGHT_CaveFill_MidSection", new Vector3(0f, 5f, 18f), new Color(0.38f, 0.48f, 0.65f), 3.0f, 22f, layer0Root.transform);
            CreateFillLight("LIGHT_CaveFill_Arena", new Vector3(0f, 6f, 30f), new Color(0.35f, 0.45f, 0.62f), 3.2f, 25f, layer0Root.transform);
            CreateFillLight("LIGHT_CaveFill_UpperRamp", new Vector3(0f, 10f, 48f), new Color(0.55f, 0.45f, 0.32f), 3.5f, 24f, layer0Root.transform);

            // Basin Terrain
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_PrimordialMudBed_Terrain.fbx");
            GameObject terrainObj = terrainPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(terrainPrefab) : GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrainObj.name = "ENV_PrimordialMudBed_Terrain";
            terrainObj.transform.SetParent(layer0Root.transform);
            terrainObj.transform.position = Vector3.zero;
            terrainObj.transform.rotation = Quaternion.identity;
            ApplyMaterialRecursively(terrainObj, mudMat);
            EnsureMeshCollider(terrainObj);
            Physics.SyncTransforms();
            MarkStaticRecursive(terrainObj);
            var terrainSurface = terrainObj.AddComponent<SurfaceMaterialIdentifier>();
            SetSurfaceType(terrainSurface, SurfaceMaterialType.Dirt);

            // Flanking cavern pillars
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_CavernWall_Pillar_A.fbx");
            GameObject pillarsRoot = new GameObject("GROUP_CavernPillarsAndOverhangs");
            pillarsRoot.transform.SetParent(layer0Root.transform);

            Vector3[] pillarPositions =
            {
                new Vector3(-8.5f, -1.8f, -7.0f), new Vector3(8.5f, -1.8f, -7.0f),
                new Vector3(-8.2f, -1.6f, -1.0f), new Vector3(8.2f, -1.6f, -1.0f),
                new Vector3(-7.5f, -0.6f, 5.0f), new Vector3(7.8f, -0.6f, 5.0f),
                new Vector3(-8.0f, 0.4f, 11.5f), new Vector3(7.5f, 0.4f, 11.5f),
                new Vector3(-6.2f, 1.2f, 17.5f), new Vector3(6.5f, 1.2f, 18.0f),
                new Vector3(-15.0f, 0.0f, -6.0f), new Vector3(15.0f, 0.0f, -6.0f),
                new Vector3(-15.5f, 1.0f, 8.0f), new Vector3(15.5f, 1.0f, 8.0f),
                new Vector3(-12.0f, 2.2f, 24.0f), new Vector3(12.5f, 2.2f, 24.0f),
                new Vector3(-13.0f, 2.8f, 32.0f), new Vector3(13.0f, 2.8f, 32.0f),
                new Vector3(-10.5f, 3.5f, 39.0f), new Vector3(10.8f, 3.5f, 39.0f),
                new Vector3(-18.0f, 3.5f, 28.0f), new Vector3(18.0f, 3.5f, 28.0f),
                new Vector3(-6.5f, 5.0f, 46.0f), new Vector3(6.5f, 5.0f, 46.0f),
                new Vector3(-7.5f, 7.5f, 54.0f), new Vector3(7.5f, 7.5f, 54.0f)
            };

            for (int i = 0; i < pillarPositions.Length; i++)
            {
                GameObject pillar = pillarPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Pillar_{i + 1:D2}";
                pillar.transform.SetParent(pillarsRoot.transform);
                pillar.transform.position = pillarPositions[i];
                pillar.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
                float randomScale = 0.82f + (i % 3) * 0.10f;
                pillar.transform.localScale = new Vector3(randomScale, randomScale * 1.15f, randomScale);
                ApplyMaterialRecursively(pillar, rockMat);
                EnsureMeshCollider(pillar);
                MarkStaticRecursive(pillar);
                var surf = pillar.AddComponent<SurfaceMaterialIdentifier>();
                SetSurfaceType(surf, SurfaceMaterialType.Stone);
            }

            // Overhang Ledges
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_Cavern_Rock_Ledge_B.fbx");
            Vector3[] ledgePositions =
            {
                new Vector3(-8.5f, 13.5f, 0.0f), new Vector3(8.5f, 14.0f, 3.0f),
                new Vector3(-9.0f, 15.0f, 14.0f), new Vector3(9.0f, 15.5f, 20.0f),
                new Vector3(-11.0f, 17.0f, 30.0f), new Vector3(11.0f, 17.5f, 36.0f),
                new Vector3(0.0f, 18.5f, 48.0f)
            };
            for (int i = 0; i < ledgePositions.Length; i++)
            {
                if (ledgePrefab != null)
                {
                    GameObject ledge = (GameObject)PrefabUtility.InstantiatePrefab(ledgePrefab);
                    ledge.name = $"OverhangLedge_{i + 1:D2}";
                    ledge.transform.SetParent(pillarsRoot.transform);
                    ledge.transform.position = ledgePositions[i];
                    ledge.transform.rotation = Quaternion.Euler(15f, (i * 73f) % 360f, 0f);
                    ApplyMaterialRecursively(ledge, rockMat);
                    EnsureMeshCollider(ledge);
                    MarkStaticRecursive(ledge);
                }
            }

            // Deep Mud Debuff Zones
            GameObject mudZonesRoot = new GameObject("GROUP_DeepMudDebuffZones");
            mudZonesRoot.transform.SetParent(layer0Root.transform);

            GameObject spawnMudZone = new GameObject("ZONE_SpawnMudBasin");
            spawnMudZone.transform.SetParent(mudZonesRoot.transform);
            spawnMudZone.transform.position = new Vector3(0f, -1.2f, -4.5f);
            var box1 = spawnMudZone.AddComponent<BoxCollider>();
            box1.isTrigger = true;
            box1.size = new Vector3(14f, 2.5f, 13f);
            spawnMudZone.AddComponent<MudDebuffZone>();

            GameObject arenaMudZone = new GameObject("ZONE_ConflictArenaMud");
            arenaMudZone.transform.SetParent(mudZonesRoot.transform);
            arenaMudZone.transform.position = new Vector3(0f, 2.6f, 28f);
            var box2 = arenaMudZone.AddComponent<BoxCollider>();
            box2.isTrigger = true;
            box2.size = new Vector3(18f, 2.8f, 16f);
            arenaMudZone.AddComponent<MudDebuffZone>();

            // Lore Object: Surface Garments Remains
            GameObject lorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/PROP_SurfaceGarments_Remains.fbx");
            GameObject loreObj = lorePrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(lorePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            loreObj.name = "PROP_SurfaceGarments_Remains";
            loreObj.transform.SetParent(layer0Root.transform);
            loreObj.transform.position = new Vector3(1.6f, -1.30f, -3.2f);
            loreObj.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
            ApplyMaterialRecursively(loreObj, rockMat);
            EnsureMeshCollider(loreObj);
            EnsureTriggerCollider(loreObj, 2.5f);

            GameObject lumenLightObj = new GameObject("LIGHT_LumenShardGlow");
            lumenLightObj.transform.SetParent(loreObj.transform);
            lumenLightObj.transform.localPosition = new Vector3(0.08f, 0.7f, 0.05f);
            Light lumenLight = lumenLightObj.AddComponent<Light>();
            lumenLight.type = LightType.Point;
            lumenLight.color = new Color(0.40f, 0.85f, 1.0f, 1f);
            lumenLight.intensity = 1.6f;
            lumenLight.range = 3.5f;

            var loreInteractable = loreObj.AddComponent<LoreInteractable>();
            var loreAudio = loreObj.AddComponent<AudioSource>();
            loreAudio.playOnAwake = false;
            loreAudio.spatialBlend = 1.0f;
            var chimeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Lore_Lumen_Chime.wav");
            SerializedObject loreSo = new SerializedObject(loreInteractable);
            loreSo.FindProperty("_interactChimeClip").objectReferenceValue = chimeClip;
            loreSo.FindProperty("_audioSource").objectReferenceValue = loreAudio;
            loreSo.FindProperty("_crystalGlowLight").objectReferenceValue = lumenLight;
            loreSo.ApplyModifiedPropertiesWithoutUndo();

            // Petrified corpses
            if (characterPrefab != null)
            {
                GameObject bodiesRoot = new GameObject("GROUP_PetrifiedHumanoidCorpses");
                bodiesRoot.transform.SetParent(layer0Root.transform);
                (Vector3 pos, Vector3 rot)[] bodyTransforms =
                {
                    (new Vector3(-1.6f, -1.25f, -3.2f), new Vector3(82f, 40f, -15f)),
                    (new Vector3(3.8f, 1.95f, 14f), new Vector3(15f, -70f, 25f)),
                    (new Vector3(-4.5f, 2.80f, 26f), new Vector3(65f, 120f, 0f)),
                    (new Vector3(2.5f, 5.95f, 44f), new Vector3(80f, -20f, 10f))
                };
                for (int i = 0; i < bodyTransforms.Length; i++)
                {
                    GameObject corpse = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                    corpse.name = $"PetrifiedBody_{i + 1:D2}";
                    corpse.transform.SetParent(bodiesRoot.transform);
                    corpse.transform.position = bodyTransforms[i].pos;
                    corpse.transform.rotation = Quaternion.Euler(bodyTransforms[i].rot);
                    ApplyMaterialRecursively(corpse, petrifiedMat);
                }
            }

            // Tutorial Ramp & Transition Elevator connecting to Layer 1 (The Hub)
            GameObject tutorialAscentRamp = InstantiateModularPiece(bridgePrefab, new Vector3(0f, 7.5f, 52f), Quaternion.Euler(-14f, 0f, 0f), new Vector3(1.2f, 1f, 1.4f), gothicMat, layer0Root.transform, "RAMP_TutorialToHubShaft");

            // Tutorial To Hub Scaffolding Elevator Tower
            GameObject elevatorTutorialObj = InstantiateModularPiece(scaffoldPrefab, new Vector3(0f, 7.8f, 66f), Quaternion.identity, new Vector3(1.4f, 1.35f, 1.4f), woodMat, layer0Root.transform, "ELEVATOR_TutorialToHub");
            var tutElevator = elevatorTutorialObj.AddComponent<PersistentElevator>();
            EnsureTriggerCollider(elevatorTutorialObj, 4.0f);

            // ==========================================
            // LAYER 1: EL SANTUARIO NEUTRAL (Central Hub)
            // ==========================================
            GameObject hubRoot = new GameObject("GROUP_Layer1_CentralHub_Sanctuary");
            hubRoot.transform.SetParent(worldRoot.transform);

            // Hub Courtyard Slabs & Colonnade
            Vector3 hubCenter = new Vector3(0f, 24.0f, 85f);
            CreateFillLight("LIGHT_Hub_WarmSanctuary", hubCenter + new Vector3(0f, 6f, 0f), new Color(1.0f, 0.88f, 0.70f), 4.5f, 30f, hubRoot.transform);

            // Floor Slabs
            InstantiateModularPiece(sueloPrefab, hubCenter, Quaternion.identity, new Vector3(2.2f, 1f, 2.2f), gothicMat, hubRoot.transform, "SLAB_Hub_Plaza_Central");
            InstantiateModularPiece(sueloPrefab, hubCenter + new Vector3(-18f, 0f, 0f), Quaternion.identity, new Vector3(1.2f, 1f, 1.4f), gothicMat, hubRoot.transform, "SLAB_Hub_WestTerrace");
            InstantiateModularPiece(sueloPrefab, hubCenter + new Vector3(18f, 0f, 0f), Quaternion.identity, new Vector3(1.2f, 1f, 1.4f), gothicMat, hubRoot.transform, "SLAB_Hub_EastTerrace");
            InstantiateModularPiece(sueloPrefab, hubCenter + new Vector3(0f, 0.5f, 18f), Quaternion.identity, new Vector3(1.6f, 1f, 1.4f), gothicMat, hubRoot.transform, "SLAB_Hub_NorthSanctum");

            // Flanking Gothic Pillars around Hub
            Vector3[] hubPillarOffsets =
            {
                new Vector3(-12f, 0f, -10f), new Vector3(12f, 0f, -10f),
                new Vector3(-14f, 0f, 0f), new Vector3(14f, 0f, 0f),
                new Vector3(-12f, 0f, 12f), new Vector3(12f, 0f, 12f),
                new Vector3(-6f, 0f, 20f), new Vector3(6f, 0f, 20f)
            };
            for (int i = 0; i < hubPillarOffsets.Length; i++)
            {
                InstantiateModularPiece(pilarPrefab, hubCenter + hubPillarOffsets[i], Quaternion.identity, new Vector3(1.2f, 1.2f, 1.2f), gothicMat, hubRoot.transform, $"Pilar_Hub_{i + 1:D2}");
            }

            // Hub Bonfire Nexus: "Bonfire_Hub_Sanctuary"
            GameObject bonfireHubObj = CreateBonfire("Bonfire_Hub_Sanctuary", hubCenter + new Vector3(0f, 0.2f, -2.0f), bonfirePrefab, gothicMat, hubRoot.transform, true);

            // Shortcut Door: Hub Balcony back down to Layer 0
            GameObject shortcutHubToL0Obj = new GameObject("SHORTCUT_HubToLayer0_Gate");
            shortcutHubToL0Obj.transform.SetParent(hubRoot.transform);
            shortcutHubToL0Obj.transform.position = hubCenter + new Vector3(0f, 0f, -16f);
            var shortcutL0 = shortcutHubToL0Obj.AddComponent<OneWayShortcutDoor>();
            EnsureTriggerCollider(shortcutHubToL0Obj, 3.5f);
            GameObject gateVisualL0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateVisualL0.name = "Hinge_GateVisual";
            gateVisualL0.transform.SetParent(shortcutHubToL0Obj.transform);
            gateVisualL0.transform.localPosition = new Vector3(1.2f, 1.8f, 0f);
            gateVisualL0.transform.localScale = new Vector3(2.4f, 3.5f, 0.3f);
            ApplyMaterialRecursively(gateVisualL0, gothicMat);
            SerializedObject scSoL0 = new SerializedObject(shortcutL0);
            scSoL0.FindProperty("_doorSaveId").stringValue = "Shortcut_Hub_To_Layer0";
            scSoL0.FindProperty("_doorHingeTransform").objectReferenceValue = gateVisualL0.transform;
            scSoL0.ApplyModifiedPropertiesWithoutUndo();

            // Diverging Path 1 (West -> Zone A: Las Agujas de Piedra)
            InstantiateModularPiece(bridgePrefab, hubCenter + new Vector3(-25f, 1.5f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector3(1.1f, 1f, 1.6f), gothicMat, hubRoot.transform, "BRIDGE_HubToZoneA");

            // Diverging Path 2 (East/Down -> Zone B: El Abismo y las Cavernas)
            InstantiateModularPiece(bridgePrefab, hubCenter + new Vector3(24f, -4f, -14f), Quaternion.Euler(16f, 45f, 0f), new Vector3(1.0f, 1f, 1.5f), gothicMat, hubRoot.transform, "RAMP_HubToZoneB");

            // Diverging Path 3 (East/Up -> Zone C: La Ciudad de los Petrificados)
            InstantiateModularPiece(bridgePrefab, hubCenter + new Vector3(24f, 4f, 14f), Quaternion.Euler(-14f, 135f, 0f), new Vector3(1.1f, 1f, 1.5f), gothicMat, hubRoot.transform, "RAMP_HubToZoneC");

            // Apex Seal Mechanism at North Sanctorum of Hub
            GameObject sealObj = new GameObject("MECHANISM_ApexSeal_Hub");
            sealObj.transform.SetParent(hubRoot.transform);
            sealObj.transform.position = hubCenter + new Vector3(0f, 0.5f, 19f);
            EnsureTriggerCollider(sealObj, 4.5f);
            var apexMechanism = sealObj.AddComponent<ApexSealMechanism>();

            // 3 Lord Soul Runic Pedestals
            GameObject runeA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            runeA.name = "RunePedestal_ZoneA_Spire";
            runeA.transform.SetParent(sealObj.transform);
            runeA.transform.localPosition = new Vector3(-3.5f, 0.6f, 0f);
            runeA.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
            ApplyMaterialRecursively(runeA, gothicMat);

            GameObject runeB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            runeB.name = "RunePedestal_ZoneB_Abyss";
            runeB.transform.SetParent(sealObj.transform);
            runeB.transform.localPosition = new Vector3(0f, 0.6f, 2.5f);
            runeB.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
            ApplyMaterialRecursively(runeB, gothicMat);

            GameObject runeC = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            runeC.name = "RunePedestal_ZoneC_City";
            runeC.transform.SetParent(sealObj.transform);
            runeC.transform.localPosition = new Vector3(3.5f, 0.6f, 0f);
            runeC.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
            ApplyMaterialRecursively(runeC, gothicMat);

            // Blocking Seal Barrier & Great Apex Elevator
            GameObject sealBarrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sealBarrier.name = "Barrier_ApexSealGate";
            sealBarrier.transform.SetParent(sealObj.transform);
            sealBarrier.transform.localPosition = new Vector3(0f, 3.5f, 6.0f);
            sealBarrier.transform.localScale = new Vector3(6.5f, 7.0f, 0.8f);
            ApplyMaterialRecursively(sealBarrier, crystalMat);

            GameObject apexElevatorTower = InstantiateModularPiece(scaffoldPrefab, hubCenter + new Vector3(0f, 0f, 28f), Quaternion.identity, new Vector3(1.8f, 4.8f, 1.8f), gothicMat, hubRoot.transform, "ELEVATOR_ApexCathedralShaft");
            var apexElevator = apexElevatorTower.AddComponent<PersistentElevator>();
            EnsureTriggerCollider(apexElevatorTower, 4.5f);

            SerializedObject apexSo = new SerializedObject(apexMechanism);
            apexSo.FindProperty("_runeSpireZoneA").objectReferenceValue = runeA;
            apexSo.FindProperty("_runeAbyssZoneB").objectReferenceValue = runeB;
            apexSo.FindProperty("_runeCityZoneC").objectReferenceValue = runeC;
            apexSo.FindProperty("_blockingSealBarrier").objectReferenceValue = sealBarrier;
            apexSo.FindProperty("_apexElevator").objectReferenceValue = apexElevator;
            apexSo.ApplyModifiedPropertiesWithoutUndo();

            // Shortcut Door: Hub West Wall from Zone A
            CreateShortcutDoor("SHORTCUT_HubFromZoneA_Gate", hubCenter + new Vector3(-20f, 0f, 8f), Quaternion.Euler(0f, 90f, 0f), "Shortcut_Hub_From_ZoneA", gothicMat, hubRoot.transform);

            // Shortcut Door: Hub East Crypt from Zone B
            CreateShortcutDoor("SHORTCUT_HubFromZoneB_Gate", hubCenter + new Vector3(20f, -2f, -6f), Quaternion.Euler(0f, -90f, 0f), "Shortcut_Hub_From_ZoneB", gothicMat, hubRoot.transform);

            // Shortcut Door: Hub North-East Gate from Zone C
            CreateShortcutDoor("SHORTCUT_HubFromZoneC_Gate", hubCenter + new Vector3(20f, 1f, 12f), Quaternion.Euler(0f, -90f, 0f), "Shortcut_Hub_From_ZoneC", gothicMat, hubRoot.transform);

            // ==========================================
            // ZONA A: LAS AGUJAS DE PIEDRA (Ascendente)
            // ==========================================
            GameObject zoneARoot = new GameObject("GROUP_ZoneA_StoneSpires");
            zoneARoot.transform.SetParent(worldRoot.transform);

            CreateFillLight("LIGHT_ZoneA_SummitSunbeam", new Vector3(-58f, 65f, 115f), new Color(1.0f, 0.95f, 0.85f), 5.0f, 45f, zoneARoot.transform);
            CreateFillLight("LIGHT_ZoneA_MidSpire", new Vector3(-45f, 44f, 90f), new Color(0.70f, 0.80f, 0.95f), 3.5f, 35f, zoneARoot.transform);

            // Monolith Spires (Stacked modular cavern walls reaching Y=65)
            Vector3[] spireBasePositions =
            {
                new Vector3(-40f, 20f, 75f),
                new Vector3(-65f, 22f, 85f),
                new Vector3(-45f, 32f, 110f),
                new Vector3(-70f, 38f, 115f),
                new Vector3(-58f, 48f, 115f)
            };
            for (int i = 0; i < spireBasePositions.Length; i++)
            {
                for (int h = 0; h < 3; h++)
                {
                    InstantiateModularPiece(wallPrefab, spireBasePositions[i] + new Vector3(0f, h * 14f, 0f), Quaternion.Euler(0f, i * 65f + h * 25f, 0f), new Vector3(1.3f, 1.2f, 1.3f), rockMat, zoneARoot.transform, $"SpireWall_{i + 1}_{h + 1}");
                }
            }

            // Walkways & Arched Bridges connecting Spires
            InstantiateModularPiece(bridgePrefab, new Vector3(-38f, 32f, 82f), Quaternion.Euler(10f, 35f, 0f), new Vector3(1.1f, 1f, 1.5f), gothicMat, zoneARoot.transform, "Walkway_Spire_Lower");
            
            // "Puente de la Aguja" at Y = +44m (Key panoramic vista overlooking Zone B chasm & Zone C)
            GameObject needleBridge = InstantiateModularPiece(bridgePrefab, new Vector3(-45f, 44f, 95f), Quaternion.Euler(0f, 15f, 0f), new Vector3(1.3f, 1f, 2.0f), gothicMat, zoneARoot.transform, "PUENTE_DE_LA_AGUJA_Y44");

            // Summit Platform at Y = +60m
            InstantiateModularPiece(sueloPrefab, new Vector3(-58f, 60f, 115f), Quaternion.identity, new Vector3(1.8f, 1.2f, 1.8f), gothicMat, zoneARoot.transform, "SLAB_ZoneA_SummitArena");

            // Bonfire: "Bonfire_ZoneA_SpireSummit"
            CreateBonfire("Bonfire_ZoneA_SpireSummit", new Vector3(-58f, 60.5f, 110f), bonfirePrefab, gothicMat, zoneARoot.transform, false);

            // Lord Soul Altar: Zone A
            CreateLordSoulAltar("ALTAR_LordSoul_ZoneA_Spire", new Vector3(-58f, 60.8f, 118f), 0, "Lord of the High Spires", apexMechanism, crystalMat, zoneARoot.transform);

            // Zone A Elevator Shortcut back down to Hub Terrace
            GameObject zoneAElevator = InstantiateModularPiece(scaffoldPrefab, new Vector3(-25f, 24f, 90f), Quaternion.identity, new Vector3(1.4f, 2.8f, 1.4f), woodMat, zoneARoot.transform, "ELEVATOR_ZoneAToHubShortcut");
            zoneAElevator.AddComponent<PersistentElevator>();
            EnsureTriggerCollider(zoneAElevator, 4.0f);

            // ==========================================
            // ZONA B: EL ABISMO Y LAS CAVERNAS (Descendente)
            // ==========================================
            GameObject zoneBRoot = new GameObject("GROUP_ZoneB_AbyssAndCaverns");
            zoneBRoot.transform.SetParent(worldRoot.transform);

            CreateFillLight("LIGHT_ZoneB_AbyssCoreGlow", new Vector3(70f, 10f, 40f), new Color(0.12f, 0.70f, 1.0f), 5.5f, 40f, zoneBRoot.transform);
            CreateFillLight("LIGHT_ZoneB_MidGrotto", new Vector3(50f, 14f, 55f), new Color(0.18f, 0.55f, 0.92f), 4.0f, 32f, zoneBRoot.transform);

            // Cavern Walls enclosing the Abyss
            Vector3[] abyssWallPositions =
            {
                new Vector3(35f, 6f, 30f), new Vector3(60f, 6f, 20f),
                new Vector3(85f, 6f, 35f), new Vector3(85f, 8f, 60f),
                new Vector3(70f, 8f, 75f), new Vector3(45f, 8f, 70f)
            };
            for (int i = 0; i < abyssWallPositions.Length; i++)
            {
                InstantiateModularPiece(wallPrefab, abyssWallPositions[i], Quaternion.Euler(0f, i * 55f, 0f), new Vector3(1.4f, 1.2f, 1.4f), rockMat, zoneBRoot.transform, $"AbyssWall_{i + 1:D2}");
            }

            // Wooden Scaffolding Catwalks across the Chasm
            InstantiateModularPiece(scaffoldPrefab, new Vector3(38f, 12f, 48f), Quaternion.identity, new Vector3(1.5f, 1f, 1.5f), woodMat, zoneBRoot.transform, "Scaffold_Abyss_01");
            InstantiateModularPiece(scaffoldPrefab, new Vector3(52f, 8f, 42f), Quaternion.identity, new Vector3(1.5f, 1f, 1.5f), woodMat, zoneBRoot.transform, "Scaffold_Abyss_02");

            // Bioluminescent Crystal Formations
            Vector3[] crystalPositions =
            {
                new Vector3(48f, 6.5f, 32f), new Vector3(65f, 7.0f, 25f),
                new Vector3(78f, 7.5f, 45f), new Vector3(62f, 9.0f, 60f),
                new Vector3(55f, 15.0f, 65f)
            };
            for (int i = 0; i < crystalPositions.Length; i++)
            {
                GameObject crystalObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crystalObj.name = $"BiolumCrystal_{i + 1:D2}";
                crystalObj.transform.SetParent(zoneBRoot.transform);
                crystalObj.transform.position = crystalPositions[i];
                crystalObj.transform.rotation = Quaternion.Euler(25f, i * 45f, 15f);
                crystalObj.transform.localScale = new Vector3(0.6f, 2.2f, 0.6f);
                ApplyMaterialRecursively(crystalObj, crystalMat);
                MarkStaticRecursive(crystalObj);
            }

            // Abyss Grotto Floor Slabs & Bonfire
            InstantiateModularPiece(sueloPrefab, new Vector3(70f, 6.0f, 40f), Quaternion.identity, new Vector3(1.8f, 1f, 1.8f), mudMat, zoneBRoot.transform, "SLAB_ZoneB_AbyssFloor");
            CreateBonfire("Bonfire_ZoneB_AbyssDeep", new Vector3(68f, 6.3f, 36f), bonfirePrefab, gothicMat, zoneBRoot.transform, false);

            // Lord Soul Altar: Zone B
            CreateLordSoulAltar("ALTAR_LordSoul_ZoneB_Abyss", new Vector3(72f, 6.6f, 44f), 1, "Lord of the Sunken Chasm", apexMechanism, crystalMat, zoneBRoot.transform);

            // Secret Subterranean Bridge connecting Zone B under the cliff into Zone C
            InstantiateModularPiece(bridgePrefab, new Vector3(65f, 16f, 80f), Quaternion.Euler(8f, 20f, 0f), new Vector3(1.2f, 1f, 1.6f), gothicMat, zoneBRoot.transform, "SECRET_BRIDGE_AbyssToCity");

            // ==========================================
            // ZONA C: LA CIUDAD DE LOS PETRIFICADOS (Laberíntica)
            // ==========================================
            GameObject zoneCRoot = new GameObject("GROUP_ZoneC_PetrifiedCity");
            zoneCRoot.transform.SetParent(worldRoot.transform);

            CreateFillLight("LIGHT_ZoneC_KeepGothicGlow", new Vector3(75f, 52f, 140f), new Color(0.92f, 0.85f, 0.72f), 4.8f, 38f, zoneCRoot.transform);
            CreateFillLight("LIGHT_ZoneC_LowerCourtyard", new Vector3(50f, 32f, 105f), new Color(0.65f, 0.62f, 0.70f), 3.8f, 32f, zoneCRoot.transform);

            // Multi-Tier Ruined Gothic Slabs
            InstantiateModularPiece(sueloPrefab, new Vector3(45f, 30f, 100f), Quaternion.identity, new Vector3(1.6f, 1f, 1.6f), gothicMat, zoneCRoot.transform, "SLAB_City_Courtyard_Lower");
            InstantiateModularPiece(sueloPrefab, new Vector3(62f, 38f, 120f), Quaternion.identity, new Vector3(1.8f, 1f, 1.8f), gothicMat, zoneCRoot.transform, "SLAB_City_Terrace_Mid");
            InstantiateModularPiece(sueloPrefab, new Vector3(75f, 50f, 140f), Quaternion.identity, new Vector3(1.8f, 1.2f, 1.8f), gothicMat, zoneCRoot.transform, "SLAB_City_Keep_Upper");

            // Connecting Gothic Bridges & Stairs
            InstantiateModularPiece(bridgePrefab, new Vector3(54f, 34f, 110f), Quaternion.Euler(-12f, 40f, 0f), new Vector3(1.2f, 1f, 1.5f), gothicMat, zoneCRoot.transform, "STAIRS_City_LowerToMid");
            InstantiateModularPiece(bridgePrefab, new Vector3(69f, 44f, 130f), Quaternion.Euler(-14f, 42f, 0f), new Vector3(1.2f, 1f, 1.5f), gothicMat, zoneCRoot.transform, "STAIRS_City_MidToKeep");

            // Gothic Ruined Pillars flanking the City courtyards
            Vector3[] cityPillarPositions =
            {
                new Vector3(40f, 30f, 94f), new Vector3(50f, 30f, 94f),
                new Vector3(40f, 30f, 106f), new Vector3(50f, 30f, 106f),
                new Vector3(56f, 38f, 114f), new Vector3(68f, 38f, 114f),
                new Vector3(56f, 38f, 126f), new Vector3(68f, 38f, 126f),
                new Vector3(70f, 50f, 134f), new Vector3(80f, 50f, 134f),
                new Vector3(70f, 50f, 146f), new Vector3(80f, 50f, 146f)
            };
            for (int i = 0; i < cityPillarPositions.Length; i++)
            {
                InstantiateModularPiece(pilarPrefab, cityPillarPositions[i], Quaternion.identity, new Vector3(1.15f, 1.15f, 1.15f), gothicMat, zoneCRoot.transform, $"Pilar_City_{i + 1:D2}");
            }

            // Petrified Humanoids scattered in the City
            if (characterPrefab != null)
            {
                (Vector3 pos, Vector3 rot)[] cityBodies =
                {
                    (new Vector3(43f, 30.5f, 98f), new Vector3(10f, 30f, 75f)),
                    (new Vector3(48f, 30.5f, 102f), new Vector3(85f, -20f, 0f)),
                    (new Vector3(60f, 38.5f, 118f), new Vector3(70f, 140f, 0f)),
                    (new Vector3(66f, 38.5f, 122f), new Vector3(20f, 90f, 80f)),
                    (new Vector3(73f, 50.5f, 138f), new Vector3(80f, 0f, 0f))
                };
                for (int i = 0; i < cityBodies.Length; i++)
                {
                    GameObject corpse = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                    corpse.name = $"PetrifiedCitizen_{i + 1:D2}";
                    corpse.transform.SetParent(zoneCRoot.transform);
                    corpse.transform.position = cityBodies[i].pos;
                    corpse.transform.rotation = Quaternion.Euler(cityBodies[i].rot);
                    ApplyMaterialRecursively(corpse, petrifiedMat);
                }
            }

            // Bonfire: "Bonfire_ZoneC_CityKeep"
            CreateBonfire("Bonfire_ZoneC_CityKeep", new Vector3(72f, 50.4f, 136f), bonfirePrefab, gothicMat, zoneCRoot.transform, false);

            // Lord Soul Altar: Zone C
            CreateLordSoulAltar("ALTAR_LordSoul_ZoneC_PetrifiedCity", new Vector3(76f, 50.6f, 144f), 2, "Lord of the Petrified Citadel", apexMechanism, crystalMat, zoneCRoot.transform);

            // ==========================================
            // LAYER 3: EL ÁPICE (The Final Summit & Cathedral)
            // ==========================================
            GameObject apexRoot = new GameObject("GROUP_Layer3_ApexCathedral");
            apexRoot.transform.SetParent(worldRoot.transform);

            Vector3 apexCenter = new Vector3(0f, 80.0f, 145f);
            CreateFillLight("LIGHT_Apex_CelestialAvenue", apexCenter + new Vector3(0f, 14f, 0f), new Color(1.0f, 0.96f, 0.90f), 6.5f, 50f, apexRoot.transform);

            // Great Cathedral Floor Slabs
            InstantiateModularPiece(sueloPrefab, apexCenter + new Vector3(0f, 0f, -25f), Quaternion.identity, new Vector3(1.8f, 1f, 1.8f), gothicMat, apexRoot.transform, "SLAB_Apex_ArrivalElevatorHall");
            InstantiateModularPiece(sueloPrefab, apexCenter, Quaternion.identity, new Vector3(2.4f, 1.2f, 2.4f), gothicMat, apexRoot.transform, "SLAB_Apex_CathedralNave");
            InstantiateModularPiece(sueloPrefab, apexCenter + new Vector3(0f, 0f, 28f), Quaternion.identity, new Vector3(2.6f, 1.2f, 2.6f), gothicMat, apexRoot.transform, "SLAB_Apex_FinalBossArena");

            // Colonnade of Colossal Gothic Pillars along the Avenue
            for (int z = -20; z <= 25; z += 10)
            {
                InstantiateModularPiece(pilarPrefab, apexCenter + new Vector3(-8.5f, 0f, z), Quaternion.identity, new Vector3(1.6f, 2.0f, 1.6f), gothicMat, apexRoot.transform, $"Pilar_ApexAvenue_L_{z}");
                InstantiateModularPiece(pilarPrefab, apexCenter + new Vector3(8.5f, 0f, z), Quaternion.identity, new Vector3(1.6f, 2.0f, 1.6f), gothicMat, apexRoot.transform, $"Pilar_ApexAvenue_R_{z}");
            }

            // Bonfire: "Bonfire_Layer3_ApexSanctuary"
            CreateBonfire("Bonfire_Layer3_ApexSanctuary", apexCenter + new Vector3(0f, 0.3f, -10f), bonfirePrefab, gothicMat, apexRoot.transform, false);

            // Monumental Boss Gate & Fog Wall
            GameObject bossGateObj = gatePrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            bossGateObj.name = "GATE_Monumental_ApexCathedral";
            bossGateObj.transform.SetParent(apexRoot.transform);
            bossGateObj.transform.position = apexCenter + new Vector3(0f, 0f, 14f);
            bossGateObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            ApplyMaterialRecursively(bossGateObj, gothicMat);
            EnsureMeshCollider(bossGateObj);
            EnsureTriggerCollider(bossGateObj, 4.5f);

            var fogWall = bossGateObj.AddComponent<FogWallArenaGate>();
            GameObject fogVisualObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogVisualObj.name = "Visual_WhiteLightFog";
            fogVisualObj.transform.SetParent(bossGateObj.transform);
            fogVisualObj.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            fogVisualObj.transform.localScale = new Vector3(6f, 7f, 1f);
            ApplyMaterialRecursively(fogVisualObj, lumenGlowMat);

            GameObject teleportPoint = new GameObject("Point_ArenaEntryTeleport");
            teleportPoint.transform.SetParent(bossGateObj.transform);
            teleportPoint.transform.localPosition = new Vector3(0f, 0.2f, 6.0f);

            SerializedObject fogSo = new SerializedObject(fogWall);
            fogSo.FindProperty("_associatedBossId").stringValue = "Boss_Apex_LordOfLight";
            fogSo.FindProperty("_blockingBarrierCollider").objectReferenceValue = bossGateObj.GetComponent<Collider>();
            fogSo.FindProperty("_fogVisualObject").objectReferenceValue = fogVisualObj;
            fogSo.FindProperty("_arenaEntryTeleportPoint").objectReferenceValue = teleportPoint.transform;
            fogSo.ApplyModifiedPropertiesWithoutUndo();

            // Celestial God Rays illuminating the Final Boss Arena
            GameObject raysPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/VFX_CavernGodRaysMesh.fbx");
            Material godRayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/MAT_URP_CavernGodRays.mat");
            if (raysPrefab != null && godRayMat != null)
            {
                GameObject apexRays = (GameObject)PrefabUtility.InstantiatePrefab(raysPrefab);
                apexRays.name = "VFX_ApexCelestialGodRays";
                apexRays.transform.SetParent(apexRoot.transform);
                apexRays.transform.position = apexCenter + new Vector3(0f, 0f, 28f);
                apexRays.transform.localScale = new Vector3(2.5f, 2.5f, 2.5f);
                foreach (var r in apexRays.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = godRayMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                foreach (var col in apexRays.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            }

            // ==========================================
            // LIGHT PROBES & OCCLUSION CULLING
            // ==========================================
            SetupLightProbeGroup(worldRoot.transform);

            // ==========================================
            // PLAYER, CAMERA & HUD
            // ==========================================
            SetupPlayerCharacter(characterPrefab, playerMat, mudMat, rockMat, out GameObject playerObj, out SoulsPlayerController playerCtrl);
            SetupFollowCamera(playerObj, out SoulsOrbitCamera orbitCam);
            SerializedObject pSo = new SerializedObject(playerCtrl);
            pSo.FindProperty("_orbitCamera").objectReferenceValue = orbitCam;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            // Cavern Atmospheric Audio Loop
            GameObject audioObj = new GameObject("AUDIO_CavernAmbience");
            AudioSource ambSource = audioObj.AddComponent<AudioSource>();
            ambSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/AMB_DeepCavern_Drips_Loop.wav");
            ambSource.loop = true;
            ambSource.volume = 0.72f;
            ambSource.playOnAwake = true;

            // Spawn Particles (Mist & Embers)
            SetupVfxParticles(layer0Root.transform);

            // Exploration HUD
            BuildExplorationHUD();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Compute Occlusion Culling
            Debug.Log("[CaveSouls] Computing Static Occlusion Culling...");
            StaticOcclusionCulling.Compute();
        }

        private static GameObject InstantiateModularPiece(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Transform parent, string name)
        {
            GameObject obj;
            if (prefab != null)
            {
                obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            obj.transform.rotation = rot;
            obj.transform.localScale = scale;
            if (mat != null) ApplyMaterialRecursively(obj, mat);
            EnsureMeshCollider(obj);
            MarkStaticRecursive(obj);
            return obj;
        }

        private static GameObject CreateBonfire(string bonfireId, Vector3 pos, GameObject bonfirePrefab, Material mat, Transform parent, bool isLitByDefault)
        {
            GameObject bonfireRoot = new GameObject(bonfireId);
            bonfireRoot.transform.SetParent(parent);
            bonfireRoot.transform.position = pos;

            GameObject visual = bonfirePrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(bonfirePrefab) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "BonfireVisual";
            visual.transform.SetParent(bonfireRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            if (mat != null) ApplyMaterialRecursively(visual, mat);

            // Warm flame point light
            GameObject flameLightObj = new GameObject("Light_BonfireFlame");
            flameLightObj.transform.SetParent(bonfireRoot.transform, false);
            flameLightObj.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            Light flameLight = flameLightObj.AddComponent<Light>();
            flameLight.type = LightType.Point;
            flameLight.color = new Color(1.0f, 0.65f, 0.25f, 1f);
            flameLight.intensity = 3.8f;
            flameLight.range = 10f;
            flameLight.shadows = LightShadows.Soft;

            // Trigger collider for interaction detection
            EnsureTriggerCollider(bonfireRoot, 3.0f);

            var checkpoint = bonfireRoot.AddComponent<BonfireCheckpoint>();
            SerializedObject cpSo = new SerializedObject(checkpoint);
            cpSo.FindProperty("_bonfireId").stringValue = bonfireId;
            cpSo.FindProperty("_respawnPoint").objectReferenceValue = bonfireRoot.transform;
            cpSo.FindProperty("_litFlameVfx").objectReferenceValue = flameLightObj;
            cpSo.FindProperty("_isLitByDefault").boolValue = isLitByDefault;
            cpSo.ApplyModifiedPropertiesWithoutUndo();

            return bonfireRoot;
        }

        private static void CreateLordSoulAltar(string name, Vector3 pos, int zoneIndex, string title, ApexSealMechanism apexMech, Material crystalMat, Transform parent)
        {
            GameObject altarObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altarObj.name = name;
            altarObj.transform.SetParent(parent);
            altarObj.transform.position = pos;
            altarObj.transform.localScale = new Vector3(2.5f, 0.8f, 2.5f);
            ApplyMaterialRecursively(altarObj, crystalMat);
            EnsureTriggerCollider(altarObj, 3.5f);

            GameObject soulVfx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            soulVfx.name = "VFX_GlowingLordSoul";
            soulVfx.transform.SetParent(altarObj.transform);
            soulVfx.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            soulVfx.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            ApplyMaterialRecursively(soulVfx, crystalMat);

            var altar = altarObj.AddComponent<LordSoulAltar>();
            var audio = altarObj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1.0f;
            var chimeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Lore_Lumen_Chime.wav");

            SerializedObject so = new SerializedObject(altar);
            so.FindProperty("_zoneIndex").intValue = zoneIndex;
            so.FindProperty("_altarSaveId").stringValue = "Altar_" + name;
            so.FindProperty("_lordSoulTitle").stringValue = title;
            so.FindProperty("_glowingSoulVfx").objectReferenceValue = soulVfx;
            so.FindProperty("_audioSource").objectReferenceValue = audio;
            so.FindProperty("_claimClip").objectReferenceValue = chimeClip;
            so.FindProperty("_hubApexMechanism").objectReferenceValue = apexMech;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateShortcutDoor(string name, Vector3 pos, Quaternion rot, string saveId, Material mat, Transform parent)
        {
            GameObject doorObj = new GameObject(name);
            doorObj.transform.SetParent(parent);
            doorObj.transform.position = pos;
            doorObj.transform.rotation = rot;
            EnsureTriggerCollider(doorObj, 3.0f);

            var shortcut = doorObj.AddComponent<OneWayShortcutDoor>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Hinge_DoorVisual";
            visual.transform.SetParent(doorObj.transform);
            visual.transform.localPosition = new Vector3(1.2f, 1.8f, 0f);
            visual.transform.localScale = new Vector3(2.4f, 3.5f, 0.3f);
            ApplyMaterialRecursively(visual, mat);

            SerializedObject so = new SerializedObject(shortcut);
            so.FindProperty("_doorSaveId").stringValue = saveId;
            so.FindProperty("_doorHingeTransform").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupLightProbeGroup(Transform parent)
        {
            GameObject lpgObj = new GameObject("PROBES_MacroWorld_LightProbeGroup");
            lpgObj.transform.SetParent(parent);
            LightProbeGroup lpg = lpgObj.AddComponent<LightProbeGroup>();

            List<Vector3> probePositions = new List<Vector3>();
            // Probes along Layer 0
            for (float z = -6f; z <= 60f; z += 12f)
            {
                probePositions.Add(new Vector3(-4f, 0f, z));
                probePositions.Add(new Vector3(4f, 0f, z));
                probePositions.Add(new Vector3(0f, 3f, z));
            }
            // Probes around Layer 1 (The Hub)
            for (float x = -15f; x <= 15f; x += 10f)
            {
                for (float z = 75f; z <= 105f; z += 10f)
                {
                    probePositions.Add(new Vector3(x, 24.5f, z));
                    probePositions.Add(new Vector3(x, 27.5f, z));
                }
            }
            // Probes Zone A (Spires)
            for (float y = 25f; y <= 62f; y += 12f)
            {
                probePositions.Add(new Vector3(-45f, y, 95f));
                probePositions.Add(new Vector3(-58f, y, 115f));
            }
            // Probes Zone B (Abyss)
            for (float z = 25f; z <= 75f; z += 15f)
            {
                probePositions.Add(new Vector3(65f, 7.5f, z));
                probePositions.Add(new Vector3(70f, 12f, z));
            }
            // Probes Zone C (City)
            for (float z = 95f; z <= 145f; z += 15f)
            {
                probePositions.Add(new Vector3(55f, 38f, z));
                probePositions.Add(new Vector3(72f, 50f, z));
            }
            // Probes Layer 3 (Apex)
            for (float z = 120f; z <= 170f; z += 12f)
            {
                probePositions.Add(new Vector3(-5f, 80.5f, z));
                probePositions.Add(new Vector3(5f, 80.5f, z));
                probePositions.Add(new Vector3(0f, 84f, z));
            }

            lpg.probePositions = probePositions.ToArray();
        }

        private static void SetupPlayerCharacter(
            GameObject characterPrefab,
            Material playerMat,
            Material mudMat,
            Material rockMat,
            out GameObject playerObj,
            out SoulsPlayerController playerCtrl)
        {
            playerObj = new GameObject("PLAYER_SoulsExplorer");
            playerObj.transform.position = new Vector3(0f, -1.15f, -5.0f);
            playerObj.transform.rotation = Quaternion.identity;

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.80f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.90f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 55f;

            AudioSource playerAudio = playerObj.AddComponent<AudioSource>();
            playerAudio.playOnAwake = false;
            playerAudio.spatialBlend = 0.3f;

            Transform visualTransform = null;
            if (characterPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
                visual.name = "MannequinVisual";
                visual.transform.SetParent(playerObj.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visualTransform = visual.transform;
                ApplyMaterialRecursively(visual, playerMat);
            }

            playerCtrl = playerObj.AddComponent<SoulsPlayerController>();

            AudioClip[] mudClips =
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Mud_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Mud_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Mud_03.wav")
            };
            AudioClip[] stoneClips =
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Stone_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Stone_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Footstep_Stone_03.wav")
            };

            SerializedObject playerSo = new SerializedObject(playerCtrl);
            playerSo.FindProperty("_characterController").objectReferenceValue = cc;
            playerSo.FindProperty("_visualModelTransform").objectReferenceValue = visualTransform;
            playerSo.FindProperty("_footstepAudioSource").objectReferenceValue = playerAudio;

            SerializedProperty mudArray = playerSo.FindProperty("_mudFootstepClips");
            mudArray.arraySize = mudClips.Length;
            for (int i = 0; i < mudClips.Length; i++) mudArray.GetArrayElementAtIndex(i).objectReferenceValue = mudClips[i];

            SerializedProperty stoneArray = playerSo.FindProperty("_stoneFootstepClips");
            stoneArray.arraySize = stoneClips.Length;
            for (int i = 0; i < stoneClips.Length; i++) stoneArray.GetArrayElementAtIndex(i).objectReferenceValue = stoneClips[i];

            playerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupFollowCamera(GameObject playerObj, out SoulsOrbitCamera orbitCam)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0.38f, -8.8f);
            camObj.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 280f;
            camObj.AddComponent<AudioListener>();

            var urpCamData = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;

            orbitCam = camObj.AddComponent<SoulsOrbitCamera>();
            orbitCam.Target = playerObj.transform;

            SerializedObject camSo = new SerializedObject(orbitCam);
            camSo.FindProperty("_target").objectReferenceValue = playerObj.transform;
            camSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupVfxParticles(Transform parent)
        {
            Material mistParticleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/MAT_VFX_MistParticles.mat");
            if (mistParticleMat != null)
            {
                GameObject mistObj = new GameObject("VFX_CavernRollingFog");
                mistObj.transform.SetParent(parent);
                mistObj.transform.position = new Vector3(0f, -1.1f, -3.5f);
                ParticleSystem mistPs = mistObj.AddComponent<ParticleSystem>();
                var mistRend = mistObj.GetComponent<ParticleSystemRenderer>();
                mistRend.sharedMaterial = mistParticleMat;
                mistRend.sortingFudge = 2f;
                var mistMain = mistPs.main;
                mistMain.loop = true;
                mistMain.prewarm = true;
                mistMain.startLifetime = 9.0f;
                mistMain.startSpeed = 0.25f;
                mistMain.startSize = 4.2f;
                mistMain.startColor = new Color(0.20f, 0.25f, 0.32f, 0.25f);
                mistMain.maxParticles = 60;
                var mistShape = mistPs.shape;
                mistShape.shapeType = ParticleSystemShapeType.Box;
                mistShape.scale = new Vector3(20f, 1.2f, 20f);
                var mistEm = mistPs.emission;
                mistEm.rateOverTime = 6f;
            }

            Material emberParticleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/MAT_VFX_EmberParticles.mat");
            GameObject emberObj = new GameObject("VFX_SpawnShaft_Embers");
            emberObj.transform.SetParent(parent);
            emberObj.transform.position = new Vector3(0f, -1.2f, -4.8f);
            ParticleSystem ps = emberObj.AddComponent<ParticleSystem>();
            var psRend = emberObj.GetComponent<ParticleSystemRenderer>();
            if (emberParticleMat != null) psRend.sharedMaterial = emberParticleMat;
            var main = ps.main;
            main.startLifetime = 4.8f;
            main.startSpeed = 0.55f;
            main.startSize = 0.09f;
            main.startColor = new Color(2.0f, 1.4f, 0.5f, 0.95f);
            main.maxParticles = 90;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 2.6f;
            var emission = ps.emission;
            emission.rateOverTime = 18f;
        }

        private static void BuildExplorationHUD()
        {
            GameObject hudCanvasObj = new GameObject("HUD_ExplorationCanvas");
            Canvas canvas = hudCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = hudCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            hudCanvasObj.AddComponent<GraphicRaycaster>();
            PlayerExplorationHUD hudCtrl = hudCanvasObj.AddComponent<PlayerExplorationHUD>();

            // Controls Tutorial Banner
            GameObject controlsObj = new GameObject("ControlsBanner");
            controlsObj.transform.SetParent(hudCanvasObj.transform, false);
            var controlsRt = controlsObj.AddComponent<RectTransform>();
            controlsRt.anchorMin = new Vector2(0.5f, 1f);
            controlsRt.anchorMax = new Vector2(0.5f, 1f);
            controlsRt.pivot = new Vector2(0.5f, 1f);
            controlsRt.anchoredPosition = new Vector2(0f, -40f);
            controlsRt.sizeDelta = new Vector2(900f, 44f);
            var controlsCg = controlsObj.AddComponent<CanvasGroup>();
            var controlsBg = controlsObj.AddComponent<Image>();
            controlsBg.color = new Color(0.02f, 0.025f, 0.035f, 0.78f);

            GameObject controlsTxtObj = new GameObject("ControlsText");
            controlsTxtObj.transform.SetParent(controlsObj.transform, false);
            var controlsTxtRt = controlsTxtObj.AddComponent<RectTransform>();
            controlsTxtRt.anchorMin = Vector2.zero;
            controlsTxtRt.anchorMax = Vector2.one;
            controlsTxtRt.sizeDelta = Vector2.zero;
            Text controlsTxt = controlsTxtObj.AddComponent<Text>();
            controlsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            controlsTxt.fontSize = 17;
            controlsTxt.alignment = TextAnchor.MiddleCenter;
            controlsTxt.color = new Color(0.92f, 0.88f, 0.76f, 1f);
            controlsTxt.text = "[ W A S D ]  M O V E R S E     |     [ S H I F T ]  C O R R E R     |     [ R A T Ó N ]  C Á M A R A";

            // Interaction Prompt
            GameObject promptObj = new GameObject("InteractionPrompt");
            promptObj.transform.SetParent(hudCanvasObj.transform, false);
            var promptRt = promptObj.AddComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0.5f, 0.35f);
            promptRt.anchorMax = new Vector2(0.5f, 0.35f);
            promptRt.pivot = new Vector2(0.5f, 0.5f);
            promptRt.sizeDelta = new Vector2(380f, 42f);
            var promptCg = promptObj.AddComponent<CanvasGroup>();
            promptCg.alpha = 0f;
            var promptBg = promptObj.AddComponent<Image>();
            promptBg.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);

            GameObject promptTxtObj = new GameObject("PromptText");
            promptTxtObj.transform.SetParent(promptObj.transform, false);
            var promptTxtRt = promptTxtObj.AddComponent<RectTransform>();
            promptTxtRt.anchorMin = Vector2.zero;
            promptTxtRt.anchorMax = Vector2.one;
            promptTxtRt.sizeDelta = Vector2.zero;
            Text promptTxt = promptTxtObj.AddComponent<Text>();
            promptTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptTxt.fontSize = 18;
            promptTxt.alignment = TextAnchor.MiddleCenter;
            promptTxt.color = new Color(1.0f, 0.94f, 0.78f, 1f);
            promptTxt.text = "[ E ]   E X A M I N A R";

            // Lore Dialogue Banner
            GameObject loreBannerObj = new GameObject("LoreBanner");
            loreBannerObj.transform.SetParent(hudCanvasObj.transform, false);
            var loreRt = loreBannerObj.AddComponent<RectTransform>();
            loreRt.anchorMin = new Vector2(0.5f, 0.18f);
            loreRt.anchorMax = new Vector2(0.5f, 0.18f);
            loreRt.pivot = new Vector2(0.5f, 0.5f);
            loreRt.sizeDelta = new Vector2(780f, 110f);
            var loreCg = loreBannerObj.AddComponent<CanvasGroup>();
            loreCg.alpha = 0f;
            var loreBg = loreBannerObj.AddComponent<Image>();
            loreBg.color = new Color(0.02f, 0.022f, 0.032f, 0.92f);

            GameObject loreTitleObj = new GameObject("LoreTitle");
            loreTitleObj.transform.SetParent(loreBannerObj.transform, false);
            var loreTitleRt = loreTitleObj.AddComponent<RectTransform>();
            loreTitleRt.anchorMin = new Vector2(0f, 0.6f);
            loreTitleRt.anchorMax = new Vector2(1f, 1f);
            loreTitleRt.offsetMin = new Vector2(20f, 0f);
            loreTitleRt.offsetMax = new Vector2(-20f, -8f);
            Text loreTitleTxt = loreTitleObj.AddComponent<Text>();
            loreTitleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loreTitleTxt.fontSize = 19;
            loreTitleTxt.alignment = TextAnchor.MiddleCenter;
            loreTitleTxt.color = new Color(1.0f, 0.88f, 0.65f, 1f);

            GameObject loreDescObj = new GameObject("LoreDesc");
            loreDescObj.transform.SetParent(loreBannerObj.transform, false);
            var loreDescRt = loreDescObj.AddComponent<RectTransform>();
            loreDescRt.anchorMin = new Vector2(0f, 0f);
            loreDescRt.anchorMax = new Vector2(1f, 0.6f);
            loreDescRt.offsetMin = new Vector2(24f, 10f);
            loreDescRt.offsetMax = new Vector2(-24f, 0f);
            Text loreDescTxt = loreDescObj.AddComponent<Text>();
            loreDescTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loreDescTxt.fontSize = 16;
            loreDescTxt.alignment = TextAnchor.MiddleCenter;
            loreDescTxt.lineSpacing = 1.2f;
            loreDescTxt.color = new Color(0.86f, 0.86f, 0.82f, 1f);

            // Wire HUD properties
            SerializedObject hudSo = new SerializedObject(hudCtrl);
            hudSo.FindProperty("_controlsBannerGroup").objectReferenceValue = controlsCg;
            hudSo.FindProperty("_controlsBannerText").objectReferenceValue = controlsTxt;
            hudSo.FindProperty("_promptGroup").objectReferenceValue = promptCg;
            hudSo.FindProperty("_promptText").objectReferenceValue = promptTxt;
            hudSo.FindProperty("_loreBannerGroup").objectReferenceValue = loreCg;
            hudSo.FindProperty("_loreTitleText").objectReferenceValue = loreTitleTxt;
            hudSo.FindProperty("_loreDescriptionText").objectReferenceValue = loreDescTxt;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateFillLight(string name, Vector3 pos, Color color, float intensity, float range, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            Light light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void ApplyMaterialRecursively(GameObject root, Material mat)
        {
            if (root == null || mat == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
            }
        }

        private static void EnsureMeshCollider(GameObject obj)
        {
            if (obj == null) return;
            // If the model already has colliders (e.g. from UCX_ postprocessing), don't duplicate
            if (obj.GetComponentInChildren<Collider>(true) != null) return;

            var filters = obj.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        private static void EnsureTriggerCollider(GameObject obj, float radius)
        {
            if (obj == null) return;
            var sc = obj.GetComponent<SphereCollider>();
            if (sc == null) sc = obj.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;
        }

        private static void MarkStaticRecursive(GameObject obj)
        {
            if (obj == null) return;
            StaticEditorFlags flags = StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.ContributeGI;
            GameObjectUtility.SetStaticEditorFlags(obj, flags);
            foreach (Transform child in obj.transform)
            {
                MarkStaticRecursive(child.gameObject);
            }
        }

        private static void SetSurfaceType(SurfaceMaterialIdentifier identifier, SurfaceMaterialType surfaceType)
        {
            if (identifier == null) return;
            SerializedObject so = new SerializedObject(identifier);
            SerializedProperty prop = so.FindProperty("_surfaceType");
            if (prop != null)
            {
                prop.enumValueIndex = (int)surfaceType;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void RegisterInBuildSettings()
        {
            string[] scenePaths =
            {
                "Assets/_Project/Scenes/00_SplashScreen.unity",
                "Assets/_Project/Scenes/01_MainMenu.unity",
                "Assets/_Project/Scenes/02_IntroCinematic.unity",
                "Assets/_Project/Scenes/03_PrimordialMudBed.unity"
            };

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (string path in scenePaths)
            {
                if (File.Exists(path))
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[CaveSouls] Registered {list.Count} scenes in EditorBuildSettings.");
        }
    }
}
