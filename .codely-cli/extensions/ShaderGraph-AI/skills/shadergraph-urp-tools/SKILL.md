---
name: shadergraph-urp-tools
description: URP ShaderGraph extension tools — operate URP SubTarget Keywords, Passes, and Removed Variants. Tuanjie engine only. Use for URP shader variants, keyword toggles, or pass management.
---

# URP ShaderGraph 团结引擎 扩展工具 Skill

## 前置条件

- **共享规则见 CODELY.md**（先验真管线、禁止手改 .shadergraph JSON、工具失败时停止并回问用户）
- 必须确认当前项目和当前目标图属于 URP 技术链路，并且是团结引擎版本
- 当前打开的 Shader Graph 必须有 URP Target（UniversalLitSubTarget / UniversalUnlitSubTarget）
- 工具定义在 `Editor/MCPTools/TJBasicEX/MCPtools.ShaderGraph.TJURP.cs`
- 底层使用 `SGReflection` 反射访问 URP SubTarget 的 `keywordsStatus`、`passStatus`、`m_RemovedVariants`、`m_RemovedPasses` 等内部字段

## 核心概念

### Keywords
URP SubTarget 预定义了一组 Potential Keywords（如 `_NORMALMAP`、`_RECEIVE_SHADOWS_OFF` 等），每个 keyword 有启用/禁用状态。启用 keyword 会生成对应的 Shader Variant。

### Passes
URP SubTarget 预定义了一组 Potential Passes（如 "ShadowCaster"、"DepthOnly"、"DepthNormals" 等），每个 pass 有启用/禁用状态。

### Removed Variants / Passes
被移除的 Variants 和 Passes 不会编译到最终 Shader 中，用于减少 Shader Variant 数量，优化构建大小。

---

## 工具清单

### shader_graph_urp_get_keywords

获取 URP SubTarget 的 Keywords 列表及启用/移除状态。

**参数：** 无

**返回示例：**
```json
{
  "success": true,
  "subTarget": "UniversalLitSubTarget",
  "keywordCount": 15,
  "keywords": [
    { "name": "_NORMALMAP", "enabled": true, "removed": false },
    { "name": "_RECEIVE_SHADOWS_OFF", "enabled": false, "removed": false }
  ]
}
```

---

### shader_graph_urp_set_keywords

设置 URP SubTarget 的 Keywords 启用/禁用状态。

**参数：**
- `enableKeywords` (string[], 可选) — 要启用的 keyword 名称列表
- `disableKeywords` (string[], 可选) — 要禁用的 keyword 名称列表

**示例：**
```json
{
  "enableKeywords": ["_NORMALMAP", "_PARALLAXMAP"],
  "disableKeywords": ["_RECEIVE_SHADOWS_OFF"]
}
```

**返回：** `changes[]` — 实际修改的 keyword 列表

---

### shader_graph_urp_get_passes

获取 URP SubTarget 的 Passes 列表及启用/移除状态。

**参数：** 无

**返回示例：**
```json
{
  "success": true,
  "subTarget": "UniversalLitSubTarget",
  "passCount": 8,
  "passes": [
    { "name": "ShadowCaster", "enabled": true, "removed": false },
    { "name": "DepthOnly", "enabled": true, "removed": false },
    { "name": "DepthNormals", "enabled": false, "removed": true }
  ]
}
```

---

### shader_graph_urp_set_passes

设置 URP SubTarget 的 Passes 启用/禁用状态。

**参数：**
- `enablePasses` (string[], 可选) — 要启用的 pass 名称列表
- `disablePasses` (string[], 可选) — 要禁用的 pass 名称列表

**示例：**
```json
{
  "enablePasses": ["DepthNormals"],
  "disablePasses": ["ShadowCaster"]
}
```

**返回：** `changes[]` — 实际修改的 pass 列表

---

### shader_graph_urp_get_removed_variants

获取 URP SubTarget 被移除的 Variants（Keywords）和 Passes 列表。

**参数：** 无

**返回：** `removedVariants[]` / `removedPasses[]` / `removedVariantCount` / `removedPassCount`

---

### shader_graph_urp_set_removed_variants

设置 URP SubTarget 被移除的 Variants 和 Passes。

**参数：**
- `addRemovedVariants` (string[], 可选) — 添加到移除列表的 variant 名称
- `removeRemovedVariants` (string[], 可选) — 从移除列表中恢复的 variant 名称
- `addRemovedPasses` (string[], 可选) — 添加到移除列表的 pass 名称
- `removeRemovedPasses` (string[], 可选) — 从移除列表中恢复的 pass 名称

**示例：**
```json
{
  "addRemovedVariants": ["_SCREEN_SPACE_OCCLUSION"],
  "removeRemovedPasses": ["DepthNormals"]
}
```

**返回：** `changes[]` — 所有变更项

---

## 典型工作流

### 优化 Shader Variant 数量

```
1. shader_graph_urp_get_keywords → 查看所有 keywords
2. shader_graph_urp_set_keywords → 禁用不需要的 keywords
3. shader_graph_urp_get_passes → 查看所有 passes
4. shader_graph_urp_set_removed_variants → 移除不需要的 variants/passes
5. shader_graph_save → 保存
```

### 启用特定功能

```
1. shader_graph_urp_get_keywords → 确认当前状态
2. shader_graph_urp_set_keywords → 启用需要的 keyword（如 _NORMALMAP）
3. shader_graph_get_errors → 验证无错误
4. shader_graph_save → 保存
```

---

## 注意事项

1. **Keywords 名称区分大小写** — 必须使用精确的 keyword 名称（如 `_NORMALMAP`，不是 `_NormalMap`）
2. **Removed ≠ Disabled** — `removed` 表示从编译中移除（不生成 variant），`enabled` 表示 keyword 开关状态
3. **Pass 依赖** — 某些 Pass 可能依赖特定的 Keyword，移除前需确认依赖关系
4. **SubTarget 差异** — Lit 和 Unlit SubTarget 有不同的 PotentialKeywords/PotentialPasses 列表
5. **变更后需保存** — 修改 Keywords/Passes 后需要调用 `shader_graph_save` 保存变更
