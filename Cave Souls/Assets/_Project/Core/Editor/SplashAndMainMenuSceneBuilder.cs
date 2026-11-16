using System.IO;
using CaveSouls.Features.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CaveSouls.Core.Editor
{
    public static class SplashAndMainMenuSceneBuilder
    {
        private const string ScenesDir = "Assets/_Project/Scenes";
        private const string VfxDir = "Assets/_Project/Art/VFX";
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string TexturesDir = "Assets/_Project/Art/Textures";
        private const string SettingsDir = "Assets/_Project/Art/Settings";
        private const string AudioDir = "Assets/_Project/Art/Audio";
        private const string SplashScenePath = ScenesDir + "/00_SplashScreen.unity";
        private const string MainMenuScenePath = ScenesDir + "/01_MainMenu.unity";
        private const string IntroCinematicScenePath = ScenesDir + "/02_IntroCinematic.unity";

        [MenuItem("CaveSouls/Build Splash & Main Menu Scenes")]
        public static void BuildAll()
        {
            EnsureDirectories();
            EnsurePbrTextureImportSettings();
            ConfigureUrpDepthAndOpaqueTextures();

            GenerateProceduralVfxTexturesAndMaterials(
                out Material rockMat,
                out Material godRayMat,
                out Material mistParticleMat,
                out Material emberParticleMat,
                out Sprite unityCubeEmblemSprite,
                out Sprite uiDarkBannerSprite);

            VolumeProfile eldenRingProfile = CreateOrUpdateEldenRingVolumeProfile();
            IntroCinematicDataSO introData = CreateOrUpdateIntroCinematicDataSO();

            BuildSplashScreenScene(unityCubeEmblemSprite);
            BuildIntroCinematicScene(introData, uiDarkBannerSprite);
            BuildMainMenuScene(rockMat, godRayMat, mistParticleMat, emberParticleMat, eldenRingProfile, uiDarkBannerSprite, introData);
            RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaveSouls] Successfully built 00_SplashScreen.unity, 01_MainMenu.unity (with EN/ES Settings), and 02_IntroCinematic.unity (1:33 Intro)!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
            if (!Directory.Exists(VfxDir)) Directory.CreateDirectory(VfxDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
            if (!Directory.Exists(TexturesDir)) Directory.CreateDirectory(TexturesDir);
            if (!Directory.Exists(SettingsDir)) Directory.CreateDirectory(SettingsDir);
            if (!Directory.Exists(AudioDir)) Directory.CreateDirectory(AudioDir);
        }

        private static void EnsurePbrTextureImportSettings()
        {
            string[] normalPaths =
            {
                TexturesDir + "/TEX_CavernCliff_NormalGL.png",
                TexturesDir + "/TEX_CavernMoss_NormalGL.png"
            };
            foreach (string path in normalPaths)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.sRGBTexture = false;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.anisoLevel = 8;
                    ti.SaveAndReimport();
                }
            }

            string[] maskPaths =
            {
                TexturesDir + "/TEX_CavernCliff_MaskMap.png",
                TexturesDir + "/TEX_CavernMoss_MaskMap.png"
            };
            foreach (string path in maskPaths)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.sRGBTexture)
                {
                    ti.sRGBTexture = false;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.anisoLevel = 8;
                    ti.SaveAndReimport();
                }
            }

            // Ensure all 10 2.7K (2752x1536) Intro Cinematic slides have maxTextureSize = 4096 so Sprite rect is valid
            for (int i = 1; i <= 10; i++)
            {
                string slidePath = $"Assets/_Project/Art/UI/IntroCinematic/UI_IntroSlide_{i:D2}.png";
                if (AssetImporter.GetAtPath(slidePath) is TextureImporter ti &&
                    (ti.textureType != TextureImporterType.Sprite || ti.maxTextureSize < 4096))
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.maxTextureSize = 4096;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
            }
        }

        private static void ConfigureUrpDepthAndOpaqueTextures()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.supportsCameraDepthTexture = true;
                urpAsset.supportsCameraOpaqueTexture = true;
                EditorUtility.SetDirty(urpAsset);
            }
        }

        private static VolumeProfile CreateOrUpdateEldenRingVolumeProfile()
        {
            string profilePath = SettingsDir + "/Profile_MainMenu_EldenRing.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            // 1. ACES Tonemapping (high dynamic range filmic roll-off)
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            // 2. Color Adjustments (LookDev Directive: Contrast +15, Saturation -12)
            if (!profile.TryGet(out ColorAdjustments colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.active = true;
            colorAdj.postExposure.Override(0.28f);
            colorAdj.contrast.Override(15f);
            colorAdj.saturation.Override(-12f);
            colorAdj.colorFilter.Override(new Color(0.95f, 0.97f, 1.0f, 1.0f));

            // 3. Bloom (LookDev Directive: Threshold 1.1, Intensity 0.35, warm tint)
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1.0f, 0.86f, 0.68f, 1.0f));

            // 4. Subtle Dark Fantasy Vignette
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }
            vignette.active = true;
            vignette.color.Override(new Color(0.01f, 0.012f, 0.018f, 1.0f));
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void GenerateProceduralVfxTexturesAndMaterials(
            out Material rockMat,
            out Material godRayMat,
            out Material mistParticleMat,
            out Material emberParticleMat,
            out Sprite unityCubeEmblemSprite,
            out Sprite uiDarkBannerSprite)
        {
            // 1. Soft Radial Mist & Ember Texture (128x128 Gaussian falloff so particles are soft clouds/sparks)
            string mistTexPath = VfxDir + "/TEX_SoftRadialCloud.png";
            if (!File.Exists(mistTexPath))
            {
                Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        float nx = (x / 127f) * 2f - 1f;
                        float ny = (y / 127f) * 2f - 1f;
                        float distSq = nx * nx + ny * ny;
                        float alpha = Mathf.Clamp01(1f - distSq);
                        alpha = alpha * alpha * alpha;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                tex.Apply();
                File.WriteAllBytes(mistTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            // 2. FromSoftware Horizontal Dark Gradient Banner Sprite (for "PRESS ANY BUTTON" & Menu Selection)
            string bannerPath = "Assets/_Project/Art/UI/UI_SoulsMenuBanner.png";
            if (!File.Exists(bannerPath))
            {
                Texture2D banTex = new Texture2D(256, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                {
                    float vy = Mathf.Sin((y / 31f) * Mathf.PI);
                    for (int x = 0; x < 256; x++)
                    {
                        float ux = Mathf.Sin((x / 255f) * Mathf.PI);
                        float a = (ux * ux) * (vy * 0.85f + 0.15f);
                        banTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                banTex.Apply();
                File.WriteAllBytes(bannerPath, banTex.EncodeToPNG());
                Object.DestroyImmediate(banTex);
            }

            // 3. Minimalist Isometric Unity-Style Cube Emblem Sprite for "MADE WITH UNITY" Splash
            string emblemPath = "Assets/_Project/Art/UI/UI_UnityCubeEmblem.png";
            if (!File.Exists(emblemPath))
            {
                Texture2D uTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                {
                    for (int x = 0; x < 256; x++)
                    {
                        float nx = (x - 128f) / 96f;
                        float ny = (y - 128f) / 96f;
                        float hex = Mathf.Max(Mathf.Abs(nx) * 0.866f + Mathf.Abs(ny) * 0.5f, Mathf.Abs(ny));
                        bool onOuterRing = hex >= 0.72f && hex <= 0.88f;
                        float d1 = Mathf.Abs(nx) < 0.08f && ny < 0f && hex < 0.78f ? 1f : 0f;
                        float d2 = Mathf.Abs(nx * 0.5f - ny * 0.866f) < 0.08f && ny > -0.1f && hex < 0.78f ? 1f : 0f;
                        float d3 = Mathf.Abs(-nx * 0.5f - ny * 0.866f) < 0.08f && ny > -0.1f && hex < 0.78f ? 1f : 0f;
                        bool onInnerY = (d1 > 0f || d2 > 0f || d3 > 0f);
                        bool gap = hex > 0.22f && hex < 0.34f && !onOuterRing;
                        float alpha = (onOuterRing || (onInnerY && !gap)) ? 1f : 0f;
                        uTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }
                uTex.Apply();
                File.WriteAllBytes(emblemPath, uTex.EncodeToPNG());
                Object.DestroyImmediate(uTex);
            }

            AssetDatabase.Refresh();

            Texture2D mistTexAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(mistTexPath);
            unityCubeEmblemSprite = AssetDatabase.LoadAssetAtPath<Sprite>(emblemPath);
            uiDarkBannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bannerPath);

            // Load 2K CC0 PBR Textures + Packed MaskMaps
            Texture2D cliffBase = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernCliff_BaseColor.png");
            Texture2D cliffNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernCliff_NormalGL.png");
            Texture2D cliffMask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernCliff_MaskMap.png");

            Texture2D mossBase = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernMoss_BaseColor.png");
            Texture2D mossNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernMoss_NormalGL.png");
            Texture2D mossMask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturesDir + "/TEX_CavernMoss_MaskMap.png");

            // Create / Update FromSoftware Vertex-Blend + Rim Light Cavern Rock Material
            string rockMatPath = MaterialsDir + "/MAT_URP_AbyssalCavernRock.mat";
            rockMat = AssetDatabase.LoadAssetAtPath<Material>(rockMatPath);
            Shader soulsRockShader = Shader.Find("CaveSouls/URP/SoulsVertexBlendLit")
                                     ?? Shader.Find("Universal Render Pipeline/Lit")
                                     ?? Shader.Find("Standard");
            if (rockMat == null)
            {
                rockMat = new Material(soulsRockShader);
                AssetDatabase.CreateAsset(rockMat, rockMatPath);
            }
            rockMat.shader = soulsRockShader;
            rockMat.SetTexture("_BaseMap", cliffBase);
            rockMat.SetColor("_BaseColor", new Color(0.15f, 0.17f, 0.20f, 1.0f));
            rockMat.SetTexture("_BumpMap", cliffNorm);
            rockMat.SetFloat("_BumpScale", 1.38f);
            rockMat.SetTexture("_MaskMap", cliffMask);

            rockMat.SetTexture("_MossBaseMap", mossBase);
            rockMat.SetColor("_MossColor", new Color(0.09f, 0.12f, 0.11f, 1.0f));
            rockMat.SetTexture("_MossBumpMap", mossNorm);
            rockMat.SetFloat("_MossBumpScale", 1.25f);
            rockMat.SetTexture("_MossMaskMap", mossMask);
            rockMat.SetFloat("_MossBlendStrength", 1.15f);
            rockMat.SetFloat("_MossSharpness", 2.6f);

            rockMat.SetColor("_EdgeWearColor", new Color(0.36f, 0.42f, 0.50f, 1.0f));
            rockMat.SetFloat("_EdgeWearStrength", 0.45f);
            rockMat.SetColor("_RimColor", new Color(0.45f, 0.56f, 0.72f, 1.0f));
            rockMat.SetFloat("_RimPower", 3.6f);
            rockMat.SetFloat("_RimIntensity", 0.48f);
            rockMat.SetFloat("_SmoothnessMultiplier", 0.76f);
            rockMat.SetFloat("_OcclusionStrength", 0.94f);
            EditorUtility.SetDirty(rockMat);

            // Create / Update Volumetric Depth-Faded God-Rays Material
            string godRayMatPath = MaterialsDir + "/MAT_URP_CavernGodRays.mat";
            godRayMat = AssetDatabase.LoadAssetAtPath<Material>(godRayMatPath);
            Shader volumetricRayShader = Shader.Find("CaveSouls/URP/SoulsVolumetricGodRay")
                                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (godRayMat == null)
            {
                godRayMat = new Material(volumetricRayShader);
                AssetDatabase.CreateAsset(godRayMat, godRayMatPath);
            }
            godRayMat.shader = volumetricRayShader;
            godRayMat.SetColor("_BeamColor", new Color(1.25f, 1.38f, 1.58f, 0.32f));
            godRayMat.SetColor("_CoreColor", new Color(1.55f, 1.62f, 1.75f, 1.0f));
            godRayMat.SetFloat("_DepthFadeDistance", 2.2f);
            godRayMat.SetFloat("_FresnelFadePower", 1.45f);
            godRayMat.SetFloat("_NoiseScale", 3.2f);
            godRayMat.SetFloat("_ScrollSpeed", 0.045f);
            godRayMat.SetFloat("_BeamContrast", 1.55f);
            godRayMat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(godRayMat);

            Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");

            // Create URP Soft Mist Particle Material
            string mistMatPath = MaterialsDir + "/MAT_URP_CavernMistParticle.mat";
            mistParticleMat = AssetDatabase.LoadAssetAtPath<Material>(mistMatPath);
            if (mistParticleMat == null)
            {
                mistParticleMat = new Material(particleUnlit);
                AssetDatabase.CreateAsset(mistParticleMat, mistMatPath);
            }
            mistParticleMat.shader = particleUnlit;
            ConfigureAlphaBlendedParticleMaterial(mistParticleMat, mistTexAsset, new Color(0.65f, 0.72f, 0.82f, 0.14f));

            // Create URP HDR Glowing Ember Particle Material (HDR values > 1.1 trigger ACES Bloom!)
            string emberMatPath = MaterialsDir + "/MAT_URP_FloatingEmberParticle.mat";
            emberParticleMat = AssetDatabase.LoadAssetAtPath<Material>(emberMatPath);
            if (emberParticleMat == null)
            {
                emberParticleMat = new Material(particleUnlit);
                AssetDatabase.CreateAsset(emberParticleMat, emberMatPath);
            }
            emberParticleMat.shader = particleUnlit;
            ConfigureAdditiveParticleMaterial(emberParticleMat, mistTexAsset, new Color(2.85f, 1.18f, 0.28f, 0.95f));
        }

        private static void ConfigureAdditiveParticleMaterial(Material mat, Texture2D tex, Color baseCol)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", baseCol);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 2f);   // Additive
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        private static void ConfigureAlphaBlendedParticleMaterial(Material mat, Texture2D tex, Color baseCol)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", baseCol);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        private static void BuildSplashScreenScene(Sprite unityCubeEmblemSprite)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Main Camera (Pure Black Background)
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<UniversalAdditionalCameraData>();

            // 2. EventSystem
            CreateEventSystem();

            // 3. UI Canvas (1920x1080)
            GameObject canvasObj = CreateStandardCanvas("SplashScreenCanvas", cam);

            // Fullscreen Black Background
            GameObject bgObj = CreateUIObject("BlackBackground", canvasObj.transform);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(1920f, 1080f);
            RawImage bgImg = bgObj.AddComponent<RawImage>();
            bgImg.color = Color.black;

            // Step 1: Made with Unity Container
            Texture2D unityCubeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_UnityCubeEmblem.png");
            GameObject unityGroupObj = CreateUIObject("UnityLogoGroup", canvasObj.transform);
            RectTransform unityRect = unityGroupObj.GetComponent<RectTransform>();
            unityRect.sizeDelta = new Vector2(600f, 380f);
            CanvasGroup unityCg = unityGroupObj.AddComponent<CanvasGroup>();

            GameObject cubeIconObj = CreateUIObject("UnityCubeEmblem", unityGroupObj.transform);
            RectTransform cubeRect = cubeIconObj.GetComponent<RectTransform>();
            cubeRect.anchoredPosition = new Vector2(0f, 65f);
            cubeRect.sizeDelta = new Vector2(140f, 140f);
            RawImage cubeImg = cubeIconObj.AddComponent<RawImage>();
            cubeImg.texture = unityCubeTex;
            cubeImg.color = Color.white;

            GameObject madeWithObj = CreateUIObject("MadeWithLabel", unityGroupObj.transform);
            RectTransform madeWithRect = madeWithObj.GetComponent<RectTransform>();
            madeWithRect.anchoredPosition = new Vector2(0f, -38f);
            madeWithRect.sizeDelta = new Vector2(500f, 40f);
            Text madeWithTxt = madeWithObj.AddComponent<Text>();
            madeWithTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            madeWithTxt.text = "M A D E   W I T H";
            madeWithTxt.fontSize = 20;
            madeWithTxt.alignment = TextAnchor.MiddleCenter;
            madeWithTxt.color = new Color(0.82f, 0.82f, 0.85f, 1f);

            GameObject unityTextObj = CreateUIObject("UnityTitleLabel", unityGroupObj.transform);
            RectTransform unityTextRect = unityTextObj.GetComponent<RectTransform>();
            unityTextRect.anchoredPosition = new Vector2(0f, -92f);
            unityTextRect.sizeDelta = new Vector2(500f, 75f);
            Text unityTxt = unityTextObj.AddComponent<Text>();
            unityTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            unityTxt.text = "U N I T Y";
            unityTxt.fontSize = 52;
            unityTxt.fontStyle = FontStyle.Bold;
            unityTxt.alignment = TextAnchor.MiddleCenter;
            unityTxt.color = Color.white;

            // Step 2: Beyond Game Studios Logo Container (using beyondgamestudios_black_logo.png on clean white card)
            Texture2D studioBlackTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_Logo_BeyondGameStudios_Black.png")
                                       ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_Logo_BeyondGameStudios.png");
            GameObject studioGroupObj = CreateUIObject("BeyondGameStudiosLogoGroup", canvasObj.transform);
            RectTransform studioRect = studioGroupObj.GetComponent<RectTransform>();
            studioRect.anchoredPosition = Vector2.zero;
            studioRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup studioCg = studioGroupObj.AddComponent<CanvasGroup>();
            RawImage studioWhiteBg = studioGroupObj.AddComponent<RawImage>();
            studioWhiteBg.color = Color.white;

            GameObject studioLogoArtObj = CreateUIObject("StudioBlackLogoArt", studioGroupObj.transform);
            RectTransform studioLogoArtRect = studioLogoArtObj.GetComponent<RectTransform>();
            studioLogoArtRect.anchoredPosition = Vector2.zero;
            studioLogoArtRect.sizeDelta = new Vector2(680f, 680f);
            RawImage studioImg = studioLogoArtObj.AddComponent<RawImage>();
            studioImg.texture = studioBlackTex;
            studioImg.color = Color.white;

            // Attach & Wire SplashScreenController
            SplashScreenController controller = canvasObj.AddComponent<SplashScreenController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("_unityLogoGroup").objectReferenceValue = unityCg;
            so.FindProperty("_unityLogoRect").objectReferenceValue = unityRect;
            so.FindProperty("_studioLogoGroup").objectReferenceValue = studioCg;
            so.FindProperty("_studioLogoRect").objectReferenceValue = studioLogoArtRect;
            so.FindProperty("_nextSceneName").stringValue = "01_MainMenu";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SplashScenePath);
        }

        private static void BuildMainMenuScene(
            Material rockMat,
            Material godRayMat,
            Material mistParticleMat,
            Material emberParticleMat,
            VolumeProfile eldenRingProfile,
            Sprite uiDarkBannerSprite,
            IntroCinematicDataSO introData)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Configure pitch-black upper sky & atmospheric lower cavern fog per LookDev Directive
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.018f, 0.022f, 0.030f, 1.0f);
            RenderSettings.ambientEquatorColor = new Color(0.012f, 0.015f, 0.020f, 1.0f);
            RenderSettings.ambientGroundColor = new Color(0.005f, 0.006f, 0.009f, 1.0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.010f, 0.014f, 1.0f);
            RenderSettings.fogDensity = 0.038f;

            // 1. Main Camera framed so the multi-plane 3D Cavern Diorama occupies the lower 38% and the upper 62% is pure pitch-black
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 2.35f, -9.4f);
            camObj.transform.rotation = Quaternion.Euler(4.2f, 0f, 0f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 65f;
            cam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;
            camObj.AddComponent<AudioListener>();
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.requiresDepthOption = CameraOverrideOption.On;
            camData.requiresColorOption = CameraOverrideOption.On;

            // 2. Global Volume (ACES Tonemapping, Contrast +15, Saturation -12, Bloom 1.1 / 0.35, Vignette)
            GameObject volumeObj = new GameObject("Global Volume (Elden Ring LookDev)");
            Volume globalVol = volumeObj.AddComponent<Volume>();
            globalVol.isGlobal = true;
            globalVol.priority = 10f;
            globalVol.sharedProfile = eldenRingProfile;

            // 3. Dramatic Grazing Cold Rim Light (LookDev Directive: 15-25 deg grazing angle + Soft Shadows)
            GameObject rimLightObj = new GameObject("Light_CavernGrazingRim");
            rimLightObj.transform.position = new Vector3(5.0f, 6.0f, -3.0f);
            rimLightObj.transform.rotation = Quaternion.Euler(20f, -42f, 0f);
            Light rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.62f, 0.72f, 0.88f, 1.0f);
            rimLight.intensity = 1.15f;
            rimLight.shadows = LightShadows.Soft;
            rimLight.shadowStrength = 0.92f;
            rimLightObj.AddComponent<UniversalAdditionalLightData>();

            // Secondary Chasm Aperture Point Light (illuminates the right-hand fractured crags)
            GameObject chasmLightObj = new GameObject("Light_UpperRightChasmFill");
            chasmLightObj.transform.position = new Vector3(3.2f, 0.6f, -3.2f);
            Light chasmLight = chasmLightObj.AddComponent<Light>();
            chasmLight.type = LightType.Point;
            chasmLight.range = 14.0f;
            chasmLight.color = new Color(0.55f, 0.68f, 0.88f, 1.0f);
            chasmLight.intensity = 1.85f;
            chasmLight.shadows = LightShadows.Soft;
            chasmLightObj.AddComponent<UniversalAdditionalLightData>();

            // Subtle Warm Bonfire / Ember Sub-Fill on Foreground Rocks
            GameObject emberLightObj = new GameObject("Light_EmberWarmSubFill");
            emberLightObj.transform.position = new Vector3(-0.8f, -0.8f, -5.2f);
            Light emberLight = emberLightObj.AddComponent<Light>();
            emberLight.type = LightType.Point;
            emberLight.range = 8.5f;
            emberLight.color = new Color(1.0f, 0.48f, 0.16f, 1.0f);
            emberLight.intensity = 0.75f;
            emberLight.shadows = LightShadows.None;
            emberLightObj.AddComponent<UniversalAdditionalLightData>();

            // 4. Instantiate High-Detail Multi-Plane 3D Cavern Diorama from Blender FBX in the lower third
            GameObject dioramaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/ENV_MainMenuCavernDiorama.fbx");
            if (dioramaPrefab != null)
            {
                GameObject dioramaInst = (GameObject)PrefabUtility.InstantiatePrefab(dioramaPrefab);
                dioramaInst.name = "ENV_MainMenuCavernDiorama";
                dioramaInst.transform.position = new Vector3(0f, -2.05f, 0.8f);
                dioramaInst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                dioramaInst.transform.localScale = new Vector3(1.12f, 0.36f, 1.05f);

                if (dioramaInst.TryGetComponent(out LODGroup dioLodGroup))
                {
                    Object.DestroyImmediate(dioLodGroup);
                }

                Renderer[] renderers = dioramaInst.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = rockMat;
                    renderers[i].shadowCastingMode = ShadowCastingMode.On;
                    renderers[i].receiveShadows = true;
                    if (!renderers[i].gameObject.name.EndsWith("_LOD0"))
                    {
                        renderers[i].gameObject.SetActive(false);
                    }
                }
            }

            // 5. Instantiate Multi-Layered Volumetric God-Rays Mesh from Blender FBX
            GameObject godRaysPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/VFX_CavernGodRaysMesh.fbx");
            if (godRaysPrefab != null)
            {
                GameObject raysInst = (GameObject)PrefabUtility.InstantiatePrefab(godRaysPrefab);
                raysInst.name = "VFX_CavernGodRaysMesh";
                raysInst.transform.position = new Vector3(-2.4f, -2.35f, -2.5f);
                raysInst.transform.rotation = Quaternion.Euler(0f, 0f, -8f);
                raysInst.transform.localScale = new Vector3(0.95f, 0.42f, 1.0f);

                if (raysInst.TryGetComponent(out LODGroup raysLodGroup))
                {
                    Object.DestroyImmediate(raysLodGroup);
                }

                Renderer lod0RayRenderer = null;
                Renderer[] renderers = raysInst.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = godRayMat;
                    renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                    if (renderers[i].gameObject.name.EndsWith("_LOD0"))
                    {
                        lod0RayRenderer = renderers[i];
                    }
                    else
                    {
                        renderers[i].gameObject.SetActive(false);
                    }
                }

                Collider[] cols = raysInst.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < cols.Length; i++)
                {
                    cols[i].enabled = false;
                }

                var pulse = raysInst.AddComponent<CavernGodRayPulseController>();
                if (lod0RayRenderer != null)
                {
                    SerializedObject pulseSo = new SerializedObject(pulse);
                    pulseSo.FindProperty("_godRayRenderer").objectReferenceValue = lod0RayRenderer;
                    pulseSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 6. Real-Time Particle System 1: Drifting Low Cavern Mist
            GameObject fogPsObj = new GameObject("PS_CavernRollingFog");
            fogPsObj.transform.position = new Vector3(-0.4f, -0.55f, -2.4f);
            ParticleSystem fogPs = fogPsObj.AddComponent<ParticleSystem>();
            var fogRenderer = fogPsObj.GetComponent<ParticleSystemRenderer>();
            fogRenderer.sharedMaterial = mistParticleMat;
            fogRenderer.sortingFudge = 2f;

            var fogMain = fogPs.main;
            fogMain.loop = true;
            fogMain.prewarm = true;
            fogMain.startLifetime = new ParticleSystem.MinMaxCurve(7.5f, 13f);
            fogMain.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.20f);
            fogMain.startSize = new ParticleSystem.MinMaxCurve(4.5f, 8.2f);
            fogMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            fogMain.startColor = new Color(0.68f, 0.75f, 0.85f, 0.12f);
            fogMain.maxParticles = 72;

            var fogEmission = fogPs.emission;
            fogEmission.rateOverTime = 9.0f;

            var fogShape = fogPs.shape;
            fogShape.shapeType = ParticleSystemShapeType.Box;
            fogShape.scale = new Vector3(12.0f, 1.5f, 5.0f);

            var fogColOverLife = fogPs.colorOverLifetime;
            fogColOverLife.enabled = true;
            Gradient fogGrad = new Gradient();
            fogGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            fogColOverLife.color = new ParticleSystem.MinMaxGradient(fogGrad);

            // 7. Real-Time Particle System 2: HDR Floating Glowing Orange Embers (triggers ACES Bloom)
            GameObject emberPsObj = new GameObject("PS_FloatingEmbers");
            emberPsObj.transform.position = new Vector3(0f, -0.85f, -3.4f);
            ParticleSystem emberPs = emberPsObj.AddComponent<ParticleSystem>();
            var emberRenderer = emberPsObj.GetComponent<ParticleSystemRenderer>();
            emberRenderer.sharedMaterial = emberParticleMat;

            var emberMain = emberPs.main;
            emberMain.loop = true;
            emberMain.prewarm = true;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.0f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.068f);
            emberMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.48f, 0.14f, 0.92f),
                new Color(1.0f, 0.76f, 0.28f, 1.0f));
            emberMain.maxParticles = 48;

            var emberEmission = emberPs.emission;
            emberEmission.rateOverTime = 5.5f;

            var emberShape = emberPs.shape;
            emberShape.shapeType = ParticleSystemShapeType.Box;
            emberShape.scale = new Vector3(10.5f, 1.4f, 3.8f);

            var emberVel = emberPs.velocityOverLifetime;
            emberVel.enabled = true;
            emberVel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.14f);
            emberVel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.26f);
            emberVel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var emberNoise = emberPs.noise;
            emberNoise.enabled = true;
            emberNoise.strength = 0.20f;
            emberNoise.frequency = 0.45f;

            var emberColLife = emberPs.colorOverLifetime;
            emberColLife.enabled = true;
            emberColLife.color = new ParticleSystem.MinMaxGradient(fogGrad);

            fogPs.Simulate(6.0f, true, true);
            emberPs.Simulate(6.0f, true, true);

            // 8. Looping Main Menu OST ("Beneath_the_Sealed_Sky.ogg")
            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/BGM_BeneathTheSealedSky.ogg");
            GameObject bgmObj = new GameObject("BGM_MainMenuOST");
            AudioSource bgmSource = bgmObj.AddComponent<AudioSource>();
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
            bgmSource.spatialBlend = 0f; // 2D UI/Menu Music
            bgmSource.volume = 0.80f;
            bgmSource.priority = 0;

            // 8b. UI Sound Effects (Hover & Confirm SFX)
            AudioClip hoverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/Audio/UI/SFX_Menu_Hover.wav");
            AudioClip confirmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/Audio/UI/SFX_Menu_Confirm.wav");
            GameObject sfxObj = new GameObject("SFX_MenuAudio");
            AudioSource sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 1.0f;

            // 9. UI Canvas & CAVE SOULS Title + FromSoftware Ornamental Menu
            CreateEventSystem();
            GameObject canvasObj = CreateStandardCanvas("MainMenuCanvas", cam);

            // Title Logo ("CAVE SOULS") positioned to match REF_MainMenu_TargetLook.jpg
            Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/UI_Logo_CaveSouls_Title.png");
            GameObject titleObj = CreateUIObject("TitleLogoGroup", canvasObj.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 58f);
            titleRect.sizeDelta = new Vector2(1420f, 790f);
            CanvasGroup titleCg = titleObj.AddComponent<CanvasGroup>();
            Image titleImg = titleObj.AddComponent<Image>();
            titleImg.sprite = titleSprite;
            titleImg.preserveAspect = true;
            titleImg.color = Color.white;
            titleImg.raycastTarget = false;

            // "PRESS ANY BUTTON" Prompt with FromSoftware Dark Gradient Banner & Gold Rules
            GameObject pressAnyObj = CreateUIObject("PressAnyButtonGroup", canvasObj.transform);
            RectTransform pressAnyRect = pressAnyObj.GetComponent<RectTransform>();
            pressAnyRect.anchoredPosition = new Vector2(0f, -295f);
            pressAnyRect.sizeDelta = new Vector2(640f, 52f);
            CanvasGroup pressAnyCg = pressAnyObj.AddComponent<CanvasGroup>();

            Image pressBannerImg = pressAnyObj.AddComponent<Image>();
            pressBannerImg.sprite = uiDarkBannerSprite;
            pressBannerImg.color = new Color(0.01f, 0.012f, 0.016f, 0.72f);
            pressBannerImg.raycastTarget = false;

            CreateOrnamentalRuleLine("TopGoldRule", pressAnyObj.transform, new Vector2(0f, 22f), new Vector2(420f, 1.5f));
            CreateOrnamentalRuleLine("BottomGoldRule", pressAnyObj.transform, new Vector2(0f, -22f), new Vector2(420f, 1.5f));

            GameObject pressTextObj = CreateUIObject("PressAnyLabel", pressAnyObj.transform);
            StretchFullScreen(pressTextObj.GetComponent<RectTransform>());
            Text pressAnyTxt = pressTextObj.AddComponent<Text>();
            pressAnyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pressAnyTxt.text = "-  P R E S S   A N Y   B U T T O N  -";
            pressAnyTxt.fontSize = 22;
            pressAnyTxt.alignment = TextAnchor.MiddleCenter;
            pressAnyTxt.color = new Color(0.90f, 0.87f, 0.80f, 0.96f);
            pressAnyTxt.raycastTarget = false;

            // Main Menu Buttons Container (CONTINUE, NEW GAME, SETTINGS, EXIT GAME)
            GameObject menuButtonsObj = CreateUIObject("MenuButtonsGroup", canvasObj.transform);
            RectTransform menuButtonsRect = menuButtonsObj.GetComponent<RectTransform>();
            menuButtonsRect.anchoredPosition = new Vector2(0f, -285f);
            menuButtonsRect.sizeDelta = new Vector2(460f, 220f);
            CanvasGroup menuButtonsCg = menuButtonsObj.AddComponent<CanvasGroup>();
            menuButtonsCg.alpha = 0f;
            menuButtonsCg.interactable = false;
            menuButtonsCg.blocksRaycasts = false;

            Image menuBgBanner = menuButtonsObj.AddComponent<Image>();
            menuBgBanner.sprite = uiDarkBannerSprite;
            menuBgBanner.color = new Color(0.01f, 0.012f, 0.018f, 0.65f);
            menuBgBanner.raycastTarget = false;

            string[] btnTitles = { "C O N T I N U E", "N E W   G A M E", "S E T T I N G S", "E X I T   G A M E" };
            Button[] buttons = new Button[4];
            Text[] btnLabels = new Text[4];
            RectTransform[] btnRects = new RectTransform[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject bObj = CreateUIObject($"Btn_{i}", menuButtonsObj.transform);
                RectTransform bRect = bObj.GetComponent<RectTransform>();
                bRect.anchoredPosition = new Vector2(0f, 68f - (i * 46f));
                bRect.sizeDelta = new Vector2(380f, 38f);
                btnRects[i] = bRect;

                Image bImg = bObj.AddComponent<Image>();
                bImg.color = new Color(0f, 0f, 0f, 0.02f); // Clickable hitbox
                bImg.raycastTarget = true;

                Button btn = bObj.AddComponent<Button>();
                btn.targetGraphic = bImg;
                buttons[i] = btn;

                GameObject lblObj = CreateUIObject("Label", bObj.transform);
                StretchFullScreen(lblObj.GetComponent<RectTransform>());
                Text lbl = lblObj.AddComponent<Text>();
                lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.text = btnTitles[i];
                lbl.fontSize = 22;
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = Color.white;
                lbl.raycastTarget = false;
                btnLabels[i] = lbl;
            }

            // Settings Sub-Panel (with Interactive Language Selector EN / ES and Stats Toggle)
            GameObject settingsObj = CreateUIObject("SettingsPanelGroup", canvasObj.transform);
            RectTransform settingsRect = settingsObj.GetComponent<RectTransform>();
            settingsRect.anchoredPosition = new Vector2(0f, -260f);
            settingsRect.sizeDelta = new Vector2(560f, 310f);
            CanvasGroup settingsCg = settingsObj.AddComponent<CanvasGroup>();
            settingsCg.alpha = 0f;
            settingsCg.interactable = false;
            settingsCg.blocksRaycasts = false;
            Image settingsBg = settingsObj.AddComponent<Image>();
            settingsBg.color = new Color(0.02f, 0.022f, 0.03f, 0.94f);

            CreateOrnamentalRuleLine("SettingsTopRule", settingsObj.transform, new Vector2(0f, 150f), new Vector2(510f, 1.5f));
            CreateOrnamentalRuleLine("SettingsBottomRule", settingsObj.transform, new Vector2(0f, -150f), new Vector2(510f, 1.5f));

            GameObject settingsHeaderObj = CreateUIObject("SettingsHeaderLabel", settingsObj.transform);
            RectTransform settingsHeaderRect = settingsHeaderObj.GetComponent<RectTransform>();
            settingsHeaderRect.anchoredPosition = new Vector2(0f, 102f);
            settingsHeaderRect.sizeDelta = new Vector2(500f, 40f);
            Text settingsHeaderTxt = settingsHeaderObj.AddComponent<Text>();
            settingsHeaderTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settingsHeaderTxt.text = "G A M E   &   A U D I O   S E T T I N G S";
            settingsHeaderTxt.fontSize = 19;
            settingsHeaderTxt.alignment = TextAnchor.MiddleCenter;
            settingsHeaderTxt.color = new Color(0.92f, 0.88f, 0.78f, 1f);
            settingsHeaderTxt.raycastTarget = false;

            // Interactive Language Selector Button (< ENGLISH / ESPAÑOL >)
            GameObject langBtnObj = CreateUIObject("Btn_ToggleLanguage", settingsObj.transform);
            RectTransform langBtnRect = langBtnObj.GetComponent<RectTransform>();
            langBtnRect.anchoredPosition = new Vector2(0f, 40f);
            langBtnRect.sizeDelta = new Vector2(480f, 44f);
            Image langBtnImg = langBtnObj.AddComponent<Image>();
            langBtnImg.color = new Color(0.12f, 0.13f, 0.17f, 0.85f);
            Button langBtn = langBtnObj.AddComponent<Button>();
            langBtn.targetGraphic = langBtnImg;

            GameObject langLblObj = CreateUIObject("LanguageLabel", langBtnObj.transform);
            StretchFullScreen(langLblObj.GetComponent<RectTransform>());
            Text langLbl = langLblObj.AddComponent<Text>();
            langLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            langLbl.text = "L A N G U A G E   ( I D I O M A ) :     <   E N G L I S H   >";
            langLbl.fontSize = 18;
            langLbl.alignment = TextAnchor.MiddleCenter;
            langLbl.color = new Color(1.0f, 0.95f, 0.82f, 1f);
            langLbl.raycastTarget = false;

            // Performance Telemetry Overlay Toggle Button (< ON / OFF >)
            GameObject statsBtnObj = CreateUIObject("Btn_ToggleStats", settingsObj.transform);
            RectTransform statsBtnRect = statsBtnObj.GetComponent<RectTransform>();
            statsBtnRect.anchoredPosition = new Vector2(0f, -18f);
            statsBtnRect.sizeDelta = new Vector2(480f, 44f);
            Image statsBtnImg = statsBtnObj.AddComponent<Image>();
            statsBtnImg.color = new Color(0.12f, 0.13f, 0.17f, 0.85f);
            Button statsBtn = statsBtnObj.AddComponent<Button>();
            statsBtn.targetGraphic = statsBtnImg;

            GameObject statsLblObj = CreateUIObject("StatsLabel", statsBtnObj.transform);
            StretchFullScreen(statsLblObj.GetComponent<RectTransform>());
            Text statsLbl = statsLblObj.AddComponent<Text>();
            statsLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statsLbl.text = "P E R F O R M A N C E   S T A T S :     <   O F F   >";
            statsLbl.fontSize = 18;
            statsLbl.alignment = TextAnchor.MiddleCenter;
            statsLbl.color = new Color(1.0f, 0.95f, 0.82f, 1f);
            statsLbl.raycastTarget = false;

            GameObject backBtnObj = CreateUIObject("Btn_CloseSettings", settingsObj.transform);
            RectTransform backBtnRect = backBtnObj.GetComponent<RectTransform>();
            backBtnRect.anchoredPosition = new Vector2(0f, -80f);
            backBtnRect.sizeDelta = new Vector2(260f, 40f);
            Image backBtnImg = backBtnObj.AddComponent<Image>();
            backBtnImg.color = new Color(0.15f, 0.15f, 0.18f, 0.8f);
            Button backBtn = backBtnObj.AddComponent<Button>();
            backBtn.targetGraphic = backBtnImg;

            GameObject backLblObj = CreateUIObject("Label", backBtnObj.transform);
            StretchFullScreen(backLblObj.GetComponent<RectTransform>());
            Text backLbl = backLblObj.AddComponent<Text>();
            backLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            backLbl.text = "B A C K   [ E S C ]";
            backLbl.fontSize = 18;
            backLbl.alignment = TextAnchor.MiddleCenter;
            backLbl.color = Color.white;
            backLbl.raycastTarget = false;

            // Fullscreen Black Fade Overlay (for smooth scene entry & exit)
            GameObject blackFadeObj = CreateUIObject("ScreenBlackFadeGroup", canvasObj.transform);
            StretchFullScreen(blackFadeObj.GetComponent<RectTransform>());
            CanvasGroup blackFadeCg = blackFadeObj.AddComponent<CanvasGroup>();
            blackFadeCg.alpha = 0f; // Set 0 in Editor so Title & Cavern are immediately visible in Scene/Game View; Awake fades it in at runtime!
            blackFadeCg.blocksRaycasts = false;
            RawImage blackFadeImg = blackFadeObj.AddComponent<RawImage>();
            blackFadeImg.color = Color.black;
            blackFadeImg.raycastTarget = false;

            // =========================================================================
            // PRE-MENU SPLASH SCREEN OVERLAY ("MADE WITH UNITY" -> "BEYOND GAME STUDIOS")
            // Ensures pressing Play directly inside 01_MainMenu.unity plays the Splash first!
            // =========================================================================
            GameObject splashRootObj = CreateUIObject("PreMenuSplashRootGroup", canvasObj.transform);
            StretchFullScreen(splashRootObj.GetComponent<RectTransform>());
            CanvasGroup splashRootCg = splashRootObj.AddComponent<CanvasGroup>();
            splashRootCg.alpha = 0f; // 0 in Edit Mode so Scene/Game View previews the 3D Cavern; Awake sets 1 at runtime!
            splashRootCg.interactable = false;
            splashRootCg.blocksRaycasts = false;

            RawImage splashBlackBg = splashRootObj.AddComponent<RawImage>();
            splashBlackBg.color = Color.black;
            splashBlackBg.raycastTarget = false;

            // Phase 1: "MADE WITH UNITY" on Black
            GameObject splashUnityGroupObj = CreateUIObject("SplashUnityLogoGroup", splashRootObj.transform);
            RectTransform splashUnityRect = splashUnityGroupObj.GetComponent<RectTransform>();
            splashUnityRect.sizeDelta = new Vector2(600f, 320f);
            CanvasGroup splashUnityCg = splashUnityGroupObj.AddComponent<CanvasGroup>();
            splashUnityCg.alpha = 0f;

            GameObject splashCubeIconObj = CreateUIObject("UnityCubeEmblem", splashUnityGroupObj.transform);
            RectTransform splashCubeRect = splashCubeIconObj.GetComponent<RectTransform>();
            splashCubeRect.anchoredPosition = new Vector2(0f, 45f);
            splashCubeRect.sizeDelta = new Vector2(128f, 128f);
            RawImage splashCubeImg = splashCubeIconObj.AddComponent<RawImage>();
            splashCubeImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_UnityCubeEmblem.png");
            splashCubeImg.color = Color.white;
            splashCubeImg.raycastTarget = false;

            GameObject splashMadeWithTextObj = CreateUIObject("MadeWithLabel", splashUnityGroupObj.transform);
            RectTransform splashMadeWithRect = splashMadeWithTextObj.GetComponent<RectTransform>();
            splashMadeWithRect.anchoredPosition = new Vector2(0f, -45f);
            splashMadeWithRect.sizeDelta = new Vector2(500f, 40f);
            Text splashMadeWithTxt = splashMadeWithTextObj.AddComponent<Text>();
            splashMadeWithTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            splashMadeWithTxt.text = "M A D E   W I T H";
            splashMadeWithTxt.fontSize = 18;
            splashMadeWithTxt.alignment = TextAnchor.MiddleCenter;
            splashMadeWithTxt.color = new Color(0.75f, 0.75f, 0.78f, 1f);
            splashMadeWithTxt.raycastTarget = false;

            GameObject splashUnityTitleObj = CreateUIObject("UnityTitleLabel", splashUnityGroupObj.transform);
            RectTransform splashUnityTitleRect = splashUnityTitleObj.GetComponent<RectTransform>();
            splashUnityTitleRect.anchoredPosition = new Vector2(0f, -85f);
            splashUnityTitleRect.sizeDelta = new Vector2(500f, 60f);
            Text splashUnityTitleTxt = splashUnityTitleObj.AddComponent<Text>();
            splashUnityTitleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            splashUnityTitleTxt.text = "U N I T Y";
            splashUnityTitleTxt.fontSize = 42;
            splashUnityTitleTxt.fontStyle = FontStyle.Bold;
            splashUnityTitleTxt.alignment = TextAnchor.MiddleCenter;
            splashUnityTitleTxt.color = Color.white;
            splashUnityTitleTxt.raycastTarget = false;

            // Phase 2: Beyond Game Studios Black Logo on Crisp White Backdrop
            GameObject splashStudioGroupObj = CreateUIObject("SplashStudioLogoGroup", splashRootObj.transform);
            RectTransform splashStudioRect = splashStudioGroupObj.GetComponent<RectTransform>();
            splashStudioRect.anchoredPosition = Vector2.zero;
            splashStudioRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup splashStudioCg = splashStudioGroupObj.AddComponent<CanvasGroup>();
            splashStudioCg.alpha = 0f;

            RawImage splashStudioWhiteBg = splashStudioGroupObj.AddComponent<RawImage>();
            splashStudioWhiteBg.color = Color.white;
            splashStudioWhiteBg.raycastTarget = false;

            GameObject splashStudioLogoArtObj = CreateUIObject("StudioBlackLogoArt", splashStudioGroupObj.transform);
            RectTransform splashStudioLogoArtRect = splashStudioLogoArtObj.GetComponent<RectTransform>();
            splashStudioLogoArtRect.anchoredPosition = Vector2.zero;
            splashStudioLogoArtRect.sizeDelta = new Vector2(680f, 680f);
            RawImage splashStudioImg = splashStudioLogoArtObj.AddComponent<RawImage>();
            Texture2D studioBlackTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_Logo_BeyondGameStudios_Black.png");
            if (studioBlackTex == null)
            {
                studioBlackTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/UI_Logo_BeyondGameStudios.png");
            }
            splashStudioImg.texture = studioBlackTex;
            splashStudioImg.color = Color.white;
            splashStudioImg.raycastTarget = false;

            // =========================================================================
            // EMBEDDED INTRO CINEMATIC OVERLAY (1:33 Narrative Sequence inside 01_MainMenu)
            // Guarantees clicking NEW GAME plays the 10-slide Intro Cinematic immediately!
            // =========================================================================
            GameObject introAudioRoot = new GameObject("EmbeddedIntroCinematicAudio");
            GameObject introBgmObj = new GameObject("AudioSource_BGM_WeightOfTheSeal");
            introBgmObj.transform.SetParent(introAudioRoot.transform, false);
            AudioSource introBgmSource = introBgmObj.AddComponent<AudioSource>();
            introBgmSource.playOnAwake = false;
            introBgmSource.loop = false;
            introBgmSource.spatialBlend = 0f;
            introBgmSource.volume = 0.78f;

            GameObject introVoObj = new GameObject("AudioSource_VO_LocalizedNarrator");
            introVoObj.transform.SetParent(introAudioRoot.transform, false);
            AudioSource introVoSource = introVoObj.AddComponent<AudioSource>();
            introVoSource.playOnAwake = false;
            introVoSource.loop = false;
            introVoSource.spatialBlend = 0f;
            introVoSource.volume = 1.0f;

            GameObject embeddedIntroRoot = CreateUIObject("EmbeddedIntroCinematicRoot", canvasObj.transform);
            StretchFullScreen(embeddedIntroRoot.GetComponent<RectTransform>());
            CanvasGroup embeddedIntroRootCg = embeddedIntroRoot.AddComponent<CanvasGroup>();
            embeddedIntroRootCg.alpha = 0f;
            embeddedIntroRootCg.interactable = false;
            embeddedIntroRootCg.blocksRaycasts = false;

            GameObject embBgObj = CreateUIObject("BlackBackdrop", embeddedIntroRoot.transform);
            RectTransform embBgRect = embBgObj.GetComponent<RectTransform>();
            embBgRect.anchoredPosition = Vector2.zero;
            embBgRect.sizeDelta = new Vector2(1920f, 1080f);
            RawImage embBgImg = embBgObj.AddComponent<RawImage>();
            embBgImg.color = Color.black;

            GameObject embSlideObj = CreateUIObject("CinematicSlideImage", embeddedIntroRoot.transform);
            RectTransform embSlideRect = embSlideObj.GetComponent<RectTransform>();
            embSlideRect.anchoredPosition = Vector2.zero;
            embSlideRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup embSlideCg = embSlideObj.AddComponent<CanvasGroup>();
            embSlideCg.alpha = 1f;
            RawImage embSlideImg = embSlideObj.AddComponent<RawImage>();
            embSlideImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/IntroCinematic/UI_IntroSlide_01.png");
            embSlideImg.color = Color.white;

            GameObject embSubGroupObj = CreateUIObject("SubtitleBarGroup", embeddedIntroRoot.transform);
            RectTransform embSubGroupRect = embSubGroupObj.GetComponent<RectTransform>();
            embSubGroupRect.anchoredPosition = new Vector2(0f, -415f);
            embSubGroupRect.sizeDelta = new Vector2(1460f, 110f);
            CanvasGroup embSubCg = embSubGroupObj.AddComponent<CanvasGroup>();
            embSubCg.alpha = 1f;

            Image embSubBannerImg = embSubGroupObj.AddComponent<Image>();
            embSubBannerImg.sprite = uiDarkBannerSprite;
            embSubBannerImg.color = new Color(0.01f, 0.012f, 0.016f, 0.82f);
            embSubBannerImg.raycastTarget = false;

            CreateOrnamentalRuleLine("SubtitleTopGoldRule", embSubGroupObj.transform, new Vector2(0f, 52f), new Vector2(980f, 1.5f));
            CreateOrnamentalRuleLine("SubtitleBottomGoldRule", embSubGroupObj.transform, new Vector2(0f, -52f), new Vector2(980f, 1.5f));

            GameObject embSubTextObj = CreateUIObject("SubtitleText", embSubGroupObj.transform);
            RectTransform embSubTextRect = embSubTextObj.GetComponent<RectTransform>();
            embSubTextRect.anchoredPosition = Vector2.zero;
            embSubTextRect.sizeDelta = new Vector2(1320f, 94f);
            Text embSubTxt = embSubTextObj.AddComponent<Text>();
            embSubTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            embSubTxt.text = "In the beginning... there was no weight. Only... the flow.";
            embSubTxt.fontSize = 26;
            embSubTxt.fontStyle = FontStyle.Italic;
            embSubTxt.alignment = TextAnchor.MiddleCenter;
            embSubTxt.color = new Color(0.93f, 0.90f, 0.83f, 1.0f);
            embSubTxt.raycastTarget = false;

            GameObject embSkipObj = CreateUIObject("SkipPromptGroup", embeddedIntroRoot.transform);
            RectTransform embSkipRect = embSkipObj.GetComponent<RectTransform>();
            embSkipRect.anchoredPosition = new Vector2(680f, -495f);
            embSkipRect.sizeDelta = new Vector2(440f, 36f);
            CanvasGroup embSkipCg = embSkipObj.AddComponent<CanvasGroup>();
            embSkipCg.alpha = 0f;
            Text embSkipTxt = embSkipObj.AddComponent<Text>();
            embSkipTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            embSkipTxt.text = "HOLD [SPACE / ESC] TO SKIP";
            embSkipTxt.fontSize = 16;
            embSkipTxt.alignment = TextAnchor.MiddleRight;
            embSkipTxt.color = new Color(0.75f, 0.72f, 0.66f, 0.85f);
            embSkipTxt.raycastTarget = false;

            GameObject embFinalBlackObj = CreateUIObject("FinalBlackFadeGroup", embeddedIntroRoot.transform);
            RectTransform embFinalBlackRect = embFinalBlackObj.GetComponent<RectTransform>();
            embFinalBlackRect.anchoredPosition = Vector2.zero;
            embFinalBlackRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup embFinalBlackCg = embFinalBlackObj.AddComponent<CanvasGroup>();
            embFinalBlackCg.alpha = 0f;
            embFinalBlackCg.blocksRaycasts = false;
            RawImage embFinalBlackImg = embFinalBlackObj.AddComponent<RawImage>();
            embFinalBlackImg.color = Color.black;
            embFinalBlackImg.raycastTarget = false;

            IntroCinematicController embIntroCtrl = embeddedIntroRoot.AddComponent<IntroCinematicController>();
            SerializedObject embIntroSo = new SerializedObject(embIntroCtrl);
            embIntroSo.FindProperty("_playOnStart").boolValue = false;
            embIntroSo.FindProperty("_rootCanvasGroup").objectReferenceValue = embeddedIntroRootCg;
            WireIntroCinematicAssetReferences(embIntroSo);
            embIntroSo.FindProperty("_nextGameplaySceneName").stringValue = "03_PrimordialMudBed";
            embIntroSo.FindProperty("_slideRawImage").objectReferenceValue = embSlideImg;
            embIntroSo.FindProperty("_slideRect").objectReferenceValue = embSlideRect;
            embIntroSo.FindProperty("_slideCanvasGroup").objectReferenceValue = embSlideCg;
            embIntroSo.FindProperty("_subtitleGroup").objectReferenceValue = embSubCg;
            embIntroSo.FindProperty("_subtitleText").objectReferenceValue = embSubTxt;
            embIntroSo.FindProperty("_skipPromptGroup").objectReferenceValue = embSkipCg;
            embIntroSo.FindProperty("_skipPromptText").objectReferenceValue = embSkipTxt;
            embIntroSo.FindProperty("_finalBlackFadeGroup").objectReferenceValue = embFinalBlackCg;
            embIntroSo.FindProperty("_bgmAudioSource").objectReferenceValue = introBgmSource;
            embIntroSo.FindProperty("_vocalsAudioSource").objectReferenceValue = introVoSource;
            embIntroSo.ApplyModifiedPropertiesWithoutUndo();

            // Attach & Wire MainMenuController
            MainMenuController menuCtrl = canvasObj.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(menuCtrl);
            so.FindProperty("_splashRootGroup").objectReferenceValue = splashRootCg;
            so.FindProperty("_splashUnityGroup").objectReferenceValue = splashUnityCg;
            so.FindProperty("_splashUnityRect").objectReferenceValue = splashUnityRect;
            so.FindProperty("_splashStudioGroup").objectReferenceValue = splashStudioCg;
            so.FindProperty("_splashStudioRect").objectReferenceValue = splashStudioRect;
            so.FindProperty("_screenBlackFadeGroup").objectReferenceValue = blackFadeCg;
            so.FindProperty("_titleLogoGroup").objectReferenceValue = titleCg;
            so.FindProperty("_pressAnyButtonGroup").objectReferenceValue = pressAnyCg;
            so.FindProperty("_menuButtonsGroup").objectReferenceValue = menuButtonsCg;
            so.FindProperty("_settingsPanelGroup").objectReferenceValue = settingsCg;
            so.FindProperty("_mainCanvas").objectReferenceValue = canvasObj.GetComponent<Canvas>();
            so.FindProperty("_uiCamera").objectReferenceValue = cam;
            so.FindProperty("_continueButton").objectReferenceValue = buttons[0];
            so.FindProperty("_newGameButton").objectReferenceValue = buttons[1];
            so.FindProperty("_settingsButton").objectReferenceValue = buttons[2];
            so.FindProperty("_exitButton").objectReferenceValue = buttons[3];
            so.FindProperty("_pressAnyButtonLabel").objectReferenceValue = pressAnyTxt;
            so.FindProperty("_languageToggleButton").objectReferenceValue = langBtn;
            so.FindProperty("_languageButtonRect").objectReferenceValue = langBtnRect;
            so.FindProperty("_languageButtonLabel").objectReferenceValue = langLbl;
            so.FindProperty("_statsToggleButton").objectReferenceValue = statsBtn;
            so.FindProperty("_statsButtonRect").objectReferenceValue = statsBtnRect;
            so.FindProperty("_statsButtonLabel").objectReferenceValue = statsLbl;
            so.FindProperty("_closeSettingsButton").objectReferenceValue = backBtn;
            so.FindProperty("_closeSettingsRect").objectReferenceValue = backBtnRect;
            so.FindProperty("_settingsHeaderLabel").objectReferenceValue = settingsHeaderTxt;
            so.FindProperty("_backButtonLabel").objectReferenceValue = backLbl;
            so.FindProperty("_bgmAudioSource").objectReferenceValue = bgmSource;
            so.FindProperty("_targetBgmVolume").floatValue = 0.80f;
            so.FindProperty("_sfxAudioSource").objectReferenceValue = sfxSource;
            so.FindProperty("_hoverClip").objectReferenceValue = hoverClip;
            so.FindProperty("_confirmClip").objectReferenceValue = confirmClip;
            so.FindProperty("_embeddedIntroCinematic").objectReferenceValue = embIntroCtrl;

            SerializedProperty labelsProp = so.FindProperty("_buttonLabels");
            labelsProp.arraySize = 4;
            SerializedProperty rectsProp = so.FindProperty("_buttonRects");
            rectsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = btnLabels[i];
                rectsProp.GetArrayElementAtIndex(i).objectReferenceValue = btnRects[i];
            }
            so.FindProperty("_introCinematicSceneName").stringValue = "02_IntroCinematic";
            so.FindProperty("_gameplaySceneName").stringValue = "03_PrimordialMudBed";
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(backBtn.onClick, menuCtrl.OnCloseSettingsClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(langBtn.onClick, menuCtrl.OnToggleLanguageClicked);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(statsBtn.onClick, menuCtrl.OnTogglePerformanceStatsClicked);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static IntroCinematicDataSO CreateOrUpdateIntroCinematicDataSO()
        {
            string soPath = SettingsDir + "/SO_IntroCinematicData.asset";
            IntroCinematicDataSO data = AssetDatabase.LoadAssetAtPath<IntroCinematicDataSO>(soPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<IntroCinematicDataSO>();
                AssetDatabase.CreateAsset(data, soPath);
            }

            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/BGM_WeightOfTheSeal.mp3");
            AudioClip voEnClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/VO_IntroVocals_English.mp3");
            AudioClip voEsClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/VO_IntroVocals_Spanish.mp3");

            // Acoustic-matched 10-slide windows across the 93.3s (1:33) timeline
            (float start, float end)[] slideWindows =
            {
                (0.0f,  10.5f), // Clip 1 (01.png)
                (10.5f, 20.5f), // Clip 2 (02.png)
                (20.5f, 30.8f), // Clip 3 (03.png)
                (30.8f, 38.6f), // Clip 4 (04.png)
                (38.6f, 46.5f), // Clip 5 (05.png)
                (46.5f, 56.8f), // Clip 6 (06.png)
                (56.8f, 66.6f), // Clip 7 (07.png)
                (66.6f, 74.5f), // Clip 8 (08.png)
                (74.5f, 83.6f), // Clip 9 (09.png)
                (83.6f, 93.3f)  // Clip 10 (10.png)
            };

            // Acoustic-synchronized subtitle cues (EN & ES) matching the real voice-over recordings
            (float start, float end, string en, string es)[] subtitleScript =
            {
                // Clip 1 (0.0s - 10.5s)
                (0.7f,  3.3f,  "In the beginning...", "En el principio..."),
                (4.0f,  6.3f,  "there was no weight.", "no había peso."),
                (6.8f,  10.1f, "Only... the flow.", "Solo... el flujo."),

                // Clip 2 (10.5s - 20.5s)
                (11.2f, 14.3f, "But light breeds shadows...", "Pero la luz engendra sombras..."),
                (14.8f, 16.5f, "and fear...", "y el miedo..."),
                (16.8f, 20.1f, "...is the poison of rulers.", "...es el veneno de los gobernantes."),

                // Clip 3 (20.5s - 30.8s)
                (21.1f, 27.1f, "To protect his own domain... he decreed the end of the cycle...", "Para proteger su propio dominio... decretó el fin del ciclo..."),
                (27.5f, 30.3f, "...and sealed our sky.", "...y selló nuestro cielo."),

                // Clip 4 (30.8s - 38.6s)
                (31.5f, 35.3f, "He left behind his most loyal Sentinel...", "Dejó atrás a su Centinela más leal..."),
                (35.6f, 38.3f, "...to guard the heights.", "...para custodiar las alturas."),

                // Clip 5 (38.6s - 46.5s)
                (39.0f, 42.1f, "Trapped beneath the seal...", "Atrapados bajo el sello..."),
                (42.6f, 46.1f, "...we were stripped of birth.", "...fuimos despojados del nacimiento."),

                // Clip 6 (46.5s - 56.8s)
                (47.2f, 49.6f, "We grew dense.", "Nos volvimos densos."),
                (50.2f, 56.4f, "solidifying our essence into prisons of crystal... mud... and stone.", "solidificando nuestra esencia en prisiones de cristal... lodo... y piedra."),

                // Clip 7 (56.8s - 66.6s)
                (57.3f, 59.3f, "And yet...", "Y sin embargo..."),
                (59.7f, 62.7f, "in the lowest stratum...", "en el estrato más profundo..."),
                (63.0f, 66.2f, "...where despair reigns...", "...donde reina la desesperación..."),

                // Clip 8 (66.6s - 74.5s)
                (67.1f, 71.5f, "...an ancient spark refuses to fade.", "...una chispa ancestral se niega a apagarse."),
                (72.0f, 74.1f, "A Lumen.", "Un Lumen."),

                // Clip 9 (74.5s - 83.6s)
                (75.3f, 77.0f, "Rise.", "Álzate."),
                (77.3f, 79.9f, "Defy the Sentinel.", "Desafía al Centinela."),
                (80.3f, 83.1f, "Reclaim the heights.", "Reclama las alturas."),

                // Clip 10 (83.6s - 93.3s, voice ends ~92.4s followed by fading echo)
                (84.4f, 86.4f, "And discover...", "Y descubre..."),
                (86.7f, 89.2f, "what was stolen from us...", "...lo que nos fue arrebatado..."),
                (89.5f, 92.6f, "...beyond the sky of stone.", "...más allá del cielo de piedra.")
            };

            SerializedObject so = new SerializedObject(data);
            so.FindProperty("_totalDuration").floatValue = 93.3f;
            so.FindProperty("_slideFadeInDuration").floatValue = 2.0f;
            so.FindProperty("_slideFadeOutDuration").floatValue = 2.0f;
            so.FindProperty("_bgmClip").objectReferenceValue = bgmClip;
            so.FindProperty("_vocalsEnglishClip").objectReferenceValue = voEnClip;
            so.FindProperty("_vocalsSpanishClip").objectReferenceValue = voEsClip;

            SerializedProperty slidesProp = so.FindProperty("_slides");
            slidesProp.arraySize = 10;
            for (int i = 0; i < 10; i++)
            {
                string texPath = $"Assets/_Project/Art/UI/IntroCinematic/UI_IntroSlide_{(i + 1):D2}.png";
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                SerializedProperty elem = slidesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("SlideTexture").objectReferenceValue = tex;
                elem.FindPropertyRelative("StartTime").floatValue = slideWindows[i].start;
                elem.FindPropertyRelative("EndTime").floatValue = slideWindows[i].end;
            }

            SerializedProperty cuesProp = so.FindProperty("_subtitleCues");
            cuesProp.arraySize = subtitleScript.Length;
            for (int i = 0; i < subtitleScript.Length; i++)
            {
                SerializedProperty elem = cuesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("StartTime").floatValue = subtitleScript[i].start;
                elem.FindPropertyRelative("EndTime").floatValue = subtitleScript[i].end;
                elem.FindPropertyRelative("TextEnglish").stringValue = subtitleScript[i].en;
                elem.FindPropertyRelative("TextSpanish").stringValue = subtitleScript[i].es;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        private static void WireIntroCinematicAssetReferences(SerializedObject introSo)
        {
            IntroCinematicDataSO freshData = AssetDatabase.LoadAssetAtPath<IntroCinematicDataSO>(SettingsDir + "/SO_IntroCinematicData.asset");
            introSo.FindProperty("_cinematicData").objectReferenceValue = freshData;

            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/BGM_WeightOfTheSeal.mp3");
            AudioClip voEnClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/VO_IntroVocals_English.mp3");
            AudioClip voEsClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/IntroCinematic/VO_IntroVocals_Spanish.mp3");

            introSo.FindProperty("_directBgmClip").objectReferenceValue = bgmClip;
            introSo.FindProperty("_directVocalsEnglishClip").objectReferenceValue = voEnClip;
            introSo.FindProperty("_directVocalsSpanishClip").objectReferenceValue = voEsClip;

            SerializedProperty directSlidesProp = introSo.FindProperty("_directSlideTextures");
            directSlidesProp.arraySize = 10;
            for (int i = 0; i < 10; i++)
            {
                string texPath = $"Assets/_Project/Art/UI/IntroCinematic/UI_IntroSlide_{(i + 1):D2}.png";
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                directSlidesProp.GetArrayElementAtIndex(i).objectReferenceValue = tex;
            }
        }

        private static void BuildIntroCinematicScene(IntroCinematicDataSO introData, Sprite uiDarkBannerSprite)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Main Camera (Pure Black Background)
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<UniversalAdditionalCameraData>();

            CreateEventSystem();

            // 2. Dual AudioSources: BGM ("Weight_of_the_Seal.mp3") + Localized Vocals ("intro_vocals_english/spanish.mp3")
            GameObject audioRoot = new GameObject("IntroCinematicAudio");
            GameObject bgmObj = new GameObject("AudioSource_BGM_WeightOfTheSeal");
            bgmObj.transform.SetParent(audioRoot.transform, false);
            AudioSource bgmSource = bgmObj.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = false;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = 0.78f;

            GameObject voObj = new GameObject("AudioSource_VO_LocalizedNarrator");
            voObj.transform.SetParent(audioRoot.transform, false);
            AudioSource voSource = voObj.AddComponent<AudioSource>();
            voSource.playOnAwake = false;
            voSource.loop = false;
            voSource.spatialBlend = 0f;
            voSource.volume = 1.0f;

            // 3. UI Canvas (1920x1080)
            GameObject canvasObj = CreateStandardCanvas("IntroCinematicCanvas", cam);

            // Pure Black Backdrop (RawImage renders immediately in ScreenSpaceCamera without requiring a Sprite sub-asset)
            GameObject bgObj = CreateUIObject("BlackBackdrop", canvasObj.transform);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(1920f, 1080f);
            RawImage bgImg = bgObj.AddComponent<RawImage>();
            bgImg.color = Color.black;

            // Artwork Slide Container (with 2s Fade-In / 2s Fade-Out CanvasGroup & Ken Burns RectTransform)
            GameObject slideObj = CreateUIObject("CinematicSlideImage", canvasObj.transform);
            RectTransform slideRect = slideObj.GetComponent<RectTransform>();
            slideRect.anchoredPosition = Vector2.zero;
            slideRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup slideCg = slideObj.AddComponent<CanvasGroup>();
            slideCg.alpha = 1f;
            RawImage slideImg = slideObj.AddComponent<RawImage>();
            slideImg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/UI/IntroCinematic/UI_IntroSlide_01.png");
            slideImg.color = Color.white;

            // Subtitle Banner & Text (Bottom Cinema Bar)
            GameObject subGroupObj = CreateUIObject("SubtitleBarGroup", canvasObj.transform);
            RectTransform subGroupRect = subGroupObj.GetComponent<RectTransform>();
            subGroupRect.anchoredPosition = new Vector2(0f, -415f);
            subGroupRect.sizeDelta = new Vector2(1460f, 110f);
            CanvasGroup subCg = subGroupObj.AddComponent<CanvasGroup>();
            subCg.alpha = 1f;

            Image subBannerImg = subGroupObj.AddComponent<Image>();
            subBannerImg.sprite = uiDarkBannerSprite;
            subBannerImg.color = new Color(0.01f, 0.012f, 0.016f, 0.82f);
            subBannerImg.raycastTarget = false;

            CreateOrnamentalRuleLine("SubtitleTopGoldRule", subGroupObj.transform, new Vector2(0f, 52f), new Vector2(980f, 1.5f));
            CreateOrnamentalRuleLine("SubtitleBottomGoldRule", subGroupObj.transform, new Vector2(0f, -52f), new Vector2(980f, 1.5f));

            GameObject subTextObj = CreateUIObject("SubtitleText", subGroupObj.transform);
            RectTransform subTextRect = subTextObj.GetComponent<RectTransform>();
            subTextRect.anchoredPosition = Vector2.zero;
            subTextRect.sizeDelta = new Vector2(1320f, 94f);
            Text subTxt = subTextObj.AddComponent<Text>();
            subTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subTxt.text = "In the beginning... there was no weight. Only... the flow.";
            subTxt.fontSize = 26;
            subTxt.fontStyle = FontStyle.Italic;
            subTxt.alignment = TextAnchor.MiddleCenter;
            subTxt.color = new Color(0.93f, 0.90f, 0.83f, 1.0f);

            // Skip Prompt (Bottom Right Corner)
            GameObject skipObj = CreateUIObject("SkipPromptGroup", canvasObj.transform);
            RectTransform skipRect = skipObj.GetComponent<RectTransform>();
            skipRect.anchoredPosition = new Vector2(680f, -495f);
            skipRect.sizeDelta = new Vector2(440f, 36f);
            CanvasGroup skipCg = skipObj.AddComponent<CanvasGroup>();
            skipCg.alpha = 0f;
            Text skipTxt = skipObj.AddComponent<Text>();
            skipTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skipTxt.text = "HOLD [SPACE / ESC] TO SKIP";
            skipTxt.fontSize = 16;
            skipTxt.alignment = TextAnchor.MiddleRight;
            skipTxt.color = new Color(0.75f, 0.72f, 0.66f, 0.85f);

            // Final Black Fade Overlay
            GameObject finalBlackObj = CreateUIObject("FinalBlackFadeGroup", canvasObj.transform);
            RectTransform finalBlackRect = finalBlackObj.GetComponent<RectTransform>();
            finalBlackRect.anchoredPosition = Vector2.zero;
            finalBlackRect.sizeDelta = new Vector2(1920f, 1080f);
            CanvasGroup finalBlackCg = finalBlackObj.AddComponent<CanvasGroup>();
            finalBlackCg.alpha = 0f;
            finalBlackCg.blocksRaycasts = false;
            RawImage finalBlackImg = finalBlackObj.AddComponent<RawImage>();
            finalBlackImg.color = Color.black;
            finalBlackImg.raycastTarget = false;

            Canvas.ForceUpdateCanvases();

            // Attach & Wire IntroCinematicController
            IntroCinematicController ctrl = canvasObj.AddComponent<IntroCinematicController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("_playOnStart").boolValue = true;
            WireIntroCinematicAssetReferences(so);
            so.FindProperty("_nextGameplaySceneName").stringValue = "03_PrimordialMudBed";
            so.FindProperty("_slideRawImage").objectReferenceValue = slideImg;
            so.FindProperty("_slideRect").objectReferenceValue = slideRect;
            so.FindProperty("_slideCanvasGroup").objectReferenceValue = slideCg;
            so.FindProperty("_subtitleGroup").objectReferenceValue = subCg;
            so.FindProperty("_subtitleText").objectReferenceValue = subTxt;
            so.FindProperty("_skipPromptGroup").objectReferenceValue = skipCg;
            so.FindProperty("_skipPromptText").objectReferenceValue = skipTxt;
            so.FindProperty("_finalBlackFadeGroup").objectReferenceValue = finalBlackCg;
            so.FindProperty("_bgmAudioSource").objectReferenceValue = bgmSource;
            so.FindProperty("_vocalsAudioSource").objectReferenceValue = voSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, IntroCinematicScenePath);
        }

        private static void CreateOrnamentalRuleLine(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            GameObject lineObj = CreateUIObject(name, parent);
            RectTransform rt = lineObj.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            Image img = lineObj.AddComponent<Image>();
            img.color = new Color(0.55f, 0.47f, 0.33f, 0.38f); // Tarnished FromSoftware gold (#8C7853)
            img.raycastTarget = false;
        }

        private static void RegisterScenesInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SplashScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(IntroCinematicScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
            };
        }

        private static GameObject CreateStandardCanvas(string name, Camera worldCamera = null)
        {
            GameObject canvasObj = new GameObject(name);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            if (worldCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = worldCamera;
                canvas.planeDistance = 1.0f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            return canvasObj;
        }

        private static void CreateEventSystem()
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
