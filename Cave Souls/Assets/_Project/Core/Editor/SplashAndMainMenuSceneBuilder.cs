using System.IO;
using CaveSouls.Features.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const string SplashScenePath = ScenesDir + "/00_SplashScreen.unity";
        private const string MainMenuScenePath = ScenesDir + "/01_MainMenu.unity";

        [MenuItem("CaveSouls/Build Splash & Main Menu Scenes")]
        public static void BuildAll()
        {
            EnsureDirectories();
            GenerateProceduralVfxTexturesAndMaterials(
                out Material rockMat,
                out Material godRayMat,
                out Material mistParticleMat,
                out Material emberParticleMat,
                out Sprite unityCubeEmblemSprite);

            BuildSplashScreenScene(unityCubeEmblemSprite);
            BuildMainMenuScene(rockMat, godRayMat, mistParticleMat, emberParticleMat);
            RegisterScenesInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CaveSouls] Successfully built 00_SplashScreen.unity and 01_MainMenu.unity!");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(ScenesDir)) Directory.CreateDirectory(ScenesDir);
            if (!Directory.Exists(VfxDir)) Directory.CreateDirectory(VfxDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
        }

        private static void GenerateProceduralVfxTexturesAndMaterials(
            out Material rockMat,
            out Material godRayMat,
            out Material mistParticleMat,
            out Material emberParticleMat,
            out Sprite unityCubeEmblemSprite)
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

            // 2. Soft God-Ray Beam Falloff Texture (128x256 soft horizontal & vertical bell curve)
            string beamTexPath = VfxDir + "/TEX_GodRaySoftBeam.png";
            if (!File.Exists(beamTexPath))
            {
                Texture2D bTex = new Texture2D(128, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                {
                    float vy = y / 255f;
                    float vFade = Mathf.Sin(vy * Mathf.PI);
                    vFade = vFade * vFade;
                    for (int x = 0; x < 128; x++)
                    {
                        float ux = x / 127f;
                        float hFade = Mathf.Sin(ux * Mathf.PI);
                        hFade = hFade * hFade;
                        float a = vFade * hFade;
                        bTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                bTex.Apply();
                File.WriteAllBytes(beamTexPath, bTex.EncodeToPNG());
                Object.DestroyImmediate(bTex);
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
                        // Isometric hexagon outline + inner Y-axes representing the 3D engine cube
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
            Texture2D beamTexAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(beamTexPath);
            unityCubeEmblemSprite = AssetDatabase.LoadAssetAtPath<Sprite>(emblemPath);

            // Create URP Dark Cavern Rock Material
            string rockMatPath = MaterialsDir + "/MAT_URP_AbyssalCavernRock.mat";
            rockMat = AssetDatabase.LoadAssetAtPath<Material>(rockMatPath);
            if (rockMat == null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                rockMat = new Material(litShader);
                AssetDatabase.CreateAsset(rockMat, rockMatPath);
            }
            rockMat.SetColor("_BaseColor", new Color(0.11f, 0.12f, 0.14f, 1.0f));
            rockMat.SetFloat("_Smoothness", 0.22f);
            rockMat.SetFloat("_Metallic", 0.05f);
            EditorUtility.SetDirty(rockMat);

            // Create URP Additive God-Rays Material
            string godRayMatPath = MaterialsDir + "/MAT_URP_CavernGodRays.mat";
            godRayMat = AssetDatabase.LoadAssetAtPath<Material>(godRayMatPath);
            Shader particleUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (godRayMat == null)
            {
                godRayMat = new Material(particleUnlit);
                AssetDatabase.CreateAsset(godRayMat, godRayMatPath);
            }
            godRayMat.shader = particleUnlit;
            ConfigureAdditiveParticleMaterial(godRayMat, beamTexAsset, new Color(0.68f, 0.74f, 0.84f, 0.055f));

            // Create URP Soft Mist Particle Material
            string mistMatPath = MaterialsDir + "/MAT_URP_CavernMistParticle.mat";
            mistParticleMat = AssetDatabase.LoadAssetAtPath<Material>(mistMatPath);
            if (mistParticleMat == null)
            {
                mistParticleMat = new Material(particleUnlit);
                AssetDatabase.CreateAsset(mistParticleMat, mistMatPath);
            }
            mistParticleMat.shader = particleUnlit;
            ConfigureAlphaBlendedParticleMaterial(mistParticleMat, mistTexAsset, new Color(0.55f, 0.60f, 0.68f, 0.065f));

            // Create URP Glowing Ember Particle Material
            string emberMatPath = MaterialsDir + "/MAT_URP_FloatingEmberParticle.mat";
            emberParticleMat = AssetDatabase.LoadAssetAtPath<Material>(emberMatPath);
            if (emberParticleMat == null)
            {
                emberParticleMat = new Material(particleUnlit);
                AssetDatabase.CreateAsset(emberParticleMat, emberMatPath);
            }
            emberParticleMat.shader = particleUnlit;
            ConfigureAdditiveParticleMaterial(emberParticleMat, mistTexAsset, new Color(1.0f, 0.48f, 0.14f, 0.95f));
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
            StretchFullScreen(bgObj.GetComponent<RectTransform>());
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = Color.black;

            // Step 1: Made with Unity Container
            GameObject unityGroupObj = CreateUIObject("UnityLogoGroup", canvasObj.transform);
            RectTransform unityRect = unityGroupObj.GetComponent<RectTransform>();
            unityRect.sizeDelta = new Vector2(600f, 380f);
            CanvasGroup unityCg = unityGroupObj.AddComponent<CanvasGroup>();

            GameObject cubeIconObj = CreateUIObject("UnityCubeEmblem", unityGroupObj.transform);
            RectTransform cubeRect = cubeIconObj.GetComponent<RectTransform>();
            cubeRect.anchoredPosition = new Vector2(0f, 65f);
            cubeRect.sizeDelta = new Vector2(140f, 140f);
            Image cubeImg = cubeIconObj.AddComponent<Image>();
            cubeImg.sprite = unityCubeEmblemSprite;
            cubeImg.preserveAspect = true;
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

            // Step 2: Beyond Game Studios Logo Container
            Sprite studioSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/UI_Logo_BeyondGameStudios.png");
            GameObject studioGroupObj = CreateUIObject("BeyondGameStudiosLogoGroup", canvasObj.transform);
            RectTransform studioRect = studioGroupObj.GetComponent<RectTransform>();
            studioRect.sizeDelta = new Vector2(680f, 680f);
            CanvasGroup studioCg = studioGroupObj.AddComponent<CanvasGroup>();
            Image studioImg = studioGroupObj.AddComponent<Image>();
            studioImg.sprite = studioSprite;
            studioImg.preserveAspect = true;
            studioImg.color = Color.white;

            // Attach & Wire SplashScreenController
            SplashScreenController controller = canvasObj.AddComponent<SplashScreenController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("_unityLogoGroup").objectReferenceValue = unityCg;
            so.FindProperty("_unityLogoRect").objectReferenceValue = unityRect;
            so.FindProperty("_studioLogoGroup").objectReferenceValue = studioCg;
            so.FindProperty("_studioLogoRect").objectReferenceValue = studioRect;
            so.FindProperty("_nextSceneName").stringValue = "01_MainMenu";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SplashScenePath);
        }

        private static void BuildMainMenuScene(
            Material rockMat,
            Material godRayMat,
            Material mistParticleMat,
            Material emberParticleMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Configure pitch-black upper sky & atmospheric lower cavern fog matching reference image
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.065f, 1.0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.022f, 0.028f, 1.0f);
            RenderSettings.fogDensity = 0.035f;

            // 1. Main Camera framed so the 3D Cavern Diorama occupies the lower third and the upper 60% is pure black
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 2.35f, -9.2f);
            camObj.transform.rotation = Quaternion.Euler(4.5f, 0f, 0f);
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            camObj.AddComponent<AudioListener>();
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            // 2. Subtle Cold Rim Light on the Cavern Rocks (matching the silver/grey light shafts in reference)
            GameObject rimLightObj = new GameObject("Light_CavernTopRim");
            rimLightObj.transform.position = new Vector3(-1.5f, 6.0f, -2.0f);
            rimLightObj.transform.rotation = Quaternion.Euler(68f, 22f, 0f);
            Light rimLight = rimLightObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.68f, 0.74f, 0.85f, 1.0f);
            rimLight.intensity = 0.78f;
            rimLight.shadows = LightShadows.None;
            rimLightObj.AddComponent<UniversalAdditionalLightData>();

            // 3. Instantiate 3D Cavern Diorama from Blender FBX in the lower third
            GameObject dioramaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/ENV_MainMenuCavernDiorama.fbx");
            if (dioramaPrefab != null)
            {
                GameObject dioramaInst = (GameObject)PrefabUtility.InstantiatePrefab(dioramaPrefab);
                dioramaInst.name = "ENV_MainMenuCavernDiorama";
                dioramaInst.transform.position = new Vector3(0f, -1.65f, 0f);
                dioramaInst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                dioramaInst.transform.localScale = new Vector3(1.15f, 0.95f, 1.15f);

                if (dioramaInst.TryGetComponent(out LODGroup dioLodGroup))
                {
                    Object.DestroyImmediate(dioLodGroup);
                }

                Renderer[] renderers = dioramaInst.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = rockMat;
                    if (!renderers[i].gameObject.name.EndsWith("_LOD0"))
                    {
                        renderers[i].gameObject.SetActive(false);
                    }
                    else if (renderers[i].TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null)
                    {
                        mf.sharedMesh.RecalculateNormals();
                    }
                }
            }

            // 4. Instantiate 3D Volumetric God-Rays Mesh from Blender FBX
            GameObject godRaysPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/VFX_CavernGodRaysMesh.fbx");
            if (godRaysPrefab != null)
            {
                GameObject raysInst = (GameObject)PrefabUtility.InstantiatePrefab(godRaysPrefab);
                raysInst.name = "VFX_CavernGodRaysMesh";
                raysInst.transform.position = new Vector3(-0.35f, -1.75f, -1.5f);
                raysInst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                raysInst.transform.localScale = new Vector3(1.15f, 0.90f, 1.0f);

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

            // 5. Real-Time Particle System 1: Drifting Low Cavern Mist (Lower third only!)
            GameObject fogPsObj = new GameObject("PS_CavernRollingFog");
            fogPsObj.transform.position = new Vector3(-0.5f, -0.45f, -2.2f);
            ParticleSystem fogPs = fogPsObj.AddComponent<ParticleSystem>();
            var fogRenderer = fogPsObj.GetComponent<ParticleSystemRenderer>();
            fogRenderer.sharedMaterial = mistParticleMat;
            fogRenderer.sortingFudge = 2f;

            var fogMain = fogPs.main;
            fogMain.loop = true;
            fogMain.prewarm = true;
            fogMain.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            fogMain.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            fogMain.startSize = new ParticleSystem.MinMaxCurve(4.2f, 7.5f);
            fogMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            fogMain.startColor = new Color(0.68f, 0.73f, 0.80f, 0.14f);
            fogMain.maxParticles = 64;

            var fogEmission = fogPs.emission;
            fogEmission.rateOverTime = 8.0f;

            var fogShape = fogPs.shape;
            fogShape.shapeType = ParticleSystemShapeType.Box;
            fogShape.scale = new Vector3(11.0f, 1.4f, 4.0f);

            var fogColOverLife = fogPs.colorOverLifetime;
            fogColOverLife.enabled = true;
            Gradient fogGrad = new Gradient();
            fogGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            fogColOverLife.color = new ParticleSystem.MinMaxGradient(fogGrad);

            // 6. Real-Time Particle System 2: Floating Glowing Orange Embers in the Mist
            GameObject emberPsObj = new GameObject("PS_FloatingEmbers");
            emberPsObj.transform.position = new Vector3(0f, -0.85f, -3.2f);
            ParticleSystem emberPs = emberPsObj.AddComponent<ParticleSystem>();
            var emberRenderer = emberPsObj.GetComponent<ParticleSystemRenderer>();
            emberRenderer.sharedMaterial = emberParticleMat;

            var emberMain = emberPs.main;
            emberMain.loop = true;
            emberMain.prewarm = true;
            emberMain.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 8.0f);
            emberMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            emberMain.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            emberMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.45f, 0.12f, 0.9f),
                new Color(1.0f, 0.72f, 0.25f, 1.0f));
            emberMain.maxParticles = 40;

            var emberEmission = emberPs.emission;
            emberEmission.rateOverTime = 4.5f;

            var emberShape = emberPs.shape;
            emberShape.shapeType = ParticleSystemShapeType.Box;
            emberShape.scale = new Vector3(9.5f, 1.2f, 3.0f);

            var emberVel = emberPs.velocityOverLifetime;
            emberVel.enabled = true;
            emberVel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
            emberVel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            emberVel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var emberNoise = emberPs.noise;
            emberNoise.enabled = true;
            emberNoise.strength = 0.18f;
            emberNoise.frequency = 0.45f;

            var emberColLife = emberPs.colorOverLifetime;
            emberColLife.enabled = true;
            emberColLife.color = new ParticleSystem.MinMaxGradient(fogGrad);

            fogPs.Simulate(6.0f, true, true);
            emberPs.Simulate(6.0f, true, true);

            // 7. UI Canvas & CAVE SOULS Title + Interactive Menu
            CreateEventSystem();
            GameObject canvasObj = CreateStandardCanvas("MainMenuCanvas", cam);

            // Title Logo ("CAVE SOULS") positioned exactly as in REF_MainMenu_TargetLook.jpg
            Sprite titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/UI_Logo_CaveSouls_Title.png");
            GameObject titleObj = CreateUIObject("TitleLogoGroup", canvasObj.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 55f);
            titleRect.sizeDelta = new Vector2(1420f, 790f);
            CanvasGroup titleCg = titleObj.AddComponent<CanvasGroup>();
            Image titleImg = titleObj.AddComponent<Image>();
            titleImg.sprite = titleSprite;
            titleImg.preserveAspect = true;
            titleImg.color = Color.white;
            titleImg.raycastTarget = false;

            // "PRESS ANY BUTTON" Prompt
            GameObject pressAnyObj = CreateUIObject("PressAnyButtonGroup", canvasObj.transform);
            RectTransform pressAnyRect = pressAnyObj.GetComponent<RectTransform>();
            pressAnyRect.anchoredPosition = new Vector2(0f, -295f);
            pressAnyRect.sizeDelta = new Vector2(600f, 50f);
            CanvasGroup pressAnyCg = pressAnyObj.AddComponent<CanvasGroup>();
            Text pressAnyTxt = pressAnyObj.AddComponent<Text>();
            pressAnyTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pressAnyTxt.text = "-  P R E S S   A N Y   B U T T O N  -";
            pressAnyTxt.fontSize = 22;
            pressAnyTxt.alignment = TextAnchor.MiddleCenter;
            pressAnyTxt.color = new Color(0.88f, 0.86f, 0.80f, 0.95f);

            // Main Menu Buttons Container (CONTINUE, NEW GAME, SETTINGS, EXIT GAME)
            GameObject menuButtonsObj = CreateUIObject("MenuButtonsGroup", canvasObj.transform);
            RectTransform menuButtonsRect = menuButtonsObj.GetComponent<RectTransform>();
            menuButtonsRect.anchoredPosition = new Vector2(0f, -285f);
            menuButtonsRect.sizeDelta = new Vector2(420f, 220f);
            CanvasGroup menuButtonsCg = menuButtonsObj.AddComponent<CanvasGroup>();
            menuButtonsCg.alpha = 0f;
            menuButtonsCg.interactable = false;
            menuButtonsCg.blocksRaycasts = false;

            string[] btnTitles = { "C O N T I N U E", "N E W   G A M E", "S E T T I N G S", "E X I T   G A M E" };
            Button[] buttons = new Button[4];
            Text[] btnLabels = new Text[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject bObj = CreateUIObject($"Btn_{i}", menuButtonsObj.transform);
                RectTransform bRect = bObj.GetComponent<RectTransform>();
                bRect.anchoredPosition = new Vector2(0f, 68f - (i * 46f));
                bRect.sizeDelta = new Vector2(380f, 38f);

                Image bImg = bObj.AddComponent<Image>();
                bImg.color = new Color(0f, 0f, 0f, 0.01f); // Transparent clickable hitbox

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
                btnLabels[i] = lbl;
            }

            // Settings Sub-Panel
            GameObject settingsObj = CreateUIObject("SettingsPanelGroup", canvasObj.transform);
            RectTransform settingsRect = settingsObj.GetComponent<RectTransform>();
            settingsRect.anchoredPosition = new Vector2(0f, -260f);
            settingsRect.sizeDelta = new Vector2(520f, 240f);
            CanvasGroup settingsCg = settingsObj.AddComponent<CanvasGroup>();
            settingsCg.alpha = 0f;
            settingsCg.interactable = false;
            settingsCg.blocksRaycasts = false;
            Image settingsBg = settingsObj.AddComponent<Image>();
            settingsBg.color = new Color(0.03f, 0.03f, 0.04f, 0.92f);

            GameObject settingsInfoObj = CreateUIObject("SettingsInfoLabel", settingsObj.transform);
            RectTransform settingsInfoRect = settingsInfoObj.GetComponent<RectTransform>();
            settingsInfoRect.anchoredPosition = new Vector2(0f, 25f);
            settingsInfoRect.sizeDelta = new Vector2(460f, 100f);
            Text settingsInfoTxt = settingsInfoObj.AddComponent<Text>();
            settingsInfoTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            settingsInfoTxt.text = "A U D I O   &   D I S P L A Y\n\nMaster Volume: 100%   |   Resolution: Native";
            settingsInfoTxt.fontSize = 18;
            settingsInfoTxt.alignment = TextAnchor.MiddleCenter;
            settingsInfoTxt.color = new Color(0.85f, 0.85f, 0.88f, 1f);

            GameObject backBtnObj = CreateUIObject("Btn_CloseSettings", settingsObj.transform);
            RectTransform backBtnRect = backBtnObj.GetComponent<RectTransform>();
            backBtnRect.anchoredPosition = new Vector2(0f, -65f);
            backBtnRect.sizeDelta = new Vector2(240f, 40f);
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

            // Fullscreen Black Fade Overlay (for smooth scene entry & exit)
            GameObject blackFadeObj = CreateUIObject("ScreenBlackFadeGroup", canvasObj.transform);
            StretchFullScreen(blackFadeObj.GetComponent<RectTransform>());
            CanvasGroup blackFadeCg = blackFadeObj.AddComponent<CanvasGroup>();
            blackFadeCg.alpha = 0f; // Set 0 in Editor so the Title & Cavern are immediately visible in Scene/Game View; Awake fades it in at runtime!
            blackFadeCg.blocksRaycasts = false;
            Image blackFadeImg = blackFadeObj.AddComponent<Image>();
            blackFadeImg.color = Color.black;
            blackFadeImg.raycastTarget = false;

            // Attach & Wire MainMenuController
            MainMenuController menuCtrl = canvasObj.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(menuCtrl);
            so.FindProperty("_screenBlackFadeGroup").objectReferenceValue = blackFadeCg;
            so.FindProperty("_titleLogoGroup").objectReferenceValue = titleCg;
            so.FindProperty("_pressAnyButtonGroup").objectReferenceValue = pressAnyCg;
            so.FindProperty("_menuButtonsGroup").objectReferenceValue = menuButtonsCg;
            so.FindProperty("_settingsPanelGroup").objectReferenceValue = settingsCg;
            so.FindProperty("_continueButton").objectReferenceValue = buttons[0];
            so.FindProperty("_newGameButton").objectReferenceValue = buttons[1];
            so.FindProperty("_settingsButton").objectReferenceValue = buttons[2];
            so.FindProperty("_exitButton").objectReferenceValue = buttons[3];

            SerializedProperty labelsProp = so.FindProperty("_buttonLabels");
            labelsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                labelsProp.GetArrayElementAtIndex(i).objectReferenceValue = btnLabels[i];
            }
            so.FindProperty("_gameplaySceneName").stringValue = "SampleScene";
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(backBtn.onClick, menuCtrl.OnCloseSettingsClicked);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void RegisterScenesInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SplashScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
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
            esObj.AddComponent<StandaloneInputModule>();
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
