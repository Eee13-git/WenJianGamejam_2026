# CODELY.md — WenJianGamejam_2026

## Project Overview

A 2D top-down roguelike dungeon crawler with a microbiological theme. Players explore procedurally generated dungeons, fight enemies, steal skills from defeated foes, assimilate enemies into followers, collect items, and face bosses to advance to the next level.

- **Unity Version**: 2022.3.62f3c1
- **Render Pipeline**: URP 14.0.12 (2D Renderer, Sprite-Lit materials)
- **Input**: Legacy Input Manager (`Input.GetAxisRaw`, `Input.GetMouseButton`, `Input.GetKeyDown`)
- **Target Platform**: PC (Windows)
- **Build Scene**: `Assets/Scenes/SampleScene.unity` (build index 0)

## Key Scenes & Entry Point

| Scene | Path | Purpose |
|-------|------|---------|
| SampleScene | `Assets/Scenes/SampleScene.unity` | Main game scene (in Build Settings) |
| LightTestScene | `Assets/Scenes/LightTestScene.unity` | 2D lighting test scene (not in Build Settings) |

**Game Flow**: StartMenu → SampleScene (procedural dungeon) → Room-to-room gameplay → Boss/Exit → `nextSceneName` (scene change for next level)

## Architecture

### Design Patterns

- **Singleton**: `PlayerManager.Instance`, `MapManager.Instance` (both `DontDestroyOnLoad`)
- **State Machine**: `EnemyStateMachine` with states: Idle, Patrol, Chase, Attack, Search, Dead
- **Strategy**: `SkillEffectBase` (skill effects), `ItemEffectBase` (item effects), `BuffEffectBase` (buff effects), `IAttackBehavior` (MeleeAttack / RangedAttack)
- **Object Pool**: `ProjectilePool` (static, prefab-keyed, `IPoolable` lifecycle)
- **Event-Driven**: `System.Action` delegates throughout (OnHealthChanged, OnDied, OnRoomChanged, OnSkillCast, OnAssimilated, etc.)
- **Facade**: `EnemyCore` aggregates EnemyHealth, EnemyMovement, EnemySkillManager, EnemyStateMachine, IAttackBehavior

### Core Interfaces (`Assets/Scripts/Interface/Interface.cs`)

- `IDamageable` — `TakeDamage(float)`
- `IHealable` — `Heal(float)`
- `IBuffTarget` — exposes `BuffManager`
- `IEnemy : IDamageable, ISkillCaster` — full enemy contract
- `ISkillCaster` — `CasterTransform`, `GetTargetDirection()`, `GetAttackStrength()`, `GetOwnerType()`
- `IAttackBehavior` — MeleeAttack / RangedAttack
- `IPoolable` — `OnSpawn()`, `OnReturn()` for object pool lifecycle

### ScriptableObject Configuration

| SO Class | Menu Path | Purpose |
|----------|-----------|---------|
| `MapConfig` | Map/Map Config | Dungeon generation params (room counts, pools, seed, spacing) |
| `RoomConfig` | Map/Room Config | Room type, doors, size, enemy/item spawn pools |
| `EnemyConfig` | Enemy/Enemy Config | Enemy stats, patrol/chase speed, detection, attack type, skill library |
| `SkillData` | Game/Skill Data | Skill stats (cooldown, damage multiplier, max level, effect strategy) |
| `SkillLibrary` | Game/Skill Library | Factory for `SkillInstance` creation by skillId |
| `BuffData` | — | Buff definition |
| `ItemData` | — | Item definition |

## Core Systems

### Player (`Assets/Scripts/Player/`)
- `PlayerManager` — Persistent singleton, auto-creates via `[RuntimeInitializeOnLoadMethod]`, spawns Player from `Assets/Prefabs/Player.prefab`
- `PlayerController` — Input handling (WASD move, mouse shoot, Q/E/Z/X skills), input lock during room transitions
- `PlayerStats` — Single source of truth for all player stats (health, speed, attack, fire rate, collider radius), implements `IDamageable`, `IHealable`, `IBuffTarget`
- `PlayerCombat` — Shooting logic, uses `ProjectilePool`, mouse-aim direction
- `PlayerSkillManager` — 4 skill slots (Q/E/Z/X), skill acquisition/upgrade/equip, implements `ISkillCaster`

### Map & Rooms (`Assets/Scripts/Map/`)
- `MapManager` — Singleton, procedural dungeon generation, room instantiation, room switching with camera transition
- `MapGenerator` / `RoomGraph` / `RoomNode` — Graph-based room layout generation
- `RoomRoot` / `RoomManager` / `RoomPortal` — Room lifecycle, player enter/exit, door connections
- `RoomCameraController` — Camera transition between rooms (locks position, preserves z-axis; **z must be -10**)
- `EnemySpawner` / `ItemSpawner` — Runtime spawning within rooms
- `MinimapUI` — Minimap display
- Room types: `Start`, `Normal`, `Treasure`, `Boss`, `Shop`, `Exit`, `Hidden` (breakable walls)

