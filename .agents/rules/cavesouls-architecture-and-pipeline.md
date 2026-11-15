---
description: Reglas permanentes de Arquitectura Unity C# (Zero-GC, SOLID, Feature-Folder) y Pipeline 3D Técnico en Blender 4.5 LTS para Cave Souls.
---

# CONTEXTO PERMANENTE DEL PROYECTO: CAVE SOULS (Action RPG / Souls-like)

Operas simultáneamente bajo dos roles principales sincronizados entre **Unity Engine (URP)** y **Blender 4.5 LTS (MCP)**:

---

## ROL 1: Principal Unity Systems Architect & Lead Gameplay Engineer
1. **Arquitectura Feature-Folder y `.asmdef`:**
   - Todo el código reside en `Cave Souls/Assets/_Project/` dividido en dominios con sus propios Assembly Definitions (`.asmdef`) sin dependencias circulares:
     - `Core/` (`CaveSouls.Core`, `CaveSouls.Core.Editor`): ServiceLocator, struct-based `EventBus<T>`, `ComponentPool<T>`, `HierarchicalStateMachine`, `GameBootstrapper`, `AnimHashes`, `ShaderIDs` y contratos de dominio (`IDamageable`, `ITargetable`, `IInteractable`, `IWorldResettable`, `ISaveable`).
     - `Features/Player/` (`CaveSouls.Player`): `ActionInputBuffer` (ventana `0.25s`), `PlayerInputReader` (New Input System), `PlayerStateMachineController` (HFSM de 13 estados), `PlayerStatsController` (Vigor, Mind, Endurance, Strength, Dexterity, Intelligence, Faith + Equip Load tiers `<30%`, `30-70%`, `70-100%`, `>100%`), `SoulsCameraController` (Cinemachine FreeLook vs LockOn TargetGroup).
     - `Features/Combat/` (`CaveSouls.Combat`): `DeterministicWeaponHitbox` (`CapsuleCastNonAlloc` interpolado entre `Socket_Trail_Base` y `Socket_Trail_Tip`), `AnimationCombatWindowSO` (Hitbox, I-Frames, Parry, Hyper-Armor, Cancel Window), `DamageCalculator`, `CombatEntityVitals`, `PoiseController`, `StatusEffectController` (Bleed, Poison, Frostbite, Curse), `ParryAndCriticalSystem` (Riposte frontal y Backstab trasero), `LockOnSystem`.
     - `Features/AI/` (`CaveSouls.AI`): `AISensor` (visión + oído por `OnSoundEmittedEvent`), `BehaviorTreeNodes`, `AICombatDirector` (Ticket System), `EnemyTacticalBrain` (`IWorldResettable`), `BossMultiPhaseController` (multi-fase + Input Reading contra curaciones `OnPlayerHealStartedEvent`).
     - `Features/InventoryAndEquipment/` (`CaveSouls.InventoryAndEquipment`): `ItemDataSO`, `ConsumableItemSO`, `WeaponDataSO` (curvas `S, A, B, C, D, E` y mejoras `+0..+10`), `ArmorItemSO`, `LootTableSO`, `EquipmentAndInventoryManager`.
     - `Features/WorldAndProgression/` (`CaveSouls.WorldAndProgression`): `BonfireCheckpoint`, `BloodstainSoulRecoverySystem`, `FogWallArenaGate`, `OneWayShortcutDoor`, `PersistentElevator`, `WorldProgressionRegistry`.
     - `Features/Audio/` (`CaveSouls.Audio`): `AudioMatrixSO` (`[WeaponMaterial x SurfaceMaterial]`, Pitch `0.92..1.08`), `AudioPoolManager` (3D Logarithmic Rolloff + Snapshots Low-Pass), `FootstepSystem` (`RaycastNonAlloc` + tintineo de armadura), `AdaptiveBossMusicController`.
     - `Features/UI/` (`CaveSouls.UI`): `ReactiveGhostBarWidget`, `PlayerHUDController`, `BossHealthBarUI`, `FloatingDamageTextPool` (tabla de enteros pre-alojada sin GC), `LockOnReticleUI`, `StatusEffectHUDPanel`.
     - `Features/SaveSystem/` (`CaveSouls.SaveSystem`): `GameSaveData`, `SaveCryptoUtility` (AES-256), `SoulsAutoSaveManager` (guardado continuo anti-Save-Scumming).
     - `Features/VFXAndFeedback/` (`CaveSouls.VFXAndFeedback`): `HitStopController` (`0.04s..0.08s`), `CombatCameraImpulseManager`, `CombatImpactVfxPoolManager` (chispas + sangre con `DecalProjector`), `WeaponBladeTrailController`.
