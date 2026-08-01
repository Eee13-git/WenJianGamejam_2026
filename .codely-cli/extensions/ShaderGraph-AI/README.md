# ShaderGraph AI 使用说明

本扩展包含两部分，配合使用可实现「用自然语言操作 Unity ShaderGraph」：

| 组成 | 路径 | 角色 |
|---|---|---|
| **工具（UPM 包）** | `Packages/cn.tuanjie.shadergraph-mcptools/` | 运行在 Unity Editor 内的 C# 工具，通过 Codely Bridge 注册为 Custom Tool |
| **技能（Skill）** | `skills/` | 让 AI Agent 正确使用上述工具的行为指导和编写规范 |

---

## 架构

```
用户（自然语言）
      │  "帮我创建一个 HDRP Lit 的 ShaderGraph，设置成透明"
      ▼
AI Agent ── 加载 Skill ── 获取行为指导（工具用法、HLSL 规范等）
      │
      ├─ 调用 execute_custom_tool（Codely CLI 内置工具）
      │     └─ tool_name: "shader_graph_create_asset"
      │
Codely Bridge（TCP Server，运行在 Unity Editor 内）
      │
      └─ ExecuteCustomTool.HandleCommand
            └─ 反射查找 [CustomTool("shader_graph_create_asset")] 标记的方法
                  └─ MCPtools.ShaderGraph.cs::CreateAsset()
```

**关键**：MCPTools C# 代码不是独立 MCP Server，而是通过 `[ExecuteCustomTool.CustomTool]` 属性注册到 Codely Bridge 的 TCP 通道。

---

## 安装

两部分各自独立安装：**工具（UPM 包）** 装进 Unity 工程，**技能（Skill）** 装进 AI Agent 的技能目录。

### 1. 安装工具（UPM 包）到 Unity / 团结引擎工程

**方式 A：嵌入包（推荐）** — 把整个包目录拷进目标工程的 `Packages/` 下：

```bash
cp -R Packages/cn.tuanjie.shadergraph-mcptools <你的Unity工程>/Packages/
```

**方式 B：Package Manager UI** — 菜单 `Window/Package Manager` → 左上 `+` → `Add package from disk...` → 选中本包的 `package.json`。

> **前置依赖**：工程需已安装 Codely Bridge（`cn.tuanjie.codely.bridge` 1.0.66+）。如未安装，工具代码无法编译（`.asmdef` 引用了 `UnityTcp.Editor` 程序集）。
> **引擎要求**：Unity 2022.3+（兼容团结引擎 Tuanjie）。

### 2. 安装技能（Skill）到 AI Agent

把技能目录拷贝到 Agent 加载技能的目录（以 Codely CLI 为例）：

```bash
cp -R skills/* ~/.codely-cli/skills/
```

---

## 环境要求与 Skill 列表

详见 `CODELY.md`（也是 AI Agent 加载的上下文文件）。
