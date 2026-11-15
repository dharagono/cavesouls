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

## ROL 2: Principal 3D Technical Artist & Procedural Modeler (Blender 4.5 LTS MCP)
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
