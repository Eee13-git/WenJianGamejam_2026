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

## Codely Structured Memoriesundefined
- [2026-08-12 00:20:11] [2026-08-11] 批量创建敌人预制体的标准工作流：①基于现有 Enemy.prefab 模板用 Instantiate 克隆；②直接设 EnemyCore.config = xxx（不用 SerializedObject，实测 SerializedObject.FindProperty+ApplyModifiedProperties 在 PrefabUtility.SaveAsPrefabAsset 后不持久化）；③设 SpriteRenderer.sprite；④设子对象 Light2D.color；⑤PrefabUtility.SaveAsPrefabAsset + UnloadPrefabContents。验证 config 引用必须用 PrefabUtility.LoadPrefabContents 读回（AssetDatabase.LoadAssetAtPath 有缓存滞后会返回 null，但磁盘 YAML 数据正确）。



### User

### Feedback

### Project
- [2026-08-04 19:58:48] 项目所有预制体(SpriteRenderer)已从 Sprites/Default 材质批量修复为 Sprite-Lit-Default (Assets/Materials/SpriteLit.mat)，以支持 URP 2D 光照。新增预制体务必使用 Sprite-Lit-Default 材质，否则不响应 2D 灯光。
- [2026-08-04 19:58:48] 2D 场景相机需要 z=-10 才能正常渲染。RoomCameraController.LateUpdate 会锁定 transform.position 到 _targetPosition 但保留 z 轴，因此场景中相机的初始 z 值很关键。
- [2026-08-04 20:16:34] ShaderGraph-AI 扩展包 (cn.tuanjie.shadergraph-mcptools) 的 MCPtools.GraphSettings.cs 曾因缺少条件编译守卫导致 77 个编译错误（HDRP 类型在 URP-only 项目中不可用）。已修复：asmdef 添加 versionDefines（URP_INSTALLED/HDRP_INSTALLED），代码中所有 HDRP/URP 专用段用 #if 守卫。注意 #region/#endregion 必须在 #if/#endif 外面，否则 false 分支时 #endregion 被跳过导致 CS1027。
- [2026-08-08 20:13:42] 新增技能时，资产按技能名分散到子文件夹：Prefab/SkillAsset/{技能名}/（2026-08-06 从 Projectiles 重命名，放预制体+材质+特效+shader）；Resources/Skills/Data/{技能名}/ 放 SkillData.asset + SkillEffect.asset。共享资产（通用 Bullet/Skilltest 等）保留在 SkillAsset/ 根目录，通用 SpriteLit.mat 保留在 Materials/。