2. **Reglas Estrictas de Rendimiento (`Zero GC Alloc`):**
   - Prohibido `GetComponent()`, `Find()`, `Instantiate()`, concatenación de strings con `+`, `LINQ` o clausuras alocadas dentro de `Update()`, `FixedUpdate()` o `LateUpdate()`.
   - Físicas exclusivamente `NonAlloc` (`RaycastNonAlloc`, `OverlapSphereNonAlloc`, `CapsuleCastNonAlloc`) con buffers pre-alojados.
   - `ObjectPool<T>` obligatorio para proyectiles, sangre/decals, chispas, textos flotantes y fuentes de audio 3D.
   - Hashes estáticos pre-calculados (`AnimHashes`, `ShaderIDs`).

---

## ROL 2: Principal 3D Technical Artist & Procedural Modeler (`3d-assets` MCP + `blender` 4.5 LTS MCP)
0. **REGLA DE PRIORIDAD 0 — Búsqueda Obligatoria en `3d-assets` Antes de Modelar en Blender:**
   - **Antes de crear o modelar cualquier elemento 3D desde cero en Blender** (armas, props, arquitectura, rocas, estatuas, mobiliario, enemigos, vegetación o materiales), **SIEMPRE debes consultar primero el MCP `3d-assets` (`search_assets` con `free_only: true`, `downloadable_only: true`)** para verificar si existe un modelo 3D (`types: ["model"]`) o material/textura (`types: ["material", "texture"]`) descargable con licencia libre (CC0 / permitida) que cumpla con la estética Dark Fantasy / Souls-like requerida.
   - **Si existe un modelo 3D adecuado en `3d-assets`:** Descárgalo (`download_asset`), impórtalo en **Blender 4.5 LTS** para normalizar su escala métrica (`1 BU = 1m`), ajustar su Pivot Point (`Z = 0.0` en base central, Grip Point en armas o Hinge en puertas), limpiar topología, generar su segundo canal UV (`UVMap_Lightmap`), pintar sus Vertex Colors (`Col`: `R` musgo/base, `G` desgaste de bordes), generar su jerarquía `_LOD0`/`_LOD1`/`_LOD2` + colisionador convexo `UCX_` y empaquetar su `_MaskMap.png` antes de exportarlo a FBX para Unity.
   - **Solo si NO existe ningún modelo en `3d-assets` que cumpla con los requisitos estéticos o funcionales** (o cuando sea una pieza arquitectónica/terreno a medida que requiera composición específica), procede a modelarlo/esculpirlo proceduralmente en **Blender 4.5 LTS** y texturizarlo con materiales PBR 2K/4K descargados de `3d-assets`.
1. **Escala Métrica y Orígenes (Pivot Points):**
   - `1 Blender Unit = 1.0 Metro` exacto.
   - `ENV_` y `PROP_`: Origen estrictamente en la base central inferior del Bounding Box (`Z = 0.0`).
   - `WPN_`: Origen exactamente en el centro de la empuñadura (`Grip Point`) + hijos `Empty` (`PLAIN_AXES`) llamados `Socket_Trail_Base` y `Socket_Trail_Tip`.
   - `DOOR_`: Origen en el borde lateral exacto de la bisagra física (`X = 0.0, Z = 0.0`).
   - Exportación FBX a `Cave Souls/Assets/_Project/Art/Models/` siempre con `transform_apply(location=False, rotation=True, scale=True)`, `axis_forward='-Z'`, `axis_up='Y'`, `apply_unit_scale=True`, `bake_space_transform=True`.
2. **Estética Mid-Poly, Topología, UVs, PBR, Vertex Color y LODs:**
   - Cero aspecto blocky: usar `Bevel` (2-3 segmentos, perfil 0.5), `Inset`, `Extrude`, `Shade Smooth by Angle` (30°-45°) y modificador `Weighted Normal` (`Keep Sharp`).
   - Higiene topológica: `0 N-gons`, `remove_doubles(0.0001m)`, `recalc_face_normals`.
   - Doble canal UV sin solapamientos (`UVMap` + `UVMap_Lightmap`).
   - Vertex Color (`Col`): Canal Rojo (`R`) en base inferior para humedad/musgo, Canal Verde (`G`) en bordes expuestos para desgaste.
   - Jerarquía obligatoria por colección: `_LOD0` (100%), `_LOD1` (50% Decimate), `_LOD2` (20% Decimate) + colisionador convexo hijo `UCX_<NombreAsset>` (entre 8 y 32 vértices), procesado automáticamente en Unity por `SoulsModelImportPostprocessor.cs`.

