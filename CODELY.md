# WenJianGamejam_2026 — 团结引擎 2D Roguelike 地牢游戏

## 项目概述

基于 **Unity 2022.3.62f3c1（团结引擎）** 开发的 2D 俯视角 Roguelike 地牢游戏，用于 2026 年文简 Gamejam。

**核心技术栈:**
- 引擎: Unity 2022.3.62f3c1 (Tuanjie)
- 渲染管线: Built-in Render Pipeline（未使用 URP/HDRP）
- 语言: C#
- 2D 物理: Rigidbody2D + Collider2D
- UI: uGUI + TextMeshPro
- 扩展: Codely Bridge + TJGenerators（AI 资产生成）+ ShaderGraph MCPTools

---

## 目录结构

```
Assets/
├── Prefab/           # 核心预制体：Player, Enemy, Boss, Bullet
├── Prefabs/          # 场景预制体：Rooms (各种房间类型), UI
├── Resources/        # 运行时加载资产：Enemy/Skills/Map Configs
├── Scenes/           # 场景文件
├── Scripts/          # 所有 C# 脚本（按模块组织）
│   ├── Bullet/       # 投射物系统 (Projectile)
│   ├── Core/         # 核心管理器 (PlayerManager 持久化单例)
│   ├── Editor/       # 编辑器扩展脚本
│   ├── Enemy/        # 敌人系统
│   │   ├── Core/     # EnemyCore（聚合门面）
│   │   ├── Behaviors/# 攻击行为 (IAttackBehavior, MeleeAttack, RangedAttack)
│   │   ├── Components/# 组件 (Health, Movement, SkillManager, Follower)
│   │   └── States/   # 状态机状态 (Idle, Patrol, Chase, Attack, Search, Dead)
│   ├── Interface/    # 核心接口 (IDamageable, IHealable)
│   ├── Item/         # 道具系统 (待开发)
│   ├── Map/          # 地图系统
│   │   ├── Config/   # 地图/Room/敌人生成/SO 配置
│   │   ├── Core/     # 地图生成核心 (Graph, Node, Generator, Door)
│   │   ├── Runtime/  # 运行时 (RoomManager, RoomPortal, RoomRoot, Spawners)
│   │   ├── Camera/   # 房间摄像机控制器
│   │   └── Room/     # 房间类型枚举
│   ├── Player/       # 玩家系统 (Controller, Combat, Stats)
│   ├── Skill/        # 技能系统
│   │   ├── ConcreteSkill/ # 具体技能实现 (ErodeProjectile)
│   │   └── Effects/  # 技能效果策略 (AoE, Dash, Heal, Projectile)
│   └── UI/           # UI 系统
│       ├── Map/      # 小地图
│       └── Skill/    # 技能 UI (面板, 槽位, 偷取弹窗, 侵蚀选择)
├── Textures/         # 纹理资产
├── TJGenerators/     # AI 生成资产历史记录
└── TextMesh Pro/     # TMP 资源
```

---

## 核心架构

### 玩家系统
- **PlayerController**: WASD 移动 + 鼠标左键攻击输入，通过事件委托解耦战斗逻辑。支持 `InputLocked` 属性（房间切换期间冻结输入）。
- **PlayerCombat**: 接收攻击事件，生成子弹（Projectile）朝鼠标方向射击，有冷却时间控制。
- **PlayerStats**: 玩家数值唯一数据源（血量/移速/攻击力/子弹速度/冷却），实现 `IDamageable` 和 `IHealable`。通过 SerializeField 暴露所有参数。
- **PlayerManager**: 持久化单例，`[RuntimeInitializeOnLoadMethod]` 在场景加载前自动创建，通过 Tag="Player" 发现/管理玩家跨场景保留。

### 敌人系统
- **EnemyCore**（聚合门面）: 实现 `IEnemy`/`IDamageable`/`ISkillCaster`，自动收集并初始化子组件（Health, Movement, SkillManager, StateMachine, AttackBehavior）。单一数据源：EnemyConfig(ScriptableObject) → EnemyCore → 各组件。
- **EnemyStateMachine**: 轻量状态机，管理状态切换和每帧 Tick。
- **状态列表**: Idle → Patrol → Chase → Attack → Search → Dead
- **攻击行为**（策略模式）: `IAttackBehavior` 接口 + `MeleeAttack`/`RangedAttack`
- **随从系统**（EnemyFollower）: 通过同化机制将敌人转化为跟随玩家的随从，有独立 AI（跟随+搜索+攻击），与 `MapManager` 跨房间传送协作。

### 技能系统
- **SkillData** (ScriptableObject): 技能数据资产（ID/名称/冷却/伤害系数/等级上限/效果策略）。
- **SkillInstance**: 运行时技能实例，管理等级、冷却 Tick、伤害系数计算。通过 `OnExecute` 委托注入效果。
- **SkillEffectBase**（策略模式）: `ProjectileSkillEffect`/`AoESkillEffect`/`DashSkillEffect`/`HealSkillEffect`。
- **SkillLibrary** (ScriptableObject): 唯一工厂入口，通过 `skillId` 创建完整配置的 SkillInstance。
- **ISkillCaster**: 统一玩家与敌人的技能施放接口。
- **PlayerSkillManager**: 管理 4 个槽位 (Q/F/E/R)，处理按键绑定、技能获取/升级/扩展。