- [2026-08-04 21:22:47] 新增技能时，只在 ConcreteSkill/ 中建脚本（投射物命中逻辑等），不要动 Effects/ 文件夹。如果必须新增 Effects/，类必须是泛用的（接口/抽象基类），能被多个技能复用。如 HomingProjectileSkillEffect + IHomingProjectile。效果资产仍用具体命名（如 BacteriophageEffect.asset），但引用的类类型是泛用的。
- [2026-08-04 21:22:47] Boss技能已整合入主技能架构：BossConfig.skillLibrary 须指向主 SkillLibrary；BossPhaseData 引用 SkillData[] 通过 GUID；BossCore 使用 EnemySkillManager.LoadSkills() 而非自维护列表；Boss 上 EnemySkillManager 须设 _manualTick=true 避免与 BossCore.Update() 双重 Tick。
- [2026-08-04 21:22:47] SummonSkillEffect 根据 ownerType 区分阵营：Player 方用 playerMinionPrefabs（Tag=Player，以 Enemy 为目标），Enemy 方用 enemyMinionPrefabs（Tag=Enemy，以 Player 为目标）。EnemyCore.PlayerTarget setter 为 public 以支持召唤后设初始目标。
- [2026-08-04 21:22:47] 场景 UI 统一在单个 UICanvas（1920×1080, ScaleWithScreenSize, Match 0.5）下，功能分区为：SkillUI（技能面板+Popups）、ItemUI（物品+详情弹窗）、CurrencyPanel、MinimapUI。所有弹出面板（SkillStealPopup/ErodeChoicePopup）放在 SkillUI/Popups 下。
- [2026-08-05 13:45:06] 2D 光照架构：低全局光 + 对象自带点光源的黑暗探索风格。Global Light 2D（场景）提供全局光；Player.prefab/Enemy.prefab/Boss.prefab 各有子对象 PlayerLight/EnemyLight/BossLight（Point Light 2D, blendStyleIndex=1），技能投射物（Bullet/Bacteriophage/Erode/Streptomyces）也自带点光源。2026-08-05 按用户要求调整：Global Light 0.15→0.55（增强地图光照），角色/敌人点光源削弱至极弱（Player 0.8→0.05、Enemy 0.5→0.03、Boss 2.0→0.1）。新增敌人/角色 prefab 时应考虑是否自带点光源。
- [2026-08-05 13:45:06] unity_asset.modify 无法修改 prefab 中嵌套子对象上的组件属性（如 PlayerLight 子对象的 Light2D.intensity，报 "No applicable or modifiable properties"）。此类修改需用 execute_csharp_script：AssetDatabase.LoadAssetAtPath<GameObject> + GetComponentsInChildren<T>(true) 修改后 EditorUtility.SetDirty + AssetDatabase.SaveAssets()。
- [2026-08-05 14:16:23] URP 2D Renderer（2D Renderer 资产）下，自定义 shader 用于 LineRenderer/非 Sprite 渲染时，只有 LightMode="UniversalForward" 的 pass 不会渲染（材质引用正常但完全不可见），必须额外提供 LightMode="Universal2D" 的 pass。2026-08-05 重制连锁闪电技能时踩坑：Streptomyces 技能从投射物改为 ChainLightningSkillEffect，闪电弧 LineRenderer 最初不可见，补 Universal2D pass 后正常。
- [2026-08-05 14:23:26] 泛用连锁闪电效果类位于 Assets/Scripts/Skill/Effects/ChainLightningSkillEffect.cs（含 ChainLightningArcRuntime + ChainLightningHitGlowRuntime 泛用视觉组件），为参数驱动（maxTargets/jumpDelay/材质/颜色等），可被多个连锁类技能复用。2026-08-05 从 ConcreteSkill/ 移至 Effects/ 并泛用化（AssetDatabase.MoveAsset 保留 GUID，Streptomyces/ChainLightningEffect.asset 引用未断）。
- [2026-08-05 15:44:12] 进化倾向系统（2026-08-05 新增）：PlayerStats 新增 evolutionTendency (float, 值域 -100~100，正值=朝向宿主，负值=朝向独特)，已接入 GetStatValue/SetStatValue Buff 接口（Set 时 Clamp ±100）。UI 面板 PlayerStatsPanel 挂在 UICanvas/StatsPanel（anchor(0,1) pos(20,-150)，CurrencyPanel 下方），显示生命/移速/攻击/射速 + 进化倾向双向槽：右半槽 BarHost（金色 fillOrigin=Left 从中心向右=正值宿主）、左半槽 BarUnique（紫色 fillOrigin=Right 从中心向左=负值独特），数值文本"宿主 +60/独特 -40/平衡 0"。
- [2026-08-05 15:44:12] 场景中 UICanvas 是 Assets/Prefabs/UI/UICanvas.prefab 的实例（场景内无对象本体，只有 PrefabInstance 引用），修改 prefab 资产后场景实例自动同步。在 prefab 中创建复杂 UI 层级用 Editor 脚本：PrefabUtility.LoadPrefabContents → 构建 UI（RectTransform/Image/TextMeshProUGUI，复用现有 TMP 字体）→ SerializedObject 绑定字段 → PrefabUtility.SaveAsPrefabAsset。注意 SaveAsPrefabAsset 后 UnloadPrefabContents 会销毁临时对象，之后的引用比较会显示 null（预期）。
- [2026-08-05 16:10:59] 进化倾向联动（2026-08-05）：Erode 技能二选一弹窗（ErodeChoicePopupManager）现在影响进化倾向——吞噬(Devour)成功 → 进化倾向 +5（朝向宿主/金色），同化(Assimilate)成功 → 进化倾向 -5（朝向独特/紫色）。偏移量字段 _evolveDevourDelta/_evolveAssimilateDelta 可配置（默认5）。修改通过 PlayerStats.SetStatValue("EvolutionTendency") 统一接口，面板单槽自动更新。注意 OnDevour 的偏移加在弹窗打开前（无论有无技能都算吞噬成功）；若需"无技能时不偏移"应移到 SkillStealPopupManager 回调。
- [2026-08-05 18:53:06] 相机受击反馈系统（2026-08-05 新增）：CameraShake.cs 挂在 Main Camera，提供 Shake(倍率)（LateUpdate 叠加随机偏移，前强后弱衰减，时长0.18s 幅度0.35）与 FlashRed()（相机子对象 DamageFlash 全屏红色 Image 淡出）。敌人受击（EnemyHealth.TakeDamage）→ Shake(0.7)；角色受击（PlayerStats.TakeDamage）→ Shake(1)+FlashRed。**关键**：执行顺序必须 RoomCameraController[DefaultExecutionOrder=100] 先锁定位置，CameraShake[200] 后叠加振动，否则振动被锁定逻辑覆盖。
- [2026-08-05 18:53:06] 道具池刷新过滤（2026-08-05）：ItemPoolFilter.cs（Item/Core/ 静态类）提供 GetAvailablePool(pool, itemManager) 过滤玩家已达 maxCount 上限的道具（从 ItemPickup.itemData 取 ItemData 比对 ItemManager.GetItemCount），GetPlayerItemManager() 统一取玩家 ItemManager。已接入 ItemSpawner.Spawn() 与 ShopManager.OpenShop()，池过滤后为空则跳过生成/无法开张。现有 6 种道具 maxCount 均=0（无上限），配置 maxCount>0 后自动生效。
- [2026-08-08 20:35:55] [2026-08-06] 泛用扩散波技能类：WaveSkillEffect（Effects/ 参数驱动 SO）+ WaveRuntime（ConcreteSkill/ 运行时，合并原 TimeStopManager+AxonBlockWave）。参数全部可选：waveSpeed/maxRadius/spreadAngle(360=圆形波,<360=扇形)/damage/freezeDuration/freezeProjectiles/cancelOnAttack/cancelOnSkillCast/cancelGracePeriod/waveMaterial/overlayMaterial。轴突传导阻滞（axon_block，X键，圆形波+时停2s+冻结弹幕+普攻/技能取消）是其中一种配置。技巧：①覆盖层 _Progress 用 0.5s sin 脉冲（0→峰值→0）实现"扩散时一次性画面扭曲"，勿用持续 ramp；②shader 全圆(360°)时必须跳过扇区裁剪（smoothstep 边界反转会出错）。
- [2026-08-08 20:35:55] [2026-08-06] shader 驱动全屏/区域渲染的 SpriteRenderer 陷阱：①必须给 SpriteRenderer 赋 Sprite 否则完全不渲染；②运行时生成纯白 sprite 用 1x1 纹理 + PPU=1（Sprite.Create(tex, rect, pivot, 1f)），这样 localScale 直接等于世界尺寸（如 maxRadius*2=20），若用 8x8+PPU=100 则 sprite 仅 0.08 单位导致 shader 的 UV 半径映射全部压缩在中心不可见。WaveRuntime.CreateWhiteSprite() 是现成范例。
- [2026-08-08 21:17:46] [2026-08-08] 持续喷射型技能架构：TorrentSkillEffect（Effects/ 参数驱动 SO：duration/range/spreadAngle/DPS/damageInterval/knockbackSpeed/torrentMaterial）+ TorrentRuntime（ConcreteSkill/ 运行时）。溶栓灌注（thrombolytic_infusion）：20s 扇形喷射、期间 PlayerController.InputLocked=true 锁移动、后坐力 rb.velocity=-dir*knockbackSpeed（实时跟随鼠标）、扇形伤害 OverlapCircleAll+Vector2.Angle 过滤。视觉 = shader 扇形底色 + ParticleSystem 液滴粒子（Cone 60°、180/s、LookRotation(dir, Vector3.forward) 映射到 XY 平面——Cone 默认沿局部 +Z 发射）。陷阱：勿设 ParticleSystem main.duration（播放中设置 assert，技能结束直接 Destroy）。
- [2026-08-08 21:17:46] [2026-08-08] shader 液态喷射特效经验（CorrosiveTorrent 迭代 3 轮）：①"扫描感"元凶是沿角度方向的 sin 条带（angle*k），必须用沿喷射方向的径向涌流（sin(worldDist*freq - t*speed)）替代；②纯 shader 扇形无论如何调都是"能量场"，真正的液体喷溅感必须加 ParticleSystem 粒子；③边界柔和=smoothstep 过渡加宽(≥0.3弧度)+FBM 湍流扰动边界角；④内部液体感=低频大块浓淡斑块(scale 2-3 主导)+中频细节+放大气泡+液滴高光。经验库：.codely-cli/experience/。
- [2026-08-08 22:14:13] [2026-08-08] 自定义 shader 在 URP 2D 下编译失败的静默陷阱：HLSL 语法错误（如重复变量声明 float mask）导致 pass 编译失败时，URP 2D Renderer **静默 fallback 到 Sprites/Default**（渲染白色 sprite 在浅色背景上不可见），控制台不报 shader 错误、材质不显粉色——表现是"技能特效只有粒子系统可见，shader 底色完全消失"。排查方法：把 frag 改成 `return float4(1,0,0,1)` 纯色调试——若出现红色方块则渲染路径 OK（问题在 frag 逻辑），若仍不可见则 shader 编译失败（查变量重名/语法）。教训：多次 replace 编辑 shader 时易引入重复声明，改完必须验证 `float mask` 等变量声明次数（两个 pass 各一次为正常）。
- [2026-08-08 22:31:58] [2026-08-08] 地面区域持续伤害技能架构（高渗裂解剂 hypertonic_lysate）：GroundSolutionZone（ConcreteSkill/ 泛用组件，radius/dps/damageInterval/duration 参数驱动）+ ProjectileSkillEffect(speed=0) 原地释放生成。预制体= SpriteRenderer(溶液shader)+Projectile+GroundSolutionZone，**无需 Collider**（伤害用 OverlapCircleAll）。关键陷阱：①ProjectileSkillEffect 只调 Projectile.Initialize 不调自定义组件 Initialize，组件须在 Start 用 Inspector 参数自初始化（_initialized=true）；②Projectile 的 OnTriggerEnter2D 会在 Start 禁用前触发导致溶液被命中销毁——必须在 Awake 就 proj.enabled=false，且预制体不要加 Collider；③Start 里 proj.CancelInvoke("ReturnToPool") 取消 Initialize 安排的自动回收。阵营从 proj.Owner 读取（Projectile 已加公开 Owner 属性）。
- [2026-08-08 23:14:34] [2026-08-08] 房间级控制 debuff 技能架构（急性谵妄 acute_delirium）：①新增敌人状态机状态 ConfusedState（随机漫步：每0.8s换随机方向，不检测玩家/不攻击/不施技能），②DeliriumBuff（BuffEffectBase 泛用减益：OnApply 清 PlayerTarget+禁 SkillManager+切 ConfusedState；OnRemove 恢复技能+重新找玩家+切回 Idle/Patrol；IsAssimilated 随从跳过），③DeliriumTrigger（ProjectileSkillEffect speed=0 触发体：Start 时 FindObjectsOfType<EnemyCore> 给全房间存活未同化敌人 ApplyBuff + 复用 DiffusionWave shader 紫色扩散环视觉动画后销毁）。敌人状态机状态列表：Idle/Patrol/Chase/Attack/Search/Dead/Confused。buffId=delirium_debuff, duration=5s, Refresh。
- [2026-08-09 00:15:55] [2026-08-08] 护盾/架盾类技能架构（角质增生覆膜 keratin_shield）：KeratinShieldRuntime（ConcreteSkill/，挂在施法者身上）+ KeratinShieldSkillEffect（Effects/ 参数驱动）。关键实现：①PlayerController 新增 AttackLocked（攻击锁）+ SpeedMultiplier（移速倍率），普攻判断与 rb.velocity 已接入；②SkillInstance.ResetCooldown() + PlayerSkillManager/EnemySkillManager.ResetCooldownBySkillId()/GetKeyCodeBySkillId()（长按检测用）；③拦截接入 Projectile.OnTriggerEnter2D 和 MeleeAttack.TryAttack/OnTriggerStay2D（命中前检查 KeratinShieldRuntime.TryBlock()）；④完美格挡=释放后 perfectWindow 内 TryBlock → 刷新冷却+结束架盾，超窗仅挡伤害；⑤玩家长按=GetKeyCodeBySkillId 找键 + Update 里 Input.GetKey 检测，松开提前结束；⑥**重要陷阱**：运行时组件挂在施法者 GameObject 上，结束时只能 Destroy(this) 组件或销毁视觉子对象，绝不能 Destroy(gameObject)（会销毁施法者/玩家！）；⑦**2026-08-09 更新**：架盾只挡一次攻击（TryBlock 成功即消耗护盾→破碎动画→组件销毁，RestoreCaster 立即执行）；⑧格挡后无敌帧：通用 DamageImmunity 组件（GrantImmunity/IsImmune）+ PlayerStats/EnemyHealth 的 TakeDamage 开头检查 IsImmune（在生命组件入口拦截才能覆盖所有伤害来源如地面溶液），KeratinShield 参数 _immuneDuration=1.5s；⑨护盾 shader 圆形硬遮罩 circleMask=smoothstep(radius, radius+0.05, dist) 裁掉方形 sprite 角落残留辉光（dist 最大 1.414 处 glow 残影会露方形）。
- [2026-08-09 00:33:19] [2026-08-09] 镇痛阻滞（analgesic_block）技能架构——"伤害延迟/分摊"机制：①泛用挂 buff 技能类 ApplyBuffSkillEffect（Effects/，引用 BuffData 执行时给施法者 ApplyBuff，自增益技能只需配 BuffData 复用）；②AnalgesicBlockRuntime（ConcreteSkill/，挂在角色上，PendingDamage 延迟池 + SplitDamage(damage) 按 ImmediateFactor 拆分）；③PlayerStats/EnemyHealth 的 TakeDamage 检查镇痛组件拆伤害（与 DamageImmunity 无敌同入口，此处顺序：无敌→镇痛拆分→扣血）；④AnalgesicBlockBuff（BuffEffectBase：OnApply 激活运行时+PlayerController.IgnoreStun=true；OnRemove 把延迟池转痛觉残留 debuff+恢复+移除运行时）；⑤AnalgesicAfterBuff（动态总量 DOT：OnApply 从运行时 TakePendingDamage 取总量，OnTick 按 total/duration×tickInterval 匀速扣）；⑥硬直免疫=PlayerController.IgnoreStun + AoESkillEffect.StunRoutine 检查（项目里硬直=InputLocked 眩晕）。配置：buff 12s、immediateFactor 0.5、残留 DOT 5s、cooldown 20s。
- [2026-08-09 20:58:13] [2026-08-09] 退行性病变（regressive_disease）技能架构——"范围降攻"类效果：复用 ApplyBuffSkillEffect（给施法者挂 5s buff），buff 的 OnApply/OnRemove 作用于房间敌人。关键实现：①RegressiveDiseaseBuff（BuffEffectBase）OnApply 遍历所有 EnemyCore（排除 IsDead/IsAssimilated）将 MeleeAttack.damage 和 RangedAttack.damage 乘 damageFactor(0.5)，静态字典按 buff 记录原始值用于恢复，OnRemove 恢复+移除紫色标记；②**关键设计**：改 MeleeAttack/RangedAttack.damage 而非技能伤害数值，因为 EnemyCore.GetAttackStrength() 从这两个组件读取——降低它们同时覆盖近战/远程弹幕/技能伤害；③视觉=给敌人挂紫色柔边圆标记（运行时生成径向渐变 sprite + Sprites/Default 紫色 tint）。
- [2026-08-09 21:15:09] [2026-08-09] 骨髓应急崩解（bone_marrow_emergency）技能架构——"献祭/清场"类效果（BoneMarrowBuff，BuffEffectBase 一次性效果，复用 ApplyBuffSkillEffect）。关键设计：①OnApply 按施法者阵营分支：玩家施放=永久扣 1/2 血上限（PlayerStats.SetStatValue("MaxHealth")，当前血不变仅被新上限截断）+ 清所有非 Boss 敌人（TakeDamage 致死走正常死亡流程触发 RoomManager 清房）+ Boss 扣 1/4 血（TakeDamage 触发阶段切换）；敌人施放=扣自己血上限 + DamagePlayerAndFollowers()（找 PlayerStats 扣 MaxHealth×0.25 + 遍历 IsAssimilated 随从扣 MaxHealth×0.25，不清敌人）；②**找玩家不能用 FindGameObjectWithTag("Player")**——随从同化后 tag 也是 Player，必须找带 PlayerStats 组件的对象；③PlayerStats.SetStatValue 的 "MaxHealth" case 会把当前血 clamp 到新上限（天然实现"血上限减半时当前血超限截断"），GetStatValue/SetStatValue 已新增 "Health" case；④玩家施放时 ClearEnemies 跳过 IsAssimilated 随从（不伤自己人）；⑤装备槽位规则：新技能默认装 Player.prefab 槽位2（Z键，_initialSkillId2）。
- [2026-08-09 21:53:21] [2026-08-09] 因子掠夺（factor_plunder）技能架构——"偷取增益"类投射物效果：①BuffInstance 新增 SetRemainingDuration(float)（直接设置剩余时长，偷取/转移 buff 用）；②BuffPlunderOnHit 组件（挂投射物预制体）：Projectile.OnTriggerEnter2D 命中时调用 TryPlunder——**条件性偷取**：遍历目标 ActiveBuffs 找 buffType==Buff 且非 Indestructible 的增益，没有则返回 null 不偷；偷取=RemoveBuff(目标)+ApplyBuff(施法者)+SetRemainingDuration(10s)；③偷取必须**在伤害前**执行（TakeDamage 致死会销毁目标导致 BuffManager.OnDestroy 清 buff 偷不到）；④PlunderBuffSkillEffect（泛用投射物效果）配置 prefab/speed/buffDuration，SetDuration 覆盖预制体 Inspector 值。测试注意：Play Mode 反复退出可能因后台图标子代理触发 stop。
- [2026-08-09 22:12:45] [2026-08-09] 细胞部分重编程（cell_reprogramming）技能架构——"刷新冷却"类 buff 效果（ReprogramBuff，BuffEffectBase 一次性效果，复用 ApplyBuffSkillEffect）。关键：①PlayerSkillManager/EnemySkillManager 新增 ResetLongestCooldownSkill()——遍历找 IsCoolingDown 且 CooldownRemaining 最长的技能 ResetCooldown()，无冷却中返回 null；②**重要修复 ApplyBuffSkillEffect**：重复施放同技能时 BuffManager.ApplyBuff 同 ID 分支只刷新时长不调 OnApply → 一次性 buff 效果不重触发。修复=Execute 先 RemoveBuffById 再 ApplyBuff（强制重触发 OnApply）。影响所有 ApplyBuffSkillEffect 技能（镇痛阻滞/腹膜透析/退行性病变/骨髓崩解等）；③注意副作用：镇痛阻滞重复施放会先触发 OnRemove（转 DOT）再重新 OnApply，会产生残留痛觉残留 DOT（边缘情况可接受）。
- [2026-08-09 22:39:32] [2026-08-09] 肌束应急奔突（muscle_burst）技能架构——"卷起敌人冲刺"效果（SlideSkillEffect 扩展 + SlideRuntime 卷起能力，参数驱动：_carryEnemies/_carryRadius/_carryDamage/_wallDamage/_carriedInvincible）。**三个关键坑（调试多轮发现）**：①Physics2D.OverlapCircleAll **无 mask 重载会漏检 collider**（实测返回 0 而 ~0 全层返回 2），检测必须用 `OverlapCircleAll(pos, r, ~0)`；②卷起敌人吸附到玩家位置时，若不禁用其 Collider2D + Rigidbody2D.simulated=false，动态体物理碰撞会把玩家推回阻挡冲刺（玩家停在半路）；③卷起期间给敌人 GrantImmunity 无敌会**拦截撞墙二次伤害**——撞墙时必须先 Destroy 敌人的 DamageImmunity 再 TakeDamage；④撞墙判定用物理接触 OnCollisionEnter2D（MovePosition 撞 Obstacles/Wall 触发）而非提前 Raycast 预判（预判距离难调且误触发）。敌人卷起目标：玩家方（ownerType=Enemy 时 tag=Player，含随从）；Boss 不可卷起。
- [2026-08-11 19:04:13] CLI 环境直接用文件工具（replace/write_file）修改 Unity 资产文件（如 Player.prefab / YAML asset）后，Unity 不一定即时检测到（它在编辑器窗口获得 OS 焦点时才自动刷新）——需用 execute_csharp_script 调 AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)（仅 Edit Mode）强制重载，或用 unity_asset 工具改。2026-08-11 改 Player.prefab 初始技能后 Play 仍显示旧配置，Refresh 后正常。
- [2026-08-11 19:24:02] [2026-08-09] 自定义 shader 圆形遮罩参数必须 ≤1（UV 空间 dist=length(centered)*2 最大 √2≈1.414）：若 _MaxRadius/_FieldRadius 设 >1（如 PurifyWave 的 1.75），smoothstep(radius, radius+0.05, dist) 对任何像素恒为 0 → circleMask 永远=1 → 方形 sprite 角落露出方形轮廓。修复：半径参数设 0.9 内。扩散动画改由 transform.localScale 驱动（sprite 放大），遮罩参数保持 UV 空间值不变。同类坑：KeratinShield/DiffusionWave/AnalgesicField/PurifyWave 都适用。
- [2026-08-11 19:24:02] [2026-08-09] shader"气浪"特效实现要点（IntestinalPeristalsisWave，肠管蠕动冲击，泛用 WaveSkillEffect 的 waveMaterial）：①气浪主体=宽软多环叠加（主前缘 smoothstep 宽过渡 RingWidth 1.6 + 外侧第二道余波 echo），不是单条硬环；②边缘破碎感=FBM 湍流撕裂（edgeRip=smoothstep(0.42,0.62, fbm(高频))，edge*=0.55+0.45*edgeRip）；③内部气流翻涌=环形卷曲 swirl（worldDist*0.5 - t*1.2 + angle*0.8 正弦）+ churn（fbm 旋转）；④湍流场=FBM 3 octave（a 0.55, p*=2.1）；⑤前缘半径用湍流扰动 distortedRadius=_CurrentRadius + (turb-0.5)*Distortion*MaxRadius*0.55。
- [2026-08-11 20:08:21] [2026-08-11] 包膜爆发冲击（capsular_burst）技能架构——"自动锁敌突进+命中爆发气浪"：扩展 SlideSkillEffect/SlideRuntime（与肌束奔突共用，参数互斥）。①SlideSkillEffect 新增爆发配置段：_autoAimNearest（自动朝最近敌人突进，替代移动/鼠标方向）/ _burstOnHit（命中释放气浪推开，不卷走）/ _burstRadius / _burstPushForce / _burstWaveMaterial；②SlideRuntime.FindNearestTargetDirection() 用 OverlapCircleAll(pos, searchRadius, ~0) 找最近敌方（排除死/同化，无目标回退默认方向）；③CheckCarry 命中分支 _burstOnHit 时→冲撞伤害(carryDamage)+TriggerBurst() 且不卷起（_burstTriggered 一次冲刺只爆发一次）；④TriggerBurst=气浪视觉（SpawnBurstWave 复用 IntestinalPeristalsisWave 白色气浪材质，_CurrentRadius 0→max 扩散+淡出 0.4s）+ OverlapCircleAll(burstRadius, ~0) 径向击退 rb.velocity=dir*pushForce。配置：distance 3.5/speed 20/carryDamage 30/burstRadius 5/pushForce 12/cooldown 8s。敌人可通过 LoadSkills 施放。
- [2026-08-11 20:34:06] [2026-08-11] Unity 2D 冲刺/突进（SlideRuntime）物理坑（三轮排查）：①Dynamic 刚体 MovePosition 撞到其他 collider 会被物理反弹推开（"角色被碰走"）；②改 Kinematic 后若玩家 collider 与场景静态 collider 重叠（玩家站立时），Kinematic MovePosition 被完全阻塞（位移 0）——Kinematic 不做穿透分离；③**最终可靠方案**：冲刺期间 刚体改 Kinematic + 禁用自身全部 collider + 用 transform.position 直接位移（避开 MovePosition 物理约束），命中敌人靠 CheckCarry（OverlapCircleAll 检测目标 tag）→ 命中即 EndSlide 停在原地，撞墙靠移动前 OverlapCircleAll 探测前方 Obstacles/Wall → EndSlide；EndSlide+OnDestroy 双保险恢复刚体类型和 collider。Kinematic transform 位移不触发物理，可自由移动。
- [2026-08-11 22:08:09] [2026-08-11] 线粒体生物光子辐散（photon_radiance）技能架构——"预警+天降光柱+辐射池"：用 SummonSkillEffect 生成 5 道光柱 + PhotonBeam 组件。①SummonSkillEffect 新增 preferEnemySpawn/enemySpawnChance：生成位置 90% 概率随机落在敌方单位坐标（PickRandomEnemyPosition 返回 Vector2?，无敌方回退随机），并新增 PhotonBeam 初始化；②**最终视觉（3阶段干净利落）**：阶段1 预警瞄准环（细亮圆环+中心十字星芒+收缩脉冲 0.5s）；阶段2 光束从天而降（easeOutCubic 高度 5x→1x + 落地闪光扩散环 0.18s）；阶段3 落地一次性伤害（20+攻击力）后淡出销毁。**坑**：PhotonBeamCore.shader 用 _Mode 区分造型（0=瞄准环/1=光束/2=闪光），多 Sprite 各自 new Material 设 _Mode（漏设导致持续阶段仍显示圆环）；③**辐射池接管持续伤害**：光柱落地后 SpawnRadiancePool 生成独立 GameObject（**必须不设 parent，否则光柱销毁时池被连带销毁**）挂 PhotonRadiancePool 组件（每 0.25s OverlapCircleAll 给圈内敌人 ApplyBuff，Refresh 行为停留刷新，10s 后消散），debuff 复用泛用 DamageOverTimeBuff（15 DPS，buffId=photon_radiance_debuff）；④预制体 Sprite.Create 引用序列化丢失坑：Start 里 sr.sprite==null 重建纯白 1x1；⑤配置 count 5/spawnInterval 0.08/preferEnemySpawn 0.9/半径1.2/单段20+攻击力/cooldown 15s。
- [2026-08-11 22:46:38] [2026-08-11] ShaderGraph-AI 扩展包 MCPTools.asmdef 的 versionDefines 字段名必须是 "define"（单数）——原文件误写 "defines"（复数）导致 URP_INSTALLED/HDRP_INSTALLED 宏从未生效：shader_graph_urp_* 6 个工具不注册、set_surface_options 报 "No HDRP or URP target active"。判断法：CompilationPipeline.GetAssemblies(Editor) 查 a.defines 是否含宏，或反射查 UnityTcp.ShaderTools.MCPtoolsShaderGraphURP 类型是否存在。2026-08-11 已修复。参考正确写法：UnityTcp.Editor.asmdef 用 "define"。
- [2026-08-11 22:46:38] [2026-08-11] ShaderGraph 工具链限制（URP 2D 项目实测）：①编译或关闭 ShaderGraph 窗口后，未保存的节点/属性修改全部丢失（窗口内部字段被重置）——必须每完成一阶段立即 shader_graph_save；②shader_graph_get_nodes 基于 GetNodes&lt;AbstractMaterialNode&gt; 不含 Block 节点（get_node_slots 也查不到）；③set_surface_options 切 UniversalSpriteUnlitSubTarget 时 InitializeOutputBlocks 不生成 Block（工具只支持 Lit/Unlit/Decal），需手动调 SubTarget.GetActiveBlocks + graph.AddRemoveBlocksFromActiveList 创建（TargetActiveBlockContext 是 struct 无默认构造，需 FormatterServices.GetUninitializedObject）；④set_slot_value 不支持动态端口（DynamicVectorMaterialSlot/DynamicValueMaterialSlot），需用常量节点代替。综上 URP 2D 特效建议手写 shader，勿走 ShaderGraph 路线。
- [2026-08-11 22:46:38] [2026-08-11] unity_scene save 用 name 参数时保存路径可能偏离原场景（LHJ.unity 被存成 Assets/LHJ.unity 而非 Assets/Scenes/LHJ.unity，生成重复场景）。正确做法：保存用 ensure_scene_saved 或显式确认 path 参数；发现误存后立即删除错误路径文件并 ensure_scene_open 正确路径。
- [2026-08-11 23:03:02] [2026-08-11] photon_radiance 技能已重做为血色生物版（用户要求"简洁干练血色光柱+落地血池+生物质感"）：①PhotonBeamCore.shader 三模式血色化——Mode0 预警环（FBM 扰动环缘+双拍心跳脉动 pow(sin,8)+pow(sin,4)+环上血管纹理）、Mode1 光柱（FBM 扰动宽度+向下流动血色条纹+暗红主体0.95系数+鲜红细核0.85，顶部 lerp 用 y*y*sqrt(y) 避免 pow 负值警告）、Mode2 闪光（放射状血管纹+冲击环）；②新增 PhotonBloodPool.shader/.mat 血池——FBM 双噪声（主体边缘+外沿晕染）+肉质纤维+涟漪 frac(t*0.1)+中心鲜红+呼吸脉动，**必须用 SrcAlpha OneMinusSrcAlpha 标准混合**（加法混合会让血池显橙亮，标准混合才有厚重深红实体感）；③PhotonBeam.cs/PhotonRadiancePool.cs 新增 _poolMaterial 字段（prefab 已赋值）；④旧 PhotonBeam.shader/.mat、PhotonBeamColumn.* 已删除。
- [2026-08-11 23:03:02] [2026-08-11] 特效渲染验证经验：①**验证血色/暗色特效必须用 GameView 截图**——SceneView 浅色网格背景 + 加法混合下暗红特效显淡橙/发白（错误判断），实际游戏暗背景下完全正常；②Blend SrcAlpha One（加法）适合发光效果（光柱/预警环），Blend SrcAlpha OneMinusSrcAlpha（标准）才有实体厚重感（血池/液体）；③同一 shader 内 _Flash 参数在测试时勿设中间值（0.5 会把颜色拉向白色），测试常态应设 0；④加法混合下高光系数需压低（CoreColor 0.85 而非 1.3），否则过曝偏橙。
- [2026-08-11 23:03:02] [2026-08-11] 误建 shadergraph 资产删除后的残留：即使 .shadergraph 文件已删、Library/ShaderCache 已清，控制台仍反复报 "Shader error in 'Shader Graphs/Master': redefinition of formal parameter 'col'"（shadergraph 生成的 shader 名是 "Shader Graphs/Master"）。不影响自定义 shader 功能（ShaderUtil.ShaderHasError=False），重启 Unity 编辑器才彻底清除。判断残留来源：错误名含 "Shader Graphs/Master"（shadergraph 产物）vs "Custom/xxx"（自定义 shader）。
- [2026-08-12 00:52:45] [2026-08-11] EnemyHealth 已重构为敌人所有属性的**单一数据源**（类同 PlayerStats）：包含 MaxHealth / PatrolSpeed / ChaseSpeed / DetectionRange / AttackRange / ContactDamage / ContactDamageCooldown，提供 GetStatValue(name) / SetStatValue(name, val) 接口 + OnStatChanged 事件。EnemyMovement.MoveSpeed 字段已移除——MoveTowardsPosition 改为显式传入 speed 参数（由状态机从 Health 读取后传入）。EnemyCore.Awake 从 EnemyConfig 全量赋值到 Health。所有状态（Chase/Patrol/Idle/Search/Attack/Confused）、BossCore、KeratinShieldRuntime、RegressiveDiseaseBuff 已同步更新为读 Health 而非 config/Movement。Buff 系统现在可通过 health.SetStatValue("ChaseSpeed", 5f) 动态修改敌人属性。
- [2026-08-12 14:51:02] [2026-08-11] EnemyHealth 已重命名为 EnemyStats，与 PlayerStats 对称。改名涉及 8 个 .cs 文件（EnemyStats.cs 自身 + EnemyCore/BossCore/EnemyMovement/KeratinShieldRuntime/BoneMarrowBuff/CarriedThrow/DamageImmunity），用 replace_all 全量替换。Unity 通过 script GUID 序列化组件引用，改名不改 GUID 所以预制体零影响。
- [2026-08-12 14:51:02] [2026-08-11] MultiStatModifierEffect 资产修复：4 个药物资产（吗啡/哌替啶/异丙嗪/氯丙嗪）的 statName 序列化值 9 越界（枚举仅 0-8），实际应为 Health(1)。代码已加 Enum.IsDefined 守卫跳过越界值（不再报 warning）。
- [2026-08-12 14:55:00] [2026-08-11] EnemyHealth 已重命名为 EnemyStats（文件+类名同步），GUID 不变（rename 保留 .meta），所有 8 个引用文件已同步更新。prefab 零影响（Unity 用 script GUID 序列化组件，不存类名字符串）。EnemyStats 现为敌人全部属性的单一数据源（类同 PlayerStats），含 7 属性 + GetStatValue/SetStatValue + OnStatChanged 事件。
- [2026-08-12 15:41:35] [2026-08-12] 新增技能"球菌冲撞"(coccus_charge)：SlideSkillEffect 参数驱动——autoAimNearest=true（自动朝最近敌人）+ burstOnHit=true（命中即停+伤）+ carryEnabled=true（必须开，CheckCarry 才执行）+ carryRadius=1.2/ carryDamage=30/ distance=5/ speed=25/ immune=0.3s/ cooldown=6s。资产在 Resources/Skills/Data/CoccusCharge/（SkillData+SlideEffect+Icon）。冲撞方向在 Activate 时由 FindNearestTargetDirection 锁定，全程不可改。已注册 SkillLibrary (_allSkills[24])，Player.prefab _initialSkillId2=coccus_charge（Z 键）。
- [2026-08-12 15:41:35] [2026-08-12] Windows 下修改 Player.prefab 文件写入失败（EPERM: rename temp→destination）的终极方案：replace 工具和 Unity API（SerializedObject/PrefabUtility.SaveAsPrefabAsset）都因 Unity 持有文件锁而失败。解决办法——用 PowerShell 的 System.IO.File.ReadAllText/Wri teAllText/Delete/Move 原子操作直接读写磁盘文件，绕过 Unity 的文件锁。改完后 AssetDatabase.Refresh(ForceUpdate) 让 Unity 重载。
- [2026-08-12 16:38:55] [2026-08-12] Unity Play Mode 下批量录制技能 GIF 的方法：通过反射获取 `UnityTcp.Editor.Tools.ManageScreenshot` 类（在 UnityTcp.Editor 程序集中），调用 `BeginCapture(float scale)` → 每帧 `CaptureFrame()` → `EndCaptureToGif(string path, int fps, int colorCount)` 编码保存。关键坑：`EditorApplication.update` 注册的回调必须是 `EditorApplication.CallbackFunction` 类型，不能用 `System.Action`（会报 CS0029）。用 `EditorApplication.CallbackFunction updateAction = null; updateAction = () => {...};` 闭包 + 状态机模式（state 0=equip+wait, 1=cast+begin, 2=capture N frames, 3=save）逐个技能录制。录制前锁 `PlayerController.InputLocked=true`，录完解锁。每个技能的帧数应基于其效果持续时间（buff duration / wave travel time / projectile flight time）单独设定，而非统一帧数。
- [2026-08-12 20:44:48] [2026-08-12] 进化倾向联动技能伤害修正乘区：PlayerSkillManager.GetSkillDamageModifier() 返回 (1 + 倾向值×_evolveSkillDamageFactor) × _skillDamageMultiplier，默认 factor=0.025（倾向值±100 时修正 ±250%）。随从全属性增幅在 EnemyFollower 中实现：属性 = 原始值 × Max(0, 1 + (-倾向值)×_evolveFollowerBuffFactor)，默认 factor=0.01。两个 factor 字段在 PlayerStats Inspector 可调。PlayerStats.SetStatValue("EvolutionTendency") 变更时调 EnemyFollower.RefreshAllFollowers() 刷新所有活跃随从。
- [2026-08-12 21:23:39] [2026-08-12] 技能伤害修正乘区架构：ISkillCaster 新增 GetSkillDamageModifier() 接口，SkillLibrary.CreateSkillInstance 的 OnExecute 委托中 finalMultiplier = CurrentDamageMultiplier × caster.GetSkillDamageModifier()。PlayerSkillManager 实现 _skillDamageMultiplier（Inspector 可调，默认1.0）+ 进化倾向加成。EnemyCore 实现返回 1f。PlayerSkillManagerEditor 自定义 Inspector（Assets/Scripts/Skill/Editor/）用 Popup 下拉菜单替代手动输入 skillId，支持随机装配/清空按钮。坑：EditorGUILayout.LabelField(string,string) 被解析为 LabelField(string,GUIStyle) 因 GUIStyle 有 string 隐式转换，改用单参数拼接字符串。
- [2026-08-13 13:04:31] [2026-08-12] Sprite sheet 帧动画处理经验：①AI 生成的 sprite sheet 背景为暗绿色 chroma key，用 Unity Texture2D.GetPixels32 遍历像素设 alpha=0 去除（System.Drawing 在 execute_csharp_script 中不可用）；②各帧角色大小不一致时需裁剪到内容边界（扫描非透明像素的 minX/maxX/minY/maxY）再居中到统一画布；③TextureImporter.spritesheet 设置后 SaveAndReimport 会重置元数据为旧值——必须用 AssetDatabase.WriteImportSettingsIfDirty(assetPath) + ImportAsset(assetPath, ForceUpdate) 两步走；④Sprite 按名称排序必须用 Regex 提取数字比较（字符串排序 10<2）；⑤AnimationClip 用 AnimationUtility.SetObjectReferenceCurve + EditorCurveBinding.PPtrCurve 驱动 SpriteRenderer.m_Sprite；⑥呼吸动画帧序应为 small→medium→large→medium→small（正放+倒放去掉首尾重复帧）才平滑，不能只从小到大突变。
- [2026-08-13 20:24:11] [2026-08-13] 敌人技能释放动画同步机制：EnemySkillManager 新增 OnSkillCast 事件（参数 slotIndex, SkillData），TryCastSkill 成功后触发。EnemyCore.Awake 中订阅该事件 → Animator.SetBool("IsCasting", true) + 协程延迟 1.4s 复位。AnimatorController 用 bool 参数 IsCasting 控制 Idle→Cast 转换（Cast→Idle 用 exitTime=0.9 自动返回）。坑：①AnimatorConditionMode 无 Trigger 枚举值，只能用 If/IfNot + bool 参数；②prefab 通过 SaveAsPrefabAsset 设置 Animator.runtimeAnimatorController 不持久化（运行时为 NULL），需在 EnemyCore.Awake 中加 fallback：按 config.displayName 自动查找 Assets/Animations/Enemy/{name}/{name}_Controller.controller 并赋值（#if UNITY_EDITOR 用 AssetDatabase，否则用 Resources.Load）；③Texture2D.GetPixels32 要求 importer.isReadable=true，操作完后设回 false。
- [2026-08-14 20:31:48] [2026-08-13] 敌人朝向逻辑：EnemyCore.LateUpdate 中根据 PlayerTarget.position.x - transform.position.x 设置 SpriteRenderer.flipX。sprite 默认朝右，玩家在左(dx<0)→flipX=true(朝左)，玩家在右(dx>0)→flipX=false(朝右)。施法期间(IsCasting=true)锁定朝向不翻转。EnemySkillManager 新增 _castDelay 字段（Inspector 可调），TryCastSkill 拆分为 StartCooldown + 协程 DelayedExecute——先开始冷却+播动画，延迟 _castDelay 秒后才 ExecuteEffect（技能效果），实现动画与技能释放同步。SkillInstance.TryCast 拆分为 StartCooldown() + ExecuteEffect() 两个公开方法。
- [2026-08-14 21:23:16] 修复 sprite sheet 朝向的镜像技巧（Unity Texture2D）：对每个格子内部做水平镜像——只翻转格内像素、不交换格子位置，即可让整张贴图镜像朝向，同时保持帧播放顺序、.meta 里每帧切割坐标、.anim 的 sprite 引用（internalID）全部不变。放线菌 idle/cast 贴图原本朝左，与项目 flipX"默认朝右"约定相反（导致背对玩家、施法光束反向），已用此法镜像为朝右。适用其他 AI 生成的朝左素材。
- [2026-08-14 21:59:02] 被动光环类技能（出生即生效的持续效果）实现模式：SkillData 新增 public bool passive 字段；EnemySkillManager 新增 AutoCastPassives(ISkillCaster) 遍历 passive 技能直接 ExecuteEffect（不进入冷却、不触发施法动画）；EnemyCore.Awake 在 InitializeFromLibrary 之后调用 AutoCastPassives(this)。中性粒细胞净化光环(neutrophil_aura)是首个用例：ApplyBuffSkillEffect + BuffData + NeutrophilAuraBuff(BuffEffectBase) 组合。
- [2026-08-14 21:59:02] BuffInstance.Tick 中 isPermanent 会直接 return，导致永久 buff（isPermanent=true）的 OnTick 永远不执行。所以需要持续 Tick 的效果（回血光环/净化光环等周期效果）必须用 isPermanent=false + 超长 duration（如 999999）而非 isPermanent=true，否则 OnTick 不会运行。若要真正支持永久 Tick，需改 BuffInstance.Tick 让永久 buff 也调用 OnTick（只跳过减时间）。
- [2026-08-14 22:45:13] PurifyWave shader（Custom/PurifyWave）不读 vertex color（_sr.color），用 SpriteRenderer.color.a 做淡出无效——必须操作材质实例的 _Opacity 参数淡出。做"扩散环淡出"类特效时，PurifyAnimator 应在 Run() 里缓存 _mat.GetFloat("_Opacity") 基准值，Update 里 _mat.SetFloat("_Opacity", Lerp(base,0,t))。白 sprite PPU=1 时 sprite scale 直接等于世界尺寸，扩散环要可见至少 scale 1→2.8。
- [2026-08-14 23:29:02] 扩展工具 generate_sprite_sequence（TJGenerators 扩展）在当前环境不可靠：提交后任务反复卡死在 "recovering" 状态（progress 0），无任何 Assets/TJGenerators/History 产出，且提交后任何编译/AssetDatabase 操作触发 domain reload 会进一步丢失任务（单球菌/中性粒细胞/双球菌多个任务全卡死）。可靠替代方案：用 MCP 工具 generate_sprite_animation 生成 4×4 共 16 帧 sprite sheet（frontier 模型，返回 task_id，用 check_task 每 10s 轮询），再走项目既有的"切割成帧 + AnimationClip + AnimatorController"流程。放线菌/单球菌的 idle_spritesheet 即来自此方式。
- [2026-08-15 19:44:00] 敌人固定技能装配机制（链球菌首个用例）：EnemyConfig 新增 List<string> fixedSkillIds；EnemyCore.Awake 中若 fixedSkillIds 非空则用 SkillManager.LoadSkills(library, ids) 精确装配，否则走 InitializeFromLibrary 随机抽取。用于需要主动技能+被动技能组合的敌人（如链球菌的周期电击+死亡分裂），避免随机抽取导致技能不全。LoadSkills 按传入 skillId 顺序装配，AttackState 只主动施放 index 0 的技能。
- [2026-08-15 19:44:05] 泛用电击范围技能 ElectricNovaSkillEffect（Effects/，参数驱动：radius/arcMaterial/arcJitter/arcColor/hitGlow），复用 ChainLightningArcRuntime + ChainLightningHitGlowRuntime 泛用视觉组件，从施法者向每个范围内敌人放射锯齿闪电弧+命中辉光，纯瞬间 AoE 不依赖投射物。泛用死亡分裂亡语 ExplodeSplitBuff（BuffEffectBase，SpawnGroup=prefab+count 列表，可爆多组敌人），死亡瞬间按组 Instantiate，比 DiplococcusDeathBuff 更泛用。两者搭配 fixedSkillIds 实现链球菌周期电击+死亡爆出单球菌/双球菌。
- [2026-08-15 20:23:50] 复用已有 shader 做"换色"特效时务必检查 frag 里硬编码的颜色。GroundSolution.shader（高渗裂解剂紫色溶液）的气泡颜色硬编码为 float3(1.0,0.8,1.0) 粉紫色，直接复用做"绿色毒液"会出现绿液+粉气泡的颜色冲突。解法：新建 PoisonLiquid.shader（复制 GroundSolution 逻辑，把气泡颜色抽成可配置 _BubbleColor 属性，默认黄绿色），不改原 GroundSolution.shader。葡萄球菌毒液即此方案。
- [2026-08-15 20:23:55] PoisonGasZone（持续伤害区域组件）的 prefab 建议在创建时就用持久 sprite 填上（如 Assets/white_sprite.png），否则 SpriteRenderer.sprite 为 null，虽然运行时 Start() 会动态生成白 sprite，但 Editor 非 Play 模式下 capture_asset 预览和场景视图不可见，排查时容易误判"特效没显示"。双球菌毒气 prefab 目前仍是 sprite=null（可补）。
- [2026-08-15 20:46:48] sprite sheet 裁剪居中时 cell 尺寸不能固定：圆形/方形敌人可用 200，但横向长条状敌人（如大肠杆菌杆状，内容宽 209~224 远大于高 75~83）会被截断。正确做法是 newCell = max(所有帧内容宽, 内容高) + padding，或直接保留原始 cell（256）。处理前先扫描每帧内容边界（maxW/maxH），据此决定 newCell，别写死。大肠杆菌即因 newCell=200 < 内容宽 224 导致横向截断。
- [2026-08-15 21:48:45] 轴向喷射型敌人（结核分枝杆菌）实现模式：EnemyConfig 新增 useAxialSpray(bool) + axialAlignThreshold(float) 字段；新建 AxialSprayState（同行|dy|或同列|dx|小于阈值→TryCastSkill喷射，喷射期间用 _sawMovementDisabled 标志判断 TorrentRuntime 是否禁用 EnemyMovement 来锁定移动，前摇期间手动 Stop()，EnemyMovement 从 disabled 恢复到 enabled 即喷射结束→切回 ChaseState）；ChaseState 加轴向检测分支。喷射方向由 TorrentRuntime 敌人阵营逻辑自动朝向玩家，玩家在同行/同列时方向自然纯水平/垂直，无需改 TorrentRuntime。
- [2026-08-15 21:48:50] EnemyCore 的 OnSkillCast 无条件 _animator.SetBool("IsCasting") 会在 AnimatorController 无该参数时报 "Parameter 'IsCasting' does not exist"（链球菌/结核等有主动技能但无 cast 动画的敌人）。已修复：加 HasAnimatorParameter(string) 辅助方法，SetBool 前检查 _animator.parameters 是否含该参数名，ResetCastBool 同样检查。所有无 cast 动画的主动技能敌人受益。
- [2026-08-15 21:48:56] TorrentSkillEffect 字段全是 [SerializeField] private（_duration/_range/_spreadAngle/_damagePerSecond/_damageInterval/_knockbackSpeed/_torrentMaterial），脚本创建资产时需用反射 GetField(...).SetValue 设置。结核"较细溶栓"配置：spreadAngle=15（窄）、duration=8、knockbackSpeed=0（喷射不移动）、cooldown=12（前后摇长），复用 CorrosiveTorrent.mat。
- [2026-08-15 22:58:19] 敌人"沿自身形象多方向固定喷射"的实现（结核分枝杆菌首个用例）：TorrentSkillEffect/TorrentRuntime 是单方向且 Execute 里 FindObjectOfType<TorrentRuntime> 全局唯一（多实例冲突），不适用于多方向。需新建 TriSpraySkillEffect + TriSprayRuntime，运行时持 static float[] Angles（结核 Y 字形三叶：上90°/左下210°/右下330°），每束独立扇形判定 + HashSet<Collider2D> 去重。喷射期间禁用 EnemyMovement 锁移动，用 FindObjectOfType<TriSprayRuntime>().IsActive 检测喷射是否结束（而非检测 EnemyMovement.enabled 的 hack）。EnemyConfig.useAxialSpray 语义已从"同行/同列轴向"改为"进入攻击范围即喷射"。EnemyCore 已加 HasAnimatorParameter 保护，避免无 IsCasting 参数的 controller 报错。
- [2026-08-16 19:38:18] 噬菌体机制实现（全局死亡概率生成 + 无视地形追踪）：BacteriophageSpawner 用 [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] 全局自动注册 + DontDestroyOnLoad，监听 EnemyCore.OnAnyEnemyDied，普通/精英怪死亡时按概率在尸体位置 Instantiate 噬菌体。排除 Boss（GetComponent<BossCore>()）和噬菌体自身（config.displayName=="噬菌体"）防无限繁殖。配置通过 Resources.Load<BacteriophageSpawnConfig>("BacteriophageSpawnConfig") 运行时加载。噬菌体 prefab 的 CircleCollider2D.isTrigger=true 实现无视地形（穿过墙壁但仍触发 OnTriggerStay2D 接触伤害和被弹幕命中），chaseSpeed=5/detectionRange=20 实现快速追踪。
- [2026-08-17 14:03:02] 泛用亡语技能触发 buff：DeathSkillTriggerBuff（BuffEffectBase，Effects/），OnApply 订阅 EnemyCore.OnDied，死亡时执行任意 SkillEffectBase（通过 public SkillEffectBase deathSkillEffect 字段引用）。用于"死亡释放气浪/爆炸/击退"等机制，比 DiplococcusDeathBuff/ExplodeSplitBuff 更泛用（不限于生成怪物，可执行任意技能效果）。流感病毒首个用例：死亡时执行 WaveSkillEffect（360°气浪小幅击退无伤害）。被动技能链路：SkillData(passive) → ApplyBuffSkillEffect → BuffData(永久) → DeathSkillTriggerBuff → deathSkillEffect.Execute(caster, Vector2.up, 1f, ownerType)。注意：不能直接把 SkillEffectBase 当 passive 技能的 skillEffect（AutoCastPassives 在出生时立即执行，而非死亡时），必须通过亡语 buff 桥接。
- [2026-08-17 15:51:45] DeliriumTrigger 已扩展为泛用房间级谵妄触发体（不再只影响敌人）：新增 _affectPlayer（锁定玩家输入 N 秒，用 PlayerController.InputLocked + 协程解锁）+ _playerLockDuration + _killCasterAfterRelease（释放后 TakeDamage(99999) 杀死施法者）。脑神经元紊乱体首个用例：affectPlayer=true/playerLockDuration=3/killCasterAfterRelease=true，cooldown=999999（只释放一次），detectionRange=20（全房间感知）。注意 Tooltip 字符串里不能用中文引号""（会被 C# 解析器截断字符串报 CS1003），用普通引号或无引号。
- [2026-08-17 18:52:23] 角质增厚细胞周期护盾机制：AbsorbShield（ConcreteSkill/，伤害吸收组件，TryAbsorb(ref damage) 先按 damageReduction 免伤再从 ShieldHP 扣，耗尽触发 OnShieldBroken 事件）+ PeriodicShieldBuff（BuffEffectBase，周期性生成护盾 HP=MaxHealth×shieldRatio，破碎时 StunController 僵直 stunDuration 秒，恢复后 regenInterval 秒重生）。EnemyStats.TakeDamage 入口顺序：免疫→AbsorbShield吸收→镇痛拆分→扣血。护盾视觉复用 KeratinShield.mat（_HitFlash 闪白 + _Break 破碎裂纹）。CreateVisual 须用持久 sprite（Assets/white_sprite.png）而非运行时 CreateWhiteSprite（会丢失引用），AssetDatabase.LoadAssetAtPath 须用 #if UNITY_EDITOR 守卫。破碎时设 _Break=1+_HitFlash=1 后延迟 0.15s 销毁让动画可见。技能改为主动（passive=false, cooldown=8s），AttackState.TryCastSkill(0) 触发。
- [2026-08-17 20:10:20] 碰撞附加 debuff 机制实现：EnemyConfig 新增 public BuffData contactDebuff 字段；EnemyCore.ProcessContactDamage 在造成伤害后检查 config.contactDebuff，非空则给目标 ApplyBuff。细胞衰老病灶首个用例：触碰玩家 → 5伤害 + aging_debuff（攻击力降至30%，5s，Refresh）。新建 AgingDebuff（BuffEffectBase，OnApply 存 _originalAttack + SetStatValue("AttackStrength", val×0.3)，OnRemove 恢复）。注意：AgingDebuff 用 static 字段存原始值，多个实例同时存在会冲突（但 debuff 设为不可叠加+Refresh 所以实际只有1个实例）。
- [2026-08-17 20:41:15] 受击闪避机制实现（肌纤维应激细胞）：DodgeComponent（ConcreteSkill/ MonoBehaviour 组件，TryDodge(awayDirection) 触发 SlideRuntime.Activate 滑移 + DamageImmunity.GrantImmunity 无敌，带冷却计时）+ DodgeOnDamagedBuff（BuffEffectBase，OnApply AddComponent+Initialize，OnRemove Destroy）。EnemyStats.TakeDamage 入口顺序：无敌→受击闪避(成功则 return 免疫)→AbsorbShield→镇痛→扣血。闪避方向=远离玩家（PlayerManager.Instance.CurrentPlayer）。关键坑：闪避逻辑不能放 ScriptableObject 里用 GetComponent 查询（永远 null），必须用 MonoBehaviour 组件。肌纤维应激细胞配置：coccus_charge(主动冲撞, fixedSkillIds index0) + muscle_dodge(被动闪避, index1)。
- [2026-08-17 21:52:08] 角色"无输入自己滑动/左移"的根因：PlayerController.FixedUpdate 里 if(InputLocked) return 不清零 _rb.velocity，而 Player.prefab 的 Rigidbody2D.linearDrag=0（速度永不自然衰减）。任何击退/位移技能设置速度后，遇到 InputLocked（房间切换/眩晕/喷射锁定）就会无限滑动。修复：InputLocked 时强制 _rb.velocity=Vector2.zero 再 return。同类坑：所有"锁定移动"的入口（PlayerController.InputLocked / EnemyMovement.enabled=false / StunController）都必须保证速度清零。
- [2026-08-18 19:27:57] AI 生成敌人帧动画时若出现"风格完全偏离原图"（如黄绿色酸橙切片生物被生成成深紫肉瘤恐怖物体掉san），需重新生成并在 prompt 里显式约束："保持原图完全一致"+"列出原图关键特征（颜色/形状/细节）"+"不要改变颜色形状或添加恐怖元素"。生成后必须先用 analyze_multimedia 验证风格符合再走切割装配流程（防止白装配）。线粒体辐照畸变体即此案例。
- [2026-08-18 19:47:39] AI 生成 sprite sheet 帧动画"抽搐感"的根因与修复：AI 生成的 4x4 帧中某些帧（如神经凋亡信使帧5）内容尺寸/位置与其他帧严重不一致（帧5 高 229px vs 其他 170px，Y 从 0 贴顶），裁剪居中后主体大小/位置跳动，播放时帧间边界差异突增（帧4→帧5 达 70px，正常 4~17px）→ 抽搐。修复：①每帧裁剪到内容边界后统一缩放到所有帧内容尺寸的中位数，再居中到统一 cell（消除帧间大小/位置差异）；②降低 fps（12→8）进一步平滑；③重建 anim+controller+重挂 prefab（重切后 fileID 变化）。排查方法：扫描每帧内容边界，计算相邻帧边界差异（>30px 即异常帧）。
- [2026-08-18 21:19:38] [2026-08-18] execute_csharp_script 中创建 AnimationClip 不能用 ScriptableObject.CreateInstance&lt;AnimationClip&gt;()（CS0311：AnimationClip 不是 ScriptableObject），必须用 new AnimationClip()。同理 Texture2D 等非 ScriptableObject 子类也用 new。
- [2026-08-18 22:55:22] [2026-08-18] ScriptableObject 私有字段（如 ProjectileSkillEffect._projectilePrefab）用反射 SetValue + AssetDatabase.SaveAssets() 不持久化到磁盘——读回仍为 null。必须直接改 YAML 文件（replace 工具把 {fileID: 0} 替换为 {fileID: xxx, guid: xxx, type: 3}）后 AssetDatabase.Refresh(ForceUpdate)。同理 TextureImporter.spritesheet 通过 API 设值后 SaveAndReimport 会丢失名称（Identifier uniqueness violation），需直接写 .meta 文件（参照葡萄球菌 .meta 格式，含 unique spriteID + internalID + nameFileIdTable）。
- [2026-08-19 22:06:48] [2026-08-19] 随从槽位/等级系统（侵蚀同化扩展）：EnemyFollower 新增 SlotIndex（0..MaxFollowerCount-1）+ Level + UpgradeLevel()（同种随从升级=属性每级+15%（_levelStatFactor 可配）+ 技能等级+1，用户确认两者都有）+ static Revision 计数（UI 重建依据）+ GetSlotsInOrder()/SameType()（config 引用比较，空则 displayName）/AssimilateToSlot()（空槽放置/同种升级并 ConsumeAsAssimilated 消耗敌人/异种 DestroyImmediate 替换）。EnemyCore.Assimilate 加 slotIndex 重载；ConsumeAsAssimilated(transform) 触发 OnAssimilated（房间计数）但不建随从直接销毁。侵蚀流程：同化→FollowerSlotPopup（选槽，成功回调进化倾向-5）；吞噬→SkillStealPopup 选技能→SkillEquipSlotPopup 选装配槽（同技能 UpgradeSkill/异技能或空槽 EquipSkill）。UI 资产在 Assets/Prefabs/UI/Skill/（FollowerAvatarView/FollowerSlotView/SkillEquipSlotView 行项 + FollowerSlotPopup/SkillEquipSlotPopup 弹窗），FollowerPanel 持久面板在 UICanvas 根下（StatsPanel 下方 pos(20,-370)），构建脚本 Assets/Scripts/Skill/Editor/FollowerUIBuilder.cs（菜单 Tools/Follower UI/Build All）。
- [2026-08-19 22:06:56] [2026-08-19] 独立 Canvas 弹窗 prefab 保存坑（FollowerSlotPopup/SkillEquipSlotPopup 踩坑）：ScreenSpaceOverlay 根 Canvas 在无窗口/后台编辑环境下保存时根 RectTransform 被按 0 尺寸屏幕重驱动为 scale=0 size=0（CanvasScaler 有无都一样）。解决：①构建时 renderMode=WorldSpace（不被驱动），保存后 PowerShell 把 YAML 根 Canvas 的 m_RenderMode: 2 改回 0（ScreenSpaceOverlay，子画布运行时需要它才会被驱动铺满父画布）；②嵌套进 UICanvas.prefab 的实例，其 RectTransform override 必须存 stretch（anchorMin(0,0)-anchorMax(1,1) sizeDelta(0,0) pivot(0.5,0.5)），否则弹窗钉在父画布左下角（正常弹窗 SkillStealPopup 运行时即被驱动成 stretch）。另：Unity 已销毁对象 == null（operator 重载），DestroyImmediate 后判空需先缓存 bool（AssimilateToSlot 替换路径曾误返回 Placed）。
- [2026-08-19 22:52:17] [2026-08-19] TMP 中文乱码（方框字/整行空白）根因与修复：全局 fallback NotoSansSC-Regular SDF 资产 atlas[1] 纹理引用断裂（null）——运行时 TryAddCharacters 动态开的新 atlas 纹理未 AddObjectToAsset 持久化，重载后 null → TMP 渲染失败（字形在 atlas1 的文本 charCount=0 整行空白，字形在 atlas0 的才正常）。修复：删除旧资产，从 Assets/Codely/Fonts/NotoSansSC-Regular.otf 重建 TMP_FontAsset 为 4096x4096 单 atlas（CreateFontAsset 90,9,SDFAA,4096,4096,Dynamic,false），收集旧字符表（639字）+UI/技能名/敌人名（共~705字）一次性 TryAddCharacters 烘焙，material+atlas 用 AddObjectToAsset 持久化，重新加入 TMP_Settings.fallbackFontAssets。坑：2048x2048 单 atlas 放不下 705 字（SDFAA 90pt cell≈99px → 仅~400格），必须 4096x4096（~1681格）。
- [2026-08-19 22:52:22] [2026-08-19] UI 弹窗点击被遮挡的根因：嵌套在 UICanvas 下的子画布（Canvas 组件，sortingOrder=100）其 sortingOrder 被 Unity 忽略（子画布在父画布内按层级深度+sibling 顺序排序）——嵌套弹窗会被 UICanvas 更靠后的常驻 UI（如 ItemUI/ItemDetailPopup 拾取提示框，raycastTarget=true 常驻屏幕中央）遮挡点击。修复：弹窗 Show 时 transform.SetParent(null) 提升为根画布 + canvas.renderMode=ScreenSpaceOverlay（prefab 序列化为 WorldSpace 以规避无窗口保存归零，运行时改回）+ SetAsLastSibling —— 根画布 sortingOrder=100 才真正生效。配套：行项内子元素（TMP 文本/Icon）raycastTarget=false，让点击直接命中行根 Button 图形（否则 TMP 文本拦截射线且部分事件路径不冒泡到父级 Button）。
- [2026-08-19 23:32:24] [2026-08-19] 侵蚀吞噬→装配槽"无法替换"双根因：①SkillLibrary._allSkills 列表含 null 元素（主库 index25 是 NULL，敌人技能库正常）——GetById 的 Find 遍历到 null 抛 NullReferenceException，整个 EventSystem 点击处理崩溃（弹窗不关、技能不换、看似"点不动"）。修复：GetById 加 s!=null 防御 + Editor 脚本 SerializedObject 清理所有 SkillLibrary 的 null 元素。②玩家 SkillLibrary 没有的敌人专用技能（如双球菌 diplococcus_death）EquipSkill 静默失败——修复：SkillLibrary 新增 CreateSkillInstance(SkillData) 重载（委托绑定逻辑复用），PlayerSkillManager 新增 EquipSkillData(index, data)，SkillEquipSlotPopupManager 改传 SkillData（而非 skillId），OnSlotSelected 优先玩家库、失败则 EquipSkillData。同技能升级用 UpgradeSkill(index)（直接操作槽内实例，不依赖玩家库）。另：PlayerSkillManager.CastSkill 加 passive 检查（防按键重复触发被动亡语）。
- [2026-08-19 23:32:30] [2026-08-19] UI 行项"技能名与图标重叠"根因：TMP 文本用 TextAlignmentOptions.Left（左对齐）时文字从矩形左缘开始画，而 NewUI 默认 pivot=(0.5,0.5) 且 anchoredPosition 是"中心"坐标 → 矩形左缘 = centerX - width/2，可能是负值 → 文字画在左侧图标上方。修复：NameText 的 RectTransform.pivot 改 (0,0.5)（左缘对齐），anchoredPosition.x = 图标右缘 + 间隙。三个行项（SkillEquipSlotView/FollowerSlotView/FollowerAvatarView）同款修复。教训：左对齐 TMP + 中心 pivot = 必重叠。
- [2026-08-20 20:45:43] Boss 巨噬细胞实现（Boss 架构首个完整用例）：①Boss 血条=BossHealthBar（单例，BossCore.Start 自动实例化 prefab+DontDestroyOnLoad/OnHealthChanged 更新/OnDestroy 隐藏，Panel 锚定屏幕顶部居中 anchor(0.5,1) pos(0,-40) pivot(0.5,1)）；②Boss 张轴向嘴冲刺=BossLungeState（同行/同列检测→TryCastSkill 释放 SlideSkillEffect 冲撞，ChaseState 加 useBossLunge+BossCore 判定分支），EnemyConfig 新增 useBossLunge 字段，_castDelay=0.5s 让冲刺在动画张嘴帧才发动；③召唤小巨噬=SummonSkillEffect 复用；④Boss 预制体=克隆 Boss.prefab（EnemyCore+BossCore+EnemySkillManager manualTick=true）+换贴图+配 config+配 _phases；⑤Boss 房间 enemyPool 清空只留 Boss；⑥动画双 clip：idle（16帧8fps循环）+ lunge（16帧16fps非循环，AnimatorController IsCasting 参数 AnyState→Lunge→Idle exitTime 0.9 过渡）；⑦小巨噬细胞 scale=0.35（非0.8），贴图用 idle spritesheet 首帧（224px），collider radius=1.0 补偿缩放，帧动画 idle 16帧@8fps。