### Enemy (`Assets/Scripts/Enemy/`)
- `EnemyCore` — Facade implementing `IEnemy`, auto-collects sub-components, handles assimilation
- `EnemyHealth` / `EnemyMovement` / `EnemySkillManager` / `EnemyStateMachine` — Component-based
- States: `IdleState`, `PatrolState`, `ChaseState`, `AttackState`, `SearchState`, `DeadState`
- `EnemyFollower` — Takes over assimilated enemies (follows player in formation)
- `BossCore` / `BossPhaseData` — Boss with phase transitions
- Attack behaviors: `MeleeAttack`, `RangedAttack` (via `IAttackBehavior`)

### Skill (`Assets/Scripts/Skill/`)
- `SkillLibrary` (SO) — Factory: `CreateSkillInstance(skillId)` injects `OnExecute` delegate
- `SkillInstance` — Runtime instance with cooldown, level, damage multiplier
- `SkillSlot` — Key binding, unlock state, equip/cast
- `PlayerSkillManager` — 4 slots (Q/E/Z/X), acquire/equip/upgrade/expand
- Effects: `ProjectileSkillEffect`, `AoESkillEffect`, `DashSkillEffect`, `HealSkillEffect`, `HomingProjectileSkillEffect`, `SummonSkillEffect`
- Concrete skills: `BacteriophageProjectile`, `ErodeProjectile`, `StreptomycesProjectile`, `PhageMarkComponent`
- Skill stealing popup: `SkillStealPopupManager` — steal skills from enemies
- Erode choice popup: `ErodeChoicePopupManager` — Devour vs Assimilate choice

### Item (`Assets/Scripts/Item/`)
- `ItemData` (SO), `ItemEffectBase` (strategy), `ItemManager`, `ItemPickup`, `ItemQuality`
- Effects: `StatModifierEffect`, `ApplyBuffEffect`, `ConditionalEffect`
- Prefabs: BeltOfStrength, PhoenixFeather, SerpentFang, ThornArmor, VampiricTouch, WindBoots
- UI: `ItemUIController`, `ItemPanel`, `ItemSlotView`, `ItemDetailPopup`

### Buff (`Assets/Scripts/Buff/`)
- `BuffData` (SO), `BuffEffectBase` (strategy), `BuffManager`, `BuffInstance`, `StatModifier`
- Effects: `StatBuff`, `DamageOverTimeBuff`, `HealOverTimeBuff`, `OnHitBuff`, `OnHitHealBuff`, `OnDamagedBuff`

### Projectile (`Assets/Scripts/Bullet/`)
- `Projectile` — OwnerType (Player/Enemy), trigger-based hit detection, auto-return to pool
- `ProjectilePool` — Static object pool, `Prewarm()`, `Get()`, `Return()`, `IPoolable` lifecycle

### Currency (`Assets/Scripts/Currency/`)
- `CurrencyManager`, `CurrencyPickup`, `CurrencyUI`

### Shop (`Assets/Scripts/Shop/`)
- `ShopManager`, `ShopItemPurchasePopup`

### System UI (`Assets/Scripts/SystemUI/`)
- `StartMenuController`, `WinningMenuController`

## Folder Structure

```
Assets/
├── Materials/          # SpriteLit.mat (shared Sprite-Lit-Default for URP 2D lighting)
├── Prefabs/
│   ├── Boss/           # Boss.prefab
│   ├── Enemy/          # Enemy.prefab
│   ├── Items/          # ItemPickup_*.prefab, ItemSlotView.prefab
│   ├── Projectiles/    # Bullet.prefab, skill projectiles (Bacteriophage, Erode, Streptomyces)
│   ├── Rooms/          # Room_*.prefab (Start, Normal, Boss, Shop, Treasure, Hidden, Exit)
│   ├── UI/             # UICanvas.prefab, Skill/
│   └── Player.prefab
├── Resources/
│   ├── Enemy/          # Runtime-loaded enemy assets
│   ├── Map/            # Runtime-loaded map assets
│   ├── Skills/         # SkillLibrary.asset, EnemySkillLibrary.asset, Data/
│   └── TestAssets/
├── Scenes/             # SampleScene.unity, LightTestScene.unity
├── Scripts/
│   ├── Buff/           # Core/, Effects/, Modifiers/
│   ├── Bullet/         # Projectile.cs, ProjectilePool.cs
│   ├── Currency/       # CurrencyManager, CurrencyPickup, CurrencyUI
│   ├── Enemy/          # Behaviors/, Boss/, Components/, Core/, States/
│   ├── Interface/      # Interface.cs (IDamageable, IHealable, IBuffTarget)
│   ├── Item/           # Core/, Effects/, UI/
│   ├── Map/            # Camera/, Config/, Core/, Room/, Runtime/, UI/
│   ├── Player/         # PlayerManager, PlayerController, PlayerStats, PlayerCombat
│   ├── Shop/           # Core/, UI/
│   ├── Skill/          # ConcreteSkill/, Effects/, UI/
│   ├── SystemUI/       # StartMenuController, WinningMenuController
│   ├── CameraFollow2D.cs
│   ├── DamagePopup.cs
│   └── PlayerController2D.cs  # Legacy (unused, superseded by Player/PlayerController.cs)
├── Settings/           # URP assets (UniversalRP.asset, Renderer2D.asset), scene templates
├── Textures/           # Map/, WhiteCircle.asset
└── TJGenerators/       # AI generation package assets
```

