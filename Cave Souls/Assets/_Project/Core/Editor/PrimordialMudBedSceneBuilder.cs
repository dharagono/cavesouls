using System.IO;
using CaveSouls.Core;
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
    /// Master Scene Builder & Level Assembler for "El Lecho de Lodo Primordial" (03_PrimordialMudBed.unity).
    /// Configures 4K PBR texture streaming, URP materials, modular rock topology,
    /// volumetric subterranean lighting, deep mud debuff zones, lore interactables,
    /// third-person player character, and zero-GC telemetry.
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
                out Material playerMat);

            VolumeProfile volumeProfile = CreateOrUpdateCavernVolumeProfile();
            AssembleSceneHierarchy(mudMat, rockMat, petrifiedMat, lumenGlowMat, playerMat, volumeProfile);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaveSouls] Successfully assembled and saved 03_PrimordialMudBed.unity!");
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

            // Normal Maps
            string[] normals =
            {
                mudDir + "/TEX_PrimordialMud_NormalGL.jpg",
                rockDir + "/TEX_CavernWallRock_NormalGL.jpg"
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
                        ti.maxTextureSize = 4096;
                        ti.streamingMipmaps = true;
                        ti.SaveAndReimport();
                    }
                }
            }

            // Mask Maps
            string[] masks =
            {
                mudDir + "/TEX_PrimordialMud_MaskMap.png",
                rockDir + "/TEX_CavernWallRock_MaskMap.png"
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
                        ti.maxTextureSize = 4096;
                        ti.streamingMipmaps = true;
                        ti.SaveAndReimport();
                    }
                }
            }

            // Base Color Maps
            string[] baseColors =
            {
                mudDir + "/TEX_PrimordialMud_BaseColor.jpg",
                rockDir + "/TEX_CavernWallRock_BaseColor.jpg"
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
                        ti.maxTextureSize = 4096;
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
            out Material playerMat)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            string mudDir = TexturesDir + "/Terrain4K";
            string rockDir = TexturesDir + "/Terrain4K";

            // 1. Mud Terrain Material
            string mudMatPath = MaterialsDir + "/MAT_URP_PrimordialMudBed.mat";
            mudMat = AssetDatabase.LoadAssetAtPath<Material>(mudMatPath);
            if (mudMat == null)
            {
                mudMat = new Material(litShader);
                AssetDatabase.CreateAsset(mudMat, mudMatPath);
            }
            mudMat.color = Color.white;
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
            mudMat.SetFloat("_Smoothness", 0.76f); // Viscous wetness sheen
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

            // 3. Petrified Mud/Crystal Humanoid Material
            string petPath = MaterialsDir + "/MAT_URP_PetrifiedHumanoid.mat";
            petrifiedMat = AssetDatabase.LoadAssetAtPath<Material>(petPath);
            if (petrifiedMat == null)
            {
                petrifiedMat = new Material(litShader);
                AssetDatabase.CreateAsset(petrifiedMat, petPath);
            }
            petrifiedMat.color = new Color(0.18f, 0.17f, 0.16f, 1f);
            petrifiedMat.SetFloat("_Smoothness", 0.50f);
            if (mudNormal != null)
            {
                petrifiedMat.SetTexture("_BumpMap", mudNormal);
                petrifiedMat.EnableKeyword("_NORMALMAP");
            }

            // 4. Player Mannequin Material
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

            // 6. Particle Dot Texture
            string dotTexPath = "Assets/_Project/Art/VFX/TEX_ParticleSoftDot.png";
            AssetDatabase.ImportAsset(dotTexPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var dotTex = AssetDatabase.LoadAssetAtPath<Texture2D>(dotTexPath);

            // 7. Mist Particles Material
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
                mistMat.SetFloat("_Surface", 1f); // Transparent
                mistMat.SetFloat("_Blend", 0f); // Alpha blend
                mistMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mistMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mistMat.SetInt("_ZWrite", 0);
                mistMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mistMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mistMat.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.30f, 0.20f));
                if (dotTex != null) mistMat.SetTexture("_BaseMap", dotTex);

                // 8. Ember Particles Material
                string emberPath = MaterialsDir + "/MAT_VFX_EmberParticles.mat";
                Material emberMat = AssetDatabase.LoadAssetAtPath<Material>(emberPath);
                if (emberMat == null)
                {
                    emberMat = new Material(particleShader);
                    AssetDatabase.CreateAsset(emberMat, emberPath);
                }
                emberMat.SetFloat("_Surface", 1f); // Transparent
                emberMat.SetFloat("_Blend", 1f); // Additive
                emberMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                emberMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                emberMat.SetInt("_ZWrite", 0);
                emberMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                emberMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                emberMat.EnableKeyword("_BLENDMODE_ADD");
                emberMat.SetColor("_BaseColor", new Color(2.5f, 1.8f, 0.6f, 1.0f));
                if (dotTex != null) emberMat.SetTexture("_BaseMap", dotTex);
            }

            // 9. Volumetric God Rays Material
            string godRayMatPath = MaterialsDir + "/MAT_URP_CavernGodRays.mat";
            Material godRaysMat = AssetDatabase.LoadAssetAtPath<Material>(godRayMatPath);
            if (godRaysMat != null)
            {
                godRaysMat.SetColor("_BeamColor", new Color(1.35f, 1.15f, 0.75f, 0.45f));
                godRaysMat.SetColor("_CoreColor", new Color(1.85f, 1.55f, 0.95f, 1.0f));
                godRaysMat.SetFloat("_EnableWorldHeightFade", 0.0f);
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

            // ACES Tonemapping
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            // Color Adjustments
            if (!profile.TryGet(out ColorAdjustments colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.active = true;
            colorAdj.postExposure.Override(0.35f);
            colorAdj.contrast.Override(20f);
            colorAdj.saturation.Override(-10f);
            colorAdj.colorFilter.Override(new Color(0.96f, 0.98f, 1.0f, 1f));

            // Bloom
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.75f);
            bloom.tint.Override(new Color(1.0f, 0.92f, 0.80f, 1.0f));

            // Shadows, Midtones, Highlights
            if (!profile.TryGet(out ShadowsMidtonesHighlights smh))
            {
                smh = profile.Add<ShadowsMidtonesHighlights>(true);
            }
            smh.active = true;
            smh.shadows.Override(new Vector4(0.88f, 0.94f, 1.05f, 0f));
            smh.midtones.Override(new Vector4(0.98f, 0.97f, 0.95f, 0f));
            smh.highlights.Override(new Vector4(1.08f, 1.00f, 0.90f, 0f));

            // Vignette
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.active = true;
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.40f);

            return profile;
        }

        private static void AssembleSceneHierarchy(
            Material mudMat,
            Material rockMat,
            Material petrifiedMat,
            Material lumenGlowMat,
            Material playerMat,
            VolumeProfile volumeProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Atmosphere & Fog Settings (Deep Subterranean Cavern - Linear mist)
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.26f, 0.32f, 1f); // Deep cold subterranean bounce
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 75f;
            RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.12f, 1f);

            // 2. Global Directional Light (Simulating sunlight filtering through ceiling cracks)
            GameObject dirLightObj = new GameObject("LIGHT_CavernCelestialShaft");
            Light dirLight = dirLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.85f, 0.92f, 1.0f, 1f);
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.75f;
            dirLightObj.transform.position = new Vector3(8f, 28f, -4f);
            dirLightObj.transform.rotation = Quaternion.Euler(65f, 35f, 0f);

            // 3. Spawn Shaft Golden Spot Light (Directly illuminating "La Fosa Cálida")
            GameObject spawnSpotObj = new GameObject("LIGHT_SpawnShaft_GoldFocus");
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

            // 4. Subtle Ambient Cave Fill Lights
            CreateFillLight("LIGHT_SpawnBasin_WarmFill", new Vector3(0f, -0.2f, -4.8f), new Color(1.0f, 0.75f, 0.35f), 4.2f, 9f);
            CreateFillLight("LIGHT_CaveFill_MidSection", new Vector3(0f, 5f, 18f), new Color(0.38f, 0.48f, 0.65f), 3.0f, 22f);
            CreateFillLight("LIGHT_CaveFill_Arena", new Vector3(0f, 6f, 30f), new Color(0.35f, 0.45f, 0.62f), 3.2f, 25f);
            CreateFillLight("LIGHT_CaveFill_UpperRamp", new Vector3(0f, 10f, 48f), new Color(0.55f, 0.45f, 0.32f), 3.5f, 24f);

            // 5. Global Post-Processing Volume
            GameObject volumeObj = new GameObject("VOLUME_GlobalPostProcessing");
            Volume volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.profile = volumeProfile;

            // 6. Terrain Basin ("El Lecho de Lodo Primordial")
            GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_PrimordialMudBed_Terrain.fbx");
            GameObject terrainObj;
            if (terrainPrefab != null)
            {
                terrainObj = (GameObject)PrefabUtility.InstantiatePrefab(terrainPrefab);
            }
            else
            {
                terrainObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                terrainObj.transform.localScale = new Vector3(5f, 1f, 7f);
            }
            terrainObj.name = "ENV_PrimordialMudBed_Terrain";
            terrainObj.transform.position = Vector3.zero;
            terrainObj.transform.rotation = Quaternion.identity;
            ApplyMaterialRecursively(terrainObj, mudMat);
            EnsureMeshCollider(terrainObj);
            var terrainSurface = terrainObj.AddComponent<SurfaceMaterialIdentifier>();
            // Use Reflection/SerializedProperty to set Dirt type
            SetSurfaceType(terrainSurface, SurfaceMaterialType.Dirt);

            // 7. Modular Cavern Framing (Pillars & Overhang Ledges)
            GameObject pillarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_CavernWall_Pillar_A.fbx");
            GameObject ledgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/ENV_Cavern_Rock_Ledge_B.fbx");
            GameObject pillarsRoot = new GameObject("GROUP_CavernPillarsAndOverhangs");

            // Pillar placements forming the canyon walls flanking the central path
            Vector3[] pillarPositions =
            {
                // Flanking Spawn Basin (Behind and to sides, leaving clear 8m path)
                new Vector3(-8.5f, -1.8f, -7.0f),
                new Vector3(8.5f, -1.8f, -7.0f),
                new Vector3(-8.2f, -1.6f, -1.0f),
                new Vector3(8.2f, -1.6f, -1.0f),

                // Canyon corridor winding path
                new Vector3(-7.5f, -0.6f, 5.0f),
                new Vector3(7.8f, -0.6f, 5.0f),
                new Vector3(-8.0f, 0.4f, 11.5f),
                new Vector3(7.5f, 0.4f, 11.5f),

                // Tutorial chokepoint (narrower corridor, 6m wide)
                new Vector3(-6.2f, 1.2f, 17.5f),
                new Vector3(6.5f, 1.2f, 18.0f),

                // Outer perimeter cliffs flanking lower section
                new Vector3(-15.0f, 0.0f, -6.0f),
                new Vector3(15.0f, 0.0f, -6.0f),
                new Vector3(-15.5f, 1.0f, 8.0f),
                new Vector3(15.5f, 1.0f, 8.0f),

                // Conflict Basin Arena (Wide mud clearing, Z = 24 to 36)
                new Vector3(-12.0f, 2.2f, 24.0f),
                new Vector3(12.5f, 2.2f, 24.0f),
                new Vector3(-13.0f, 2.8f, 32.0f),
                new Vector3(13.0f, 2.8f, 32.0f),
                new Vector3(-10.5f, 3.5f, 39.0f),
                new Vector3(10.8f, 3.5f, 39.0f),

                // Outer perimeter cliffs flanking arena
                new Vector3(-18.0f, 3.5f, 28.0f),
                new Vector3(18.0f, 3.5f, 28.0f),

                // Bottleneck ramp towards ruined foundations (Z = 44 to 56)
                new Vector3(-6.5f, 5.0f, 46.0f),
                new Vector3(6.5f, 5.0f, 46.0f),
                new Vector3(-7.5f, 7.5f, 54.0f),
                new Vector3(7.5f, 7.5f, 54.0f)
            };

            for (int i = 0; i < pillarPositions.Length; i++)
            {
                GameObject pillar;
                if (pillarPrefab != null)
                {
                    pillar = (GameObject)PrefabUtility.InstantiatePrefab(pillarPrefab);
                }
                else
                {
                    pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.transform.localScale = new Vector3(4f, 8f, 4f);
                }
                pillar.name = $"Pillar_{i + 1:D2}";
                pillar.transform.SetParent(pillarsRoot.transform);
                pillar.transform.position = pillarPositions[i];
                pillar.transform.rotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
                float randomScale = 0.82f + (i % 3) * 0.10f;
                pillar.transform.localScale = new Vector3(randomScale, randomScale * 1.15f, randomScale);
                ApplyMaterialRecursively(pillar, rockMat);
                EnsureMeshCollider(pillar);
                var surf = pillar.AddComponent<SurfaceMaterialIdentifier>();
                SetSurfaceType(surf, SurfaceMaterialType.Stone);
            }

            // Overhang Ledges placed high above to frame the cavern ceiling
            Vector3[] ledgePositions =
            {
                new Vector3(-8.5f, 13.5f, 0.0f),
                new Vector3(8.5f, 14.0f, 3.0f),
                new Vector3(-9.0f, 15.0f, 14.0f),
                new Vector3(9.0f, 15.5f, 20.0f),
                new Vector3(-11.0f, 17.0f, 30.0f),
                new Vector3(11.0f, 17.5f, 36.0f),
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
                }
            }

            // 8. Deep Mud Debuff Zones (Forces 70% speed debuff and mud footsteps)
            GameObject mudZonesRoot = new GameObject("GROUP_DeepMudDebuffZones");

            // Spawn Basin Deep Mud Zone
            GameObject spawnMudZone = new GameObject("ZONE_SpawnMudBasin");
            spawnMudZone.transform.SetParent(mudZonesRoot.transform);
            spawnMudZone.transform.position = new Vector3(0f, -1.2f, -4.5f);
            var box1 = spawnMudZone.AddComponent<BoxCollider>();
            box1.isTrigger = true;
            box1.size = new Vector3(14f, 2.5f, 13f);
            spawnMudZone.AddComponent<MudDebuffZone>();

            // Conflict Arena Deep Mud Zone
            GameObject arenaMudZone = new GameObject("ZONE_ConflictArenaMud");
            arenaMudZone.transform.SetParent(mudZonesRoot.transform);
            arenaMudZone.transform.position = new Vector3(0f, 2.6f, 28f);
            var box2 = arenaMudZone.AddComponent<BoxCollider>();
            box2.isTrigger = true;
            box2.size = new Vector3(18f, 2.8f, 16f);
            arenaMudZone.AddComponent<MudDebuffZone>();

            // 9. Lore Point of Interest: Remains of Surface Garments ("Ropa de Superficie")
            GameObject lorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/PROP_SurfaceGarments_Remains.fbx");
            GameObject loreObj;
            if (lorePrefab != null)
            {
                loreObj = (GameObject)PrefabUtility.InstantiatePrefab(lorePrefab);
            }
            else
            {
                loreObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                loreObj.transform.localScale = new Vector3(1f, 0.5f, 0.7f);
            }
            loreObj.name = "PROP_SurfaceGarments_Remains";
            loreObj.transform.position = new Vector3(1.6f, -1.30f, -3.2f);
            loreObj.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
            ApplyMaterialRecursively(loreObj, rockMat);
            EnsureMeshCollider(loreObj);

            // Add ethereal Lumen glow light on the lore object
            GameObject lumenLightObj = new GameObject("LIGHT_LumenShardGlow");
            lumenLightObj.transform.SetParent(loreObj.transform);
            lumenLightObj.transform.localPosition = new Vector3(0.08f, 0.7f, 0.05f);
            Light lumenLight = lumenLightObj.AddComponent<Light>();
            lumenLight.type = LightType.Point;
            lumenLight.color = new Color(0.40f, 0.85f, 1.0f, 1f);
            lumenLight.intensity = 1.6f;
            lumenLight.range = 3.5f;

            // Wire LoreInteractable component
            var loreInteractable = loreObj.AddComponent<LoreInteractable>();
            var loreAudio = loreObj.AddComponent<AudioSource>();
            loreAudio.playOnAwake = false;
            loreAudio.spatialBlend = 1.0f;
            loreAudio.minDistance = 2f;
            loreAudio.maxDistance = 15f;
            var chimeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/SFX_Lore_Lumen_Chime.wav");

            SerializedObject loreSo = new SerializedObject(loreInteractable);
            loreSo.FindProperty("_interactChimeClip").objectReferenceValue = chimeClip;
            loreSo.FindProperty("_audioSource").objectReferenceValue = loreAudio;
            loreSo.FindProperty("_crystalGlowLight").objectReferenceValue = lumenLight;
            loreSo.ApplyModifiedPropertiesWithoutUndo();

            // 10. Petrified Mud Humanoid Corpses (Environmental Lore)
            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsDir + "/CHR_PlayerBaseMannequin.fbx");
            if (characterPrefab != null)
            {
                GameObject bodiesRoot = new GameObject("GROUP_PetrifiedHumanoidCorpses");
                (Vector3 pos, Vector3 rot)[] bodyTransforms =
                {
                    (new Vector3(-1.6f, -1.35f, -3.2f), new Vector3(82f, 40f, -15f)),
                    (new Vector3(3.8f, 1.2f, 14f), new Vector3(15f, -70f, 25f)),
                    (new Vector3(-4.5f, 2.4f, 26f), new Vector3(65f, 120f, 0f)),
                    (new Vector3(2.5f, 5.2f, 44f), new Vector3(80f, -20f, 10f))
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

            // 11. Playable Third-Person Character (Spawn in "La Fosa Cálida" under the golden beam)
            GameObject playerObj = new GameObject("PLAYER_SoulsExplorer");
            playerObj.transform.position = new Vector3(0f, -1.48f, -5.0f);
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

            // Visual Mannequin Child
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

            SoulsPlayerController playerCtrl = playerObj.AddComponent<SoulsPlayerController>();

            // Load audio clips
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

            // 12. Orbital Follow Camera with Collision Avoidance
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 0.38f, -8.8f);
            camObj.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 250f;
            camObj.AddComponent<AudioListener>();

            // Enable URP Camera Data
            var urpCamData = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;

            SoulsOrbitCamera orbitCam = camObj.AddComponent<SoulsOrbitCamera>();
            orbitCam.Target = playerObj.transform;

            SerializedObject camSo = new SerializedObject(orbitCam);
            camSo.FindProperty("_target").objectReferenceValue = playerObj.transform;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire camera back to player controller
            playerSo.Update();
            playerSo.FindProperty("_orbitCamera").objectReferenceValue = orbitCam;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            // 13. Cavern Atmospheric Audio Loop (Water drips + deep hollow rumble)
            GameObject audioObj = new GameObject("AUDIO_CavernAmbience");
            AudioSource ambSource = audioObj.AddComponent<AudioSource>();
            ambSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/AMB_DeepCavern_Drips_Loop.wav");
            ambSource.loop = true;
            ambSource.volume = 0.72f;
            ambSource.playOnAwake = true;

            // 14. Volumetric Celestial God Rays Beam (Illuminating the spawn fosa)
            GameObject raysPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/VFX_CavernGodRaysMesh.fbx");
            Material godRayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/MAT_URP_CavernGodRays.mat");
            if (raysPrefab != null && godRayMat != null)
            {
                GameObject raysInst = (GameObject)PrefabUtility.InstantiatePrefab(raysPrefab);
                raysInst.name = "VFX_VolumetricGodRays";
                raysInst.transform.position = new Vector3(0f, -1.45f, -4.8f);
                raysInst.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                raysInst.transform.localScale = new Vector3(1.2f, 1.15f, 1.2f);
                foreach (var r in raysInst.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = godRayMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                foreach (var col in raysInst.GetComponentsInChildren<Collider>(true))
                {
                    col.enabled = false;
                }
            }

            // 15. Real-Time Particle System 1: Low Subterranean Rolling Mist
            Material mistParticleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/MAT_VFX_MistParticles.mat");
            if (mistParticleMat != null)
            {
                GameObject mistObj = new GameObject("VFX_CavernRollingFog");
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

            // 16. Real-Time Particle System 2: Golden Drifting Embers
            Material emberParticleMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/MAT_VFX_EmberParticles.mat");
            GameObject emberObj = new GameObject("VFX_SpawnShaft_Embers");
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

            // 17. Exploration HUD Canvas
            BuildExplorationHUD();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
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

            // 1. Controls Tutorial Banner (Top Center)
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

            // 2. Interaction Prompt (Center Bottom)
            GameObject promptObj = new GameObject("InteractionPrompt");
            promptObj.transform.SetParent(hudCanvasObj.transform, false);
            var promptRt = promptObj.AddComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0.5f, 0.35f);
            promptRt.anchorMax = new Vector2(0.5f, 0.35f);
            promptRt.pivot = new Vector2(0.5f, 0.5f);
            promptRt.sizeDelta = new Vector2(320f, 40f);
            var promptCg = promptObj.AddComponent<CanvasGroup>();
            promptCg.alpha = 0f;
            var promptBg = promptObj.AddComponent<Image>();
            promptBg.color = new Color(0.04f, 0.05f, 0.07f, 0.82f);

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

            // 3. Lore Dialogue Banner (Bottom Center)
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

        private static void CreateFillLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            GameObject obj = new GameObject(name);
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