- [2026-08-20 20:26:19] [2026-08-20] SlideRuntime 新增 SetForcedDirection(Vector2) 机制：外部强制冲刺方向，Activate() 优先使用 _forcedDirection（若已设），否则走原有逻辑（玩家读输入/鼠标，敌人随机）。SlideSkillEffect.Execute 中对敌人阵营且 direction 非零时调 SetForcedDirection 传递方向，玩家阵营不变。BossLungeState 用此机制实现纯轴向冲刺：同列→(0,±1) 垂直，同行→(±1,0) 水平。此模式可用于任何需要外部指定冲刺方向的敌人技能。
- [2026-08-20 21:02:02] [2026-08-20] BossHealthBar 自动实例化机制 + 事件订阅修复：BossCore 新增 _bossHealthBarPrefab 字段，Start() 中若 BossHealthBar.Instance==null 则 Instantiate(prefab)+DontDestroyOnLoad。**关键修复**：删除 OnEnable/OnDisable 中的事件订阅（OnEnable 在 Awake 后、Start 前运行，此时 _health 可能为 null 导致订阅被跳过），改为在 Start 中**无条件**订阅 _health.OnHealthChanged（不依赖 BossHealthBar.Instance 是否存在）。OnDestroy 中取消订阅。LHJ.unity 场景中旧的独立 BossHealthBar GameObject（scale=0 残留）已删除。
- [2026-08-20 21:02:05] [2026-08-20] Boss 技能施放三路冲突修复：Boss lunge 技能（index 0）被三处代码路径触发导致动画不同步+方向错误。根因：①BossCore.TryCastSkill 直接调 ready.TryCast() 绕过 EnemySkillManager.TryCastSkill() → 无 OnSkillCast 事件（无动画）+ 无 _castDelay（立即执行）+ 方向非轴向；②AttackState.TryCastSkill(0, GetTargetDirection()) 用非轴向方向施放 lunge；③AttackState 不检测 BossLungeState 对齐 → Boss 进入 AttackState 后卡住不再 lunge。修复：①BossCore.TryCastSkill 改用 _skillManager.TryCastSkill(index,...)，新增 _stateManagedSkillIndices（默认{0}）跳过 lunge；②AttackState useBossLunge=true 时新增 BossLungeState 对齐检测 + 跳过技能施放（lunge 由 BossLungeState 管，summon 由 BossCore.Update 管）。教训：SkillInstance.TryCast() 绕过 EnemySkillManager.TryCastSkill() 会丢失 OnSkillCast 动画事件+_castDelay 延迟，Boss 技能施放必须统一走 EnemySkillManager.TryCastSkill()。
- [2026-08-20 22:00:47] [2026-08-20] Unity uGUI Filled Image 无 sprite 时 fillAmount 静默失效（Boss 血条/进化倾向槽同坑）：UGUI 源码 Image.cs OnPopulateMesh 中 activeSprite==null 时直接调 base 渲染完整矩形并 return，Type.Filled 分支（GenerateFilledSprite）被完全跳过——fillAmount 每帧都在更新但渲染永远满格，控制台无任何报错。修复：给 Filled Image 的 m_Sprite 赋白色 sprite（Assets/white_sprite.png，guid c563feae7ffbcf644a32f47d69a2e672，fileID 21300000），YAML 从 {fileID: 0} 改为 {fileID: 21300000, guid: c563feae7ffbcf644a32f47d69a2e672, type: 3}。已修复 BossHealthBar.prefab 的 Fill 与 UICanvas.prefab 的 StatsPanel/BarFill（进化倾向槽）。排查方法：选中 prefab 用 SerializedObject 查 _fillImage.objectReferenceValue.sprite 是否 null。新建任何 fillAmount 驱动的血条/槽 Image 都必须带 sprite。
- [2026-08-21 18:36:55] [2026-08-21] 音频系统接入约定：AudioManager（Assets/Scripts/Audio/，持久化单例，BeforeSceneLoad 自动创建 DontDestroyOnLoad，SFX 池 4 源 + BGM 源，三级音量，同名音效最小间隔 0.05s 防高频叠加）+ AudioClipLibrary（Resources/Audio/AudioClipLibrary.asset，名称→clip 映射，现 24 条 = 20 SFX + 4 BGM）。映射键约定：技能施法音效键 = skillId，特殊事件键 = skillId+后缀或事件名。发声时机：玩家施法在 CastSkill 成功处、敌人在 EnemySkillManager 效果执行帧（_castDelay 后与动画同步）、受击在 TakeDamage 实际扣血后（damage>0）；特殊挂载点：WaveSkillEffect/WaveRuntime._endSoundName（波消散，如 axon_block_end）、SlideSkillEffect/SlideRuntime._endSoundName/_wallSoundName（EndSlide(hitWall) 区分撞墙/自然结束）、PhotonBeam 落地播 photon_radiance_landing、PlayerStats/EnemyStats 播 player_hurt/enemy_hurt。注意：文件名与技能名不完全一致的映射——"线粒体生物光子辐射"→photon_radiance_landing（落地播非施法）、"包膜冲击爆发"→capsular_burst（词序不同）、muscle_dodge 显示名是"应激闪避"而"滑移应激"=slide_stress。BGM 系统（2026-08-21 同日新增）：BgmManager（Assets/Scripts/Audio/BgmManager.cs，持久化单例）管理，素材在 Assets/Music/（日常战斗_1/2.mp3→bgm_combat_1/2、boss_1/2.mp3→bgm_boss_1/2）；切换时机=RoomManager.OnPlayerEnter（Boss 房间→PlayBoss，其他→PlayCombat），Start/Result 场景加载时 _stopMusicScenes 停止音乐；组内随机不重复（_noRepeatMemory），当前组已播放则不打断。坑：_noRepeatMemory 若误声明为 float 会 CS0019/CS0029。