## Development Conventions

- **No Assembly Definition files** — all scripts compile into a single `Assembly-CSharp`
- **Naming**: PascalCase for classes/interfaces; `_camelCase` for private serialized fields; `I` prefix for interfaces
- **ScriptableObject config**: `[CreateAssetMenu]` for all data assets
- **Comments**: Chinese (中文) for all documentation comments
- **Legacy PlayerController2D.cs** and `CameraFollow2D.cs` in `Scripts/` root are superseded by `Player/PlayerController.cs` and `Map/Camera/RoomCameraController.cs`
- **Tag "Player"**: Used by `PlayerManager`, `MapManager`, `EnemyCore`, and `Projectile` for target detection. Assimilated enemies change tag to "Player"
- **Tag "Enemy"**: Used by `Projectile` for player-owned projectile hit detection
- **Tag "Obstacles"**: Blocks projectiles
- **Layer 5 (UI)**: All UI elements

## Package & Dependency List

| Package | Version | Source |
|---------|---------|--------|
| com.unity.render-pipelines.universal | 14.0.12 | Unity Registry |
| com.unity.feature.2d | 2.0.1 | Unity Registry |
| com.unity.textmeshpro | 3.0.7 | Unity Registry |
| com.unity.timeline | 1.7.7 | Unity Registry |
| com.unity.ugui | 1.0.0 | Unity Registry |
| com.unity.test-framework | 1.1.33 | Unity Registry |
| com.unity.visualscripting | 1.9.4 | Unity Registry |
| com.unity.ide.rider | 3.0.36 | Unity Registry |
| com.unity.ide.visualstudio | 2.0.22 | Unity Registry |
| com.unity.collab-proxy | 2.12.4 | Unity Registry |
| cn.tuanjie.codely.bridge | 1.0.74 | Unity Registry |
| cn.tuanjie.ai.generators | local | .codely-cli/extensions/TJGenerators |
| cn.tuanjie.shadergraph-mcptools | local | .codely-cli/extensions/ShaderGraph-AI |

## Building & Running

- **Editor**: Open project → open `SampleScene.unity` → press **Play**
- **CLI/Batchmode**:
  ```bash
  Unity -batchmode -quit -projectPath . -buildTarget Win64 -logFile
  ```
- **Testing**: Unity Test Framework 1.1.33 is installed but no test scripts exist yet

## Version-Control Tips

