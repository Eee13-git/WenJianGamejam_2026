# ShaderGraph AI — Extension

## 概述

本扩展提供 **5 个技能**，覆盖 Unity ShaderGraph 编辑的核心工作流：节点/属性操作、Custom Function 编写、HLSL 文件创建、ShaderToy 转换、URP 扩展工具。

工具代码（C# MCPTools）打包为 UPM 包 `cn.tuanjie.shadergraph-mcptools`，通过 Codely Bridge 的 TCP 通道与 AI 通信。技能提供行为指导和编写规范。

## 架构

```
用户（自然语言）
      │
AI Agent ── 加载 Skill ── 获取行为指导
      │
      ├─ 调用 execute_custom_tool（Codely CLI 内置工具）
      │     └─ tool_name: "shader_graph_get_info" 等
      │
Codely Bridge（TCP Server，运行在 Unity Editor 内）
      │
cn.tuanjie.shadergraph-mcptools（C# 工具）── 操作 ShaderGraph
```

MCPTools C# 代码通过 `[ExecuteCustomTool.CustomTool]` 属性注册到 Codely Bridge 的 `ExecuteCustomTool` 系统，不是独立 MCP Server。

## 环境要求

| 依赖 | 版本 |
|------|------|
| Unity / Tuanjie | 2022.3+ |
| ShaderGraph | 14.x |
| HDRP（可选） | 14.x |
| URP（可选） | 14.x |
| Codely Bridge | 1.0.66+ |
| cn.tuanjie.shadergraph-mcptools | 通过 Package Manager 安装 |

## 触发场景速查

| 用户说什么 | 入口 Skill |
|---|---|
| 操作 ShaderGraph 节点/属性/Inspector | `shadergraph-mcp-tools` |
| 创建 Custom Function / 写噪声函数 | `custom-function-creator` |
| 写 HLSL 文件 / FBM / 复杂算法 | `hlsl-file-creator` |
| ShaderToy 转换 / GLSL 转 HLSL | `shadertoy-to-shadergraph` |
| URP Keywords / Passes / Variants | `shadergraph-urp-tools` |

## 共享严重规则（所有 Skill 必须遵守）

### 1. 先验真，再启用带管线前提的 skill

- 先查 `Packages/manifest.json`、渲染管线资产/设置、以及实际实现文件。
- 没确认是 URP / HDRP / Builtin 和对应技术链路前，不得启用任何带管线前提的 skill，也不得假定当前 ShaderGraph 属于某条特定渲染链路。
- `unity_workflow`、Inspector、Graph Target、材质/Renderer 观察这类单点状态，只能当作"需要进一步核实"的信号，不能单独作为判定项目管线的依据。

### 2. 绝对禁止直接编辑 `.shadergraph` 文件

- **永远不要、永远不要、永远不要**通过 `read_file` + `write_file`/`replace` 去手改 ShaderGraph 序列化文本。
- 只能通过 ShaderGraph MCP 工具、Unity 官方/反射 API、或其他明确受支持的编辑入口操作 ShaderGraph。

### 3. MCP 工具失败时必须停止并回问用户

- 如果 MCP 工具、Unity API、反射入口无法可靠完成目标，**必须立即停止继续实现**。
- 停止后必须**直接回问用户**，明确说明：
  - 当前为什么无法安全实现
  - 具体卡在哪个支持路径上
  - 继续强行修改会带来什么风险

### 4. 严禁退回到手改 JSON 方案

- 即使只改一个值、一个节点、一个 Block，也不允许为了"先做出来"而退回到手改 JSON。
- 如果发现自己已经开始走向 JSON 直改路径，必须立即中止，向用户承认当前路径不安全。

## Skill 列表

| # | Skill 名称 | 管线 | 用途 |
|---|---|---|---|
| 1 | `shadergraph-mcp-tools` | HDRP/URP/Built-in | MCP 工具完整参考（节点/属性/Inspector/Material） |
| 2 | `custom-function-creator` | 通用 | Custom Function 编写规范（String 模式） |
| 3 | `hlsl-file-creator` | 通用 | HLSL Include 文件创建规范（File 模式） |
| 4 | `shadertoy-to-shadergraph` | 通用 | ShaderToy GLSL → ShaderGraph HLSL 转换 |
| 5 | `shadergraph-urp-tools` | URP（团结引擎） | URP SubTarget 的 Keywords/Passes/Variants 操作 |

## 组合工作流

### 场景：ShaderToy 转 HDRP 透明 Shader

```
1. [shadertoy-to-shadergraph] 分析代码 → 转换为 HLSL
2. [hlsl-file-creator]            复杂逻辑写入 .hlsl 文件
3. [shadergraph-mcp-tools]        创建 ShaderGraph + Custom Function 节点
4. [shadergraph-mcp-tools]        设置 Surface Type = Transparent
5. [shadergraph-mcp-tools]        连线 + 保存
```

### 场景：从零创建带自定义 HLSL 的 ShaderGraph

```
1. [hlsl-file-creator]            编写 .hlsl 文件（噪声、算法等）
2. [shadergraph-mcp-tools]        创建 ShaderGraph
3. [shadergraph-mcp-tools]        创建 Custom Function 节点（File 模式）
4. [shadergraph-mcp-tools]        创建属性 + 节点 + 连线
5. [shadergraph-mcp-tools]        设置 Inspector（Surface / Lit / Advanced）
6. [shadergraph-mcp-tools]        验证错误 + 保存
```

## 文件结构

```text
shadergraph-ai/
├── gemini-extension.json                    # 扩展元信息
├── manifest.json                            # UPM 依赖清单
├── CODELY.md                                # 本文件
├── README.md                                # 人类可读文档
├── Packages/
│   └── cn.tuanjie.shadergraph-mcptools/     # UPM 包（C# MCPTools）
│       ├── package.json
│       └── Editor/
│           └── MCPTools/                    # C# 工具代码
│               ├── MCPTools.asmdef
│               ├── MCPtools.ShaderGraph.cs
│               ├── SGReflection.cs
│               ├── MCPtools.GraphSettings.cs
│               ├── MCPtools.Material.cs
│               └── TJBasicEX/
│                   └── MCPtools.ShaderGraph.TJURP.cs
└── skills/
    ├── shadergraph-mcp-tools/
    ├── custom-function-creator/
    ├── hlsl-file-creator/
    ├── shadertoy-to-shadergraph/
    └── shadergraph-urp-tools/
```
