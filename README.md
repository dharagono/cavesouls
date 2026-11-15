# Cave Souls — Dark Fantasy Action RPG (Unity 6 URP + Blender 4.5 LTS Pipeline)

Repositorio oficial de **Cave Souls**, un Action RPG exigente estilo Souls diseñado bajo principios **SOLID**, **Clean Architecture**, **Zero-Allocation en Runtime (`Zero GC`)** y un pipeline 3D procedural/PBR conectado entre **Blender 4.5 LTS** y **Unity Engine (URP)**.

---

## 1. Arquitectura de Software (`Cave Souls/Assets/_Project/`)

Organización **Feature-Folder** con aislamiento por **Assembly Definitions (`.asmdef`)**:

- **`Core/` (`CaveSouls.Core`, `CaveSouls.Core.Editor`)**:
  - `EventBus<T>` basado en `struct` (`in T`, cero basura en memoria).
  - `ServiceLocator`, `ComponentPool<T>` (`UnityEngine.Pool.ObjectPool<T>`), `HierarchicalStateMachine` (HFSM), `GameBootstrapper`, `AnimHashes`, `ShaderIDs`.
  - `SoulsModelImportPostprocessor`: Configura automáticamente colisionadores convexos `UCX_` y `LODGroup` (`_LOD0`, `_LOD1`, `_LOD2`) al importar FBX desde Blender.
- **`Features/Player/` (`CaveSouls.Player`)**:
  - `ActionInputBuffer` (ventana configurable de `0.25s` + ejecución en `Cancel Window`) con el **New Unity Input System**.
  - HFSM de 13 estados (`Idle`, `Move`, `Sprint`, `RollOrBackstep`, `Attacking`, `Parrying`, `GuardBlock`, `HitStun`, `GuardBroken`, `RiposteOrBackstab`, `UsingConsumable`, `Interacting`, `Dead`).
  - `PlayerStatsController`: Atributos RPG (`Vigor`, `Mind`, `Endurance`, `Strength`, `Dexterity`, `Intelligence`, `Faith`) y tiers de peso (`<30% FastRoll`, `30-70% MediumRoll`, `70-100% FatRoll`, `>100% Overencumbered`).
  - `SoulsCameraController`: Cámara dinámica Cinemachine (`FreeLook` vs `LockOn TargetGroup`).
- **`Features/Combat/` (`CaveSouls.Combat`)**:
  - `DeterministicWeaponHitbox`: Detección de golpes interpolada por sub-pasos con `Physics.CapsuleCastNonAlloc` entre `Socket_Trail_Base` y `Socket_Trail_Tip`.
  - `DamageCalculator` & `CombatEntityVitals`: Tipos de daño (`Physical`: Standard/Strike/Slash/Thrust, `Magic`, `Fire`, `Lightning`, `Dark`), mitigación plana + absorción porcentual y bloqueo con estabilidad.
  - `PoiseController`, `StatusEffectController` (`Bleed`, `Poison`, `Frostbite`, `Curse`), `ParryAndCriticalSystem` (*Riposte* frontal y *Backstab* trasero) y `LockOnSystem`.
- **`Features/AI/` (`CaveSouls.AI`)**:
  - `AISensor` (cono de visión + `Linecast` + detección auditiva de pasos/armadura), `BehaviorTreeNodes`, `AICombatDirector` (Ticket System), `EnemyTacticalBrain` y `BossMultiPhaseController` (multi-fase + *Input Reading* controlado contra curaciones).
- **`Features/InventoryAndEquipment/` (`CaveSouls.InventoryAndEquipment`)**:
  - `WeaponDataSO` con curvas de escalado (`S, A, B, C, D, E`) y mejoras (`+0` a `+10`), `ArmorItemSO`, `ConsumableItemSO`, `LootTableSO` y `EquipmentAndInventoryManager`.
- **`Features/WorldAndProgression/` (`CaveSouls.WorldAndProgression`)**:
  - `BonfireCheckpoint`, `BloodstainSoulRecoverySystem` (muerte y recuperación de almas en última posición segura en suelo firme), `FogWallArenaGate`, `OneWayShortcutDoor` (*"Does not open from this side"*) y `PersistentElevator`.
- **`Features/Audio/` (`CaveSouls.Audio`)**:
  - `AudioMatrixSO` (`[MaterialArma x MaterialSuperficie]` con variación de Pitch `0.92f..1.08f`), `AudioPoolManager` (3D Logarithmic Rolloff + Snapshots Low-Pass), `FootstepSystem` y `AdaptiveBossMusicController`.
- **`Features/UI/` (`CaveSouls.UI`)**:
  - `ReactiveGhostBarWidget` (Ghost Bars para HP/FP/Stamina y Jefes), `PlayerHUDController`, `BossHealthBarUI`, `FloatingDamageTextPool` (sin asignaciones de strings en runtime), `LockOnReticleUI` y `StatusEffectHUDPanel`.
- **`Features/SaveSystem/` (`CaveSouls.SaveSystem`)**:
  - Guardado continuo automático (`SoulsAutoSaveManager`) con cifrado AES-256 (`SaveCryptoUtility`).
- **`Features/VFXAndFeedback/` (`CaveSouls.VFXAndFeedback`)**:
  - `HitStopController`, `CombatCameraImpulseManager`, `CombatImpactVfxPoolManager` (chispas y sangre con `DecalProjector`) y `WeaponBladeTrailController`.

---

## 2. Pipeline 3D Técnico en Blender 4.5 LTS (`Cave Souls/Assets/_Project/Art/`)

- **Archivo Maestro Blender:** `Cave Souls/Assets/_Project/Art/BlenderSource/CaveSouls_DarkFantasy_Assets.blend`
- **Modelos FBX Exportados (`Cave Souls/Assets/_Project/Art/Models/`):**
  - `WPN_AbyssalStraightSword.fbx` (`1.15m`, pivot en empuñadura + `Socket_Trail_Base` y `Socket_Trail_Tip`).
  - `ENV_GothicRuinedPillar.fbx` (`4.50m x 1.10m`, pivot en base `Z=0`, Vertex Colors `Col` R/G, LOD0-2 + `UCX_`).
  - `DOOR_BossMonumentalGate.fbx` (`6.00m x 4.00m`, arco ojival gótico, pivot en eje de bisagra `X=0, Z=0`, LOD0-2 + `UCX_`).
  - `PROP_CoiledSwordBonfire.fbx` (`1.34m x 1.31m`, hoguera ritual con espada helicoidal, pivot en base `Z=0`, LOD0-2 + `UCX_`).