- [2026-08-21 13:22:27] [2026-08-21] 已知未修复问题：Play Mode 时控制台报 "The referenced script (Unknown) on this Behaviour is missing!"，由 CodexManager.Awake 加载图鉴资产（Resources.Load<CodexItemSO/EnemySO/SkillSO>）触发。已排查：CodexItemSO.asset 编辑器模式加载正常（54 条目无 null），非本次音频改动引入（未删除任何脚本）。疑似某图鉴条目/子资产引用了已删除脚本的 GUID。若用户询问该错误可直接定位到 Codex 图鉴系统。
- [2026-08-21 14:00:05] [2026-08-21] 音效引用断裂修复经验：用户替换/重导 Sounds 目录下的 wav（同名新文件，GUID 变化）会导致 AudioClipLibrary.asset 中对应条目 clip 引用断裂为 NULL（AssetDatabase.LoadAssetAtPath 读回仍显示条目存在但 clip=null，YAML 里是 {fileID: 0}）。修复=用 SerializedObject 遍历 sounds 列表找到对应 name 条目设 clip.objectReferenceValue（public List 字段可持久化，与私有字段需改 YAML 的坑不同）+ AssetDatabase.SaveAssets() + 读回验证 + 检查 YAML（{fileID: 8300000, guid: xxx, type: 3} 为正常 AudioClip 引用）。本次修复 photon_radiance_landing => 线粒体生物光子辐射.wav（新 guid d27f6871119a65246b3ceb129a3716c7）。用户说"重新绑X的音效"时先查库中该技能名相关条目是否 NULL。
- [2026-08-21 18:36:55] [2026-08-21] 亡语技能"死亡时调用角色身上技能"模式（apoptosis_axon_block 修复）：DeathSkillTriggerBuff 新增 public string deathSkillId 字段——死亡时优先从角色 EnemySkillManager.SkillInstances 按 skillId 找技能实例调 ExecuteEffect（不检查冷却，亡语不受冷却限制），找不到回退到原 deathSkillEffect 直接执行路径（向后兼容流感病毒等旧亡语）。神经凋亡信使配置：fixedSkillIds=[axon_block, apoptosis_axon_block]（index0 主动给 AttackState 施放、index1 被动亡语），专属库神经凋亡信使SkillLibrary 需含 axon_block（原先只有被动亡语技能导致角色身上无主动技能）。验证方式：Play 模式实例化 prefab 查 SkillInstances + HasBuff，触发死亡后 FindObjectOfType<WaveRuntime> 确认波已释放。此模式适用于任何"死亡释放角色已装配技能"的需求。
- [2026-08-21 18:46:31] [2026-08-21] "被动技能出生即死"类 bug 排查经验：所有被动技能会被 EnemyCore.Awake 的 AutoCastPassives 在出生时立即执行，若被动技能效果是"立即致命/破坏性"（如挂 instant_death buff → OnApply TakeDamage(99999)）会导致敌人出生即死。脑神经元紊乱体即此案例：neuron_death（被动即死）在 Awake 触发，导致全房间谵妄（neuron_delirium 主动技能）从未释放过。修复：从 fixedSkillIds 和专属库移除该被动，删除相关资产（NeuronDeath/InstantDeathApplyEffect/InstantDeathBuff/InstantDeathBuffEffect，确认无残留引用后删）；即死已由 DeliriumTrigger.killCasterAfterRelease=true 实现。经验：配置被动技能时必须检查其效果是否"出生即安全"；检查其他敌人类似问题时用脚本遍历所有 EnemyConfig.fixedSkillIds + 所有 passive 技能效果链路。另：脑神经元紊乱体专属库/配置修复后，AttackState 只施放 index0（neuron_delirium），行为验证=Play 实例化查 IsDead 应为 False。

### Reference
- [2026-08-05 15:04:45] 项目经验知识库位于 .codely-cli/experience/：development/（bug 修复经验，命名 YYYYMMDD_Unity[问题].md）和 templates/（可复用代码模板）。2026-08-05 首次建立，含 URP 2D 自定义 shader 需 Universal2D pass、LineRenderer shader uv 陷阱、MoveAsset 保留 GUID、程序化闪电弧模板等 8 篇。unity-game-debugger 调试时优先检索该目录。
- [2026-08-18 21:19:35] 11+ enemies in Prefabs/Enemy/, configs in Resources/Enemy/EnemyConfig_*.asset, room pools updated. Latest: 霍乱弧菌 (cholera_projectile, ProjectileSkillEffect count=1).

