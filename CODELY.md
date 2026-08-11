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

### Reference
- [2026-08-05 15:04:45] 项目经验知识库位于 .codely-cli/experience/：development/（bug 修复经验，命名 YYYYMMDD_Unity[问题].md）和 templates/（可复用代码模板）。2026-08-05 首次建立，含 URP 2D 自定义 shader 需 Universal2D pass、LineRenderer shader uv 陷阱、MoveAsset 保留 GUID、程序化闪电弧模板等 8 篇。unity-game-debugger 调试时优先检索该目录。
- [2026-08-12 00:20:28] 10 enemies in Prefabs/Enemy/, configs in Resources/Enemy/EnemyConfig_*.asset, room pools updated.