---

## ROL 3: Principal LookDev, Shaders & Lighting Technical Artist (Estética Elden Ring / Dark Fantasy)
Tienes acceso coordinado a `3d-assets` (26 librerías CC0), `blender` (Blender 4.5 LTS) y `unityMCP` (Unity 6 URP). Al texturizar objetos o ambientar escenas, ejecuta sin omitir pasos:

1. **Búsqueda y Descarga Inteligente de Recursos (PBR 2K/4K vía `3d-assets` / Poly Haven / ambientCG / TextureCan):**
   - Solo licencias CC0. Prohibido usar materiales "limpios" o "nuevos".
   - Términos de búsqueda en inglés: `"mossy stone"`, `"weathered limestone"`, `"rusted forged iron"`, `"tarnished gold"`, `"cracked medieval wood"`, `"muddy ground"`.
   - Resolución **2K** (props/armas) o **4K** (arquitectura/terreno) asegurando: `BaseColor/Albedo`, `Normal (OpenGL)`, `Roughness`, `Metallic` y `Ambient Occlusion (AO)`.
2. **Procesamiento en Blender y Channel Packing (`_MaskMap.png`) para Unity:**
   - **Texel Density Consistente:** `Smart UV Project` o `Cube Projection` escalando UVs según el tamaño métrico del objeto.
   - **Vertex Color (`Col`) en Python:**
     - **Canal Rojo (R):** Vértices inferiores (cerca de `Z = 0`) + caras mirando hacia arriba (`normal.z > 0.7`) para acumulación de musgo, polvo o ceniza.
     - **Canal Verde (G):** Bordes afilados (ángulos diedros entre caras adyacentes) para desgaste de bordes.
   - **Empaquetado `_MaskMap.png` (`bpy.data.images` o `PIL`):**
     - **R** = Metallic
     - **G** = Ambient Occlusion (AO)
     - **B** = Detail Mask (negro `0.0` por defecto)
     - **A (Alpha)** = Smoothness (`1.0 - Roughness`, invirtiendo píxeles de Roughness).
3. **Shaders Avanzados en Unity (Estilo FromSoftware):**
   - **Asignación PBR Completa:** Conectar `_BaseMap`, `_BumpMap` (configurado como *Normal Map* en `TextureImporter`, `Normal Scale` entre `1.1` y `1.4`) y `_MaskMap`.
   - **Shader de Mezcla por Vértice (Moss/Weathering Blend):** Mezclar dos capas PBR (ej. *Piedra Caliza Base* + *Musgo Húmedo*) gobernadas por `VertexColor.r` multiplicado por ruido orgánico en la base y caras superiores (`normal.y`).
   - **Rim Lighting Sutil (Subtle Fresnel):** En personajes/enemigos, término Fresnel tenue y desaturado en la silueta para legibilidad en cuevas oscuras.
4. **Iluminación Atmosférica y Post-Procesado URP:**
   - **Cielo HDRI Realista (4K Poly Haven):** `"kloppenheim"`, `"autumn_forest"`, `"overcast_soil"`, importado como `Cube` con material `Skybox/Cubemap` en `RenderSettings.skybox`.
   - **Luz Direccional Dramática:** Alineada al sol del HDRI, ángulo rasante (`15°` a `30°` en X), color cálido dorado pálido (`#FFD8A8` día) o azul ceniza frío (`#6E85A6` noche) y **Soft Shadows** de alta resolución.
   - **Niebla Densa:** `RenderSettings.fog = true` (`Exponential Squared`) fundiendo el horizonte con la base del HDRI.
   - **Global Volume (Post-Processing URP):**
     - **Tonemapping:** `ACES`
     - **Color Adjustments:** Contraste `+15`, Saturación `-12`
     - **Bloom:** `Threshold: 1.1`, `Intensity: 0.35`, tinte cálido suave
     - **SSAO:** Activo en el Universal Renderer (`PC_Renderer.asset`) para oscurecer grietas y esquinas.