### 地图系统
- **MapManager** (单例): 地图核心管理器，负责生成房间图、实例化房间、切换房间、传送玩家+随从。
- **MapConfig** (ScriptableObject): 全局地牢参数（种子、各类型房间数量、房间预制体池）。
- **MapGenerator**: 根据 RoomGraph 生成程序化地图，支持多种房间类型：
  - Start（起始）/ Normal（战斗）/ Boss / Shop / Treasure / Hidden / Exit
- **RoomManager**: 单房间运行时管理（敌人生成、玩家进入/离开回调）。
- **RoomPortal**: 门组件，支持普通门/可破坏隐藏墙壁/伪装墙壁。
- **RoomCameraController**: 房间间平滑过渡摄像机。

### 投射物系统
- **Projectile**: 通用子弹，支持 Player/Enemy 阵营 (`OwnerType`)，碰撞检测（Obstacles/Player/Enemy 标签）。

---

## 关键接口

| 接口 | 位置 | 用途 |
|------|------|------|
| `IDamageable` | `Scripts/Interface/Interface.cs` | 统一伤害接收（TakeDamage） |
| `IHealable` | `Scripts/Interface/Interface.cs` | 统一治疗接收（Heal） |
| `ISkillCaster` | `Scripts/Skill/ISkillCaster.cs` | 统一技能施放（玩家+敌人） |
| `IEnemy` | `Scripts/Enemy/IEnemy.cs` | 敌人对外统一接口 |

---

## 依赖包 (Packages/manifest.json)

### 核心 Unity 包
- `com.unity.feature.2d` — 2D 功能集
- `com.unity.textmeshpro` — 文字渲染
- `com.unity.ugui` — uGUI UI 系统
- `com.unity.visualscripting` — 可视化编程
- `com.unity.timeline` — 时间线

### Codely 扩展
- `cn.tuanjie.codely.bridge` (1.0.74) — Codely Unity Bridge
- `cn.tuanjie.ai.generators` — TJGenerators AI 资产生成（file: 本地包）
- `cn.tuanjie.shadergraph-mcptools` — ShaderGraph MCP 工具（file: 本地包）

---

## 开发约定

### 命名规范
- **接口**: `I` 前缀 (IDamageable, ISkillCaster, IEnemy, IHealable)
- **ScriptableObject**: `Config`/`Data`/`Library` 后缀 (EnemyConfig, SkillData, SkillLibrary)
- **组件**: 功能描述 + 角色后缀 (EnemyHealth, EnemyMovement, PlayerController)
- **目录**: PascalCase，按功能模块分层 (Scripts/Enemy/Core, Scripts/Skill/Effects)

### 架构模式
- **单例**: 全局管理器使用 MonoBehaviour 单例（MapManager, PlayerManager）
- **事件驱动**: 使用 C# event/Action 委托解耦（OnAttackInput, OnHealthChanged, OnRoomSwitchStarted 等）
- **策略模式**: 攻击行为 (IAttackBehavior)、技能效果 (SkillEffectBase)
- **状态机**: EnemyStateMachine 管理敌人 AI 状态
- **ScriptableObject 数据驱动**: 使用 SO 作为配置/数据容器（MapConfig, EnemyConfig, SkillData, SkillLibrary）
- **聚合门面**: EnemyCore 聚合子组件并统一对外接口

### 代码风格
- 中英混合注释（核心说明用中文，技术术语保留英文）
- `[Header]` 特性标注 Inspector 分组
- `[Tooltip]` 特性提供字段说明
- `#if UNITY_EDITOR` 包裹调试日志
- `[ContextMenu]` 暴露编辑器调试入口

---

## 常用开发流程

### 编译验证
```
使用 unity_workflow { action: "compile_and_validate" }
```
或手动：`unity_editor.start_compilation_pipeline` → `wait_for_compile` → `unity_console.get`

### 场景
- `LHJ.unity` — 主游戏场景
- `SampleScene.unity` — Unity 默认场景
- `yang.unity`, `yang_1.unity` — 开发者测试场景

### 编辑器工具链
- **Codely Bridge** 连接 Unity Editor 进行 AI 辅助开发
- **TJGenerators** 提供 AI 资产生成（模型/图片/材质/音效/视频等）
- **ShaderGraph-AI** 提供 ShaderGraph 编辑能力（当前项目使用 Built-in RP，ShaderGraph 工具链已就绪但非主要使用）

## Codely Structured Memories

### User
- [2026-08-01 22:51:25] 用户偏好使用中文交流（简体中文），要求 agent 也用中文回复。
### Feedback

### Project

### Reference