- Ignored: `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `UserSettings/`, `.vs/`
- Committed: `Assets/`, `ProjectSettings/`, `Packages/manifest.json`, `CODELY.md`
- `.codely-cli/` directory contains AI development extensions and auto-saves (not part of Unity project)

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-08-04 19:58:48] 项目所有预制体(SpriteRenderer)已从 Sprites/Default 材质批量修复为 Sprite-Lit-Default (Assets/Materials/SpriteLit.mat)，以支持 URP 2D 光照。新增预制体务必使用 Sprite-Lit-Default 材质，否则不响应 2D 灯光。
- [2026-08-04 19:58:48] 2D 场景相机需要 z=-10 才能正常渲染。RoomCameraController.LateUpdate 会锁定 transform.position 到 _targetPosition 但保留 z 轴，因此场景中相机的初始 z 值很关键。
- [2026-08-04 20:16:34] ShaderGraph-AI 扩展包 (cn.tuanjie.shadergraph-mcptools) 的 MCPtools.GraphSettings.cs 曾因缺少条件编译守卫导致 77 个编译错误（HDRP 类型在 URP-only 项目中不可用）。已修复：asmdef 添加 versionDefines（URP_INSTALLED/HDRP_INSTALLED），代码中所有 HDRP/URP 专用段用 #if 守卫。注意 #region/#endregion 必须在 #if/#endif 外面，否则 false 分支时 #endregion 被跳过导致 CS1027。
- [2026-08-04 21:22:47] 新增技能时，资产按技能名分散到子文件夹：Prefab/Projectiles/{技能名}/ 放预制体+材质+特效；Resources/Skills/Data/{技能名}/ 放 SkillData.asset + SkillEffect.asset。共享资产（通用 Bullet/Skilltest 等）保留在 Projectiles/ 根目录。
- [2026-08-04 21:22:47] 新增技能时，只在 ConcreteSkill/ 中建脚本（投射物命中逻辑等），不要动 Effects/ 文件夹。如果必须新增 Effects/，类必须是泛用的（接口/抽象基类），能被多个技能复用。如 HomingProjectileSkillEffect + IHomingProjectile。效果资产仍用具体命名（如 BacteriophageEffect.asset），但引用的类类型是泛用的。
- [2026-08-04 21:22:47] Boss技能已整合入主技能架构：BossConfig.skillLibrary 须指向主 SkillLibrary；BossPhaseData 引用 SkillData[] 通过 GUID；BossCore 使用 EnemySkillManager.LoadSkills() 而非自维护列表；Boss 上 EnemySkillManager 须设 _manualTick=true 避免与 BossCore.Update() 双重 Tick。
- [2026-08-04 21:22:47] SummonSkillEffect 根据 ownerType 区分阵营：Player 方用 playerMinionPrefabs（Tag=Player，以 Enemy 为目标），Enemy 方用 enemyMinionPrefabs（Tag=Enemy，以 Player 为目标）。EnemyCore.PlayerTarget setter 为 public 以支持召唤后设初始目标。
- [2026-08-04 21:22:47] 场景 UI 统一在单个 UICanvas（1920×1080, ScaleWithScreenSize, Match 0.5）下，功能分区为：SkillUI（技能面板+Popups）、ItemUI（物品+详情弹窗）、CurrencyPanel、MinimapUI。所有弹出面板（SkillStealPopup/ErodeChoicePopup）放在 SkillUI/Popups 下。
- [2026-08-05 13:45:06] 2D 光照架构：低全局光 + 对象自带点光源的黑暗探索风格。Global Light 2D（场景）提供全局光；Player.prefab/Enemy.prefab/Boss.prefab 各有子对象 PlayerLight/EnemyLight/BossLight（Point Light 2D, blendStyleIndex=1），技能投射物（Bullet/Bacteriophage/Erode/Streptomyces）也自带点光源。2026-08-05 按用户要求调整：Global Light 0.15→0.55（增强地图光照），角色/敌人点光源削弱至极弱（Player 0.8→0.05、Enemy 0.5→0.03、Boss 2.0→0.1）。新增敌人/角色 prefab 时应考虑是否自带点光源。
- [2026-08-05 13:45:06] unity_asset.modify 无法修改 prefab 中嵌套子对象上的组件属性（如 PlayerLight 子对象的 Light2D.intensity，报 "No applicable or modifiable properties"）。此类修改需用 execute_csharp_script：AssetDatabase.LoadAssetAtPath<GameObject> + GetComponentsInChildren<T>(true) 修改后 EditorUtility.SetDirty + AssetDatabase.SaveAssets()。
- [2026-08-05 14:16:23] URP 2D Renderer（2D Renderer 资产）下，自定义 shader 用于 LineRenderer/非 Sprite 渲染时，只有 LightMode="UniversalForward" 的 pass 不会渲染（材质引用正常但完全不可见），必须额外提供 LightMode="Universal2D" 的 pass。2026-08-05 重制连锁闪电技能时踩坑：Streptomyces 技能从投射物改为 ChainLightningSkillEffect，闪电弧 LineRenderer 最初不可见，补 Universal2D pass 后正常。
- [2026-08-05 14:23:26] 泛用连锁闪电效果类位于 Assets/Scripts/Skill/Effects/ChainLightningSkillEffect.cs（含 ChainLightningArcRuntime + ChainLightningHitGlowRuntime 泛用视觉组件），为参数驱动（maxTargets/jumpDelay/材质/颜色等），可被多个连锁类技能复用。2026-08-05 从 ConcreteSkill/ 移至 Effects/ 并泛用化（AssetDatabase.MoveAsset 保留 GUID，Streptomyces/ChainLightningEffect.asset 引用未断）。

### Reference
- [2026-08-05 15:04:45] 项目经验知识库位于 .codely-cli/experience/：development/（bug 修复经验，命名 YYYYMMDD_Unity[问题].md）和 templates/（可复用代码模板）。2026-08-05 首次建立，含 URP 2D 自定义 shader 需 Universal2D pass、LineRenderer shader uv 陷阱、MoveAsset 保留 GUID、程序化闪电弧模板等 8 篇。unity-game-debugger 调试时优先检索该目录。
