---
name: shadergraph-mcp-tools
description: Complete ShaderGraph MCP tool reference — node/property operations, Graph Inspector settings (Surface/Lit/Advanced), and Material operations. Use when manipulating ShaderGraph, creating shaders, setting surface options, or managing materials.
---

# ShaderGraph MCP 工具集

## 前置条件
- 通过 Package Manager 安装 `cn.tuanjie.shadergraph-mcptools` 包（工具代码在 UPM 包的 `Editor/MCPTools/` 下）
- 必须在 Unity Editor 中打开一个 Shader Graph 窗口（.shadergraph 文件）
- 所有工具依赖窗口焦点状态，确保目标 Shader Graph 窗口处于激活状态
- 节点/属性工具定义在 `Editor/MCPTools/MCPtools.ShaderGraph.cs`，通过反射访问 ShaderGraph internal API
- 反射辅助类定义在 `Editor/MCPTools/SGReflection.cs`，提供所有 ShaderGraph 类型的懒加载缓存和反射操作
- Graph Inspector 工具定义在 `Editor/MCPTools/MCPtools.GraphSettings.cs`，通过反射访问 GraphData / HDTarget / URP Target / SubTarget 数据
- 团结引擎/Unity中国版的 URP 扩展工具定义在 `Editor/MCPTools/TJBasicEX/MCPtools.ShaderGraph.TJURP.cs`，操作 URP SubTarget 的 Keywords/Passes/Variants
- Material 工具定义在 `Editor/MCPTools/MCPtools.Material.cs`，操作材质属性、贴图、FBX 嵌入材质等

> **共享规则见 CODELY.md**（先验真管线、禁止手改 .shadergraph JSON、工具失败时停止并回问用户）

## 典型工作流

```
获取信息 → 创建属性 → 创建节点 → 获取端口 → 连接节点 → 修改参数 → 验证错误 → 保存
```

Graph Inspector 工作流：

```
get_graph_settings → get_surface_options → set_surface_options → get_lit_options → set_lit_options → refresh_inspector → save
```

---

## 一、信息获取工具

### shader_graph_get_info

**作用：** 获取当前打开的 Shader Graph 整体信息（路径、属性列表等）

**使用时机：** 开始操作前确认当前编辑的 Shader Graph 状态，检查已有属性

**参数：** 无

**返回：** 图标题、文件路径、属性数量、所有属性的 displayName/referenceName/type

---

### shader_graph_get_nodes

**作用：** 获取图中所有节点列表

**使用时机：** 需要了解图中已有的节点，或查找某个节点的 ID

**参数：** 无

**返回：** nodeCount、每个节点的 id/name/displayName

---

### shader_graph_get_node_slots

**作用：** 获取指定节点的所有输入/输出端口（Slot）信息

**使用时机：** 连接节点前必须获取端口 ID，确认可用端口

**参数：**
- `nodeId` (string, 可选) — 节点 ID
- `nodeName` (string, 可选) — 节点名称（与 nodeId 二选一）

**返回：** 节点的 inputSlots/outputSlots，每个包含 id/displayName/slotType

---

### shader_graph_get_node_details

**作用：** 获取节点的详细信息，包括类型特定的属性值

**使用时机：** 需要查看节点的具体参数值（如颜色值、向量值、Custom Function 配置等）

**参数：**
- `nodeId` (string, 可选) — 节点 ID
- `nodeName` (string, 可选) — 节点名称

**返回：** 节点基础信息 + 类型特定属性：
- ColorNode → r/g/b/a/colorMode
- Vector1~4Node → x[/y/z/w]
- SamplerStateNode → filter/wrap/anisotropic
- SampleTexture2DNode → textureType/normalMapSpace
- PropertyNode → 关联属性的 displayName/referenceName/type
- CustomFunctionNode → hlslFunctionName/hlslSourceType/functionName/hlslName

---

### shader_graph_get_available_node_types

**作用：** 获取所有可用的 ShaderGraph 节点类型（约 280 种）

**使用时机：** 不确定节点类型名时搜索，或确认某个节点是否存在

**参数：**
- `filter` (string, 可选) — 按名称过滤
- `maxResults` (int, 可选, 默认 200) — 最大返回数量

**返回：** 每个类型的 typeName/fullName/displayName/category

---

### shader_graph_get_errors

**作用：** 获取 Shader Graph 的编译错误和警告

**使用时机：** 修改图后验证是否有错误，调试节点问题

**参数：** 无

**返回：** hasErrors/hasWarnings/messages（每条含 nodeId/severity/message）

---

## 二、创建工具

### shader_graph_create_asset

**作用：** 创建新的 .shadergraph 文件并打开

**使用时机：** 需要新建一个 Shader Graph 时使用，这是所有操作的起点

**参数：**
- `path` (string, 必需) — 文件路径（如 "Assets/Shaders/MyShader.shadergraph"，可省略 Assets/ 前缀和 .shadergraph 后缀）
- `targetType` (string, 可选, 默认 "HDRP_Lit") — 目标类型：
  - `"HDRP_Lit"` — HDRP Lit 材质
  - `"HDRP_Unlit"` — HDRP Unlit 材质
  - `"URP_Lit"` — URP Lit 材质（自动创建 Block 节点）
  - `"URP_Unlit"` — URP Unlit 材质（自动创建 Block 节点）
  - `"URP_Decal"` — URP Decal 材质（自动设置 surfaceType=Transparent + Block 节点）
  - `"Blank"` — 空白图（无渲染管线 Target）
- `openAfterCreate` (bool, 可选, 默认 true) — 创建后是否自动打开

**返回：** path/targets（创建的 Target 列表）

**注意：** 文件不能已存在，否则返回失败。URP 类型会自动调用 `InitializeOutputs` 生成对应 Block 节点

---

### shader_graph_create_by_name

**作用：** 通过节点类型名创建节点（如 ColorNode、AddNode、MultiplyNode 等）

**使用时机：** 需要添加标准 ShaderGraph 节点时使用。类型名可通过 `get_available_node_types` 查询

**参数：**
- `nodeTypeName` (string, 必需) — 节点类型名，如 "ColorNode"、"AddNode"
- `x` (float, 可选, 默认 200) — 节点 X 坐标
- `y` (float, 可选, 默认 200) — 节点 Y 坐标

**返回：** nodeId/nodeName

---

### shader_graph_create_custom_function

**作用：** 创建 Custom Function 节点并设置 HLSL 代码

**使用时机：** 需要自定义 HLSL 逻辑时使用。支持 String 模式（简单函数体）和 File 模式（复杂逻辑/嵌套函数）

**参数：**
- `functionName` (string, 必需) — 函数名
- `sourceType` (string, 可选, 默认 "String") — 源类型："String" 或 "File"
- `hlslCode` (string, String模式) — HLSL 函数体（仅写函数体，不含签名和花括号）
- `hlslFileName` (string, File模式) — HLSL 文件路径（如 "Assets/Shaders/MyFunc.hlsl"）
- `x` (float, 可选) — 节点 X 坐标
- `y` (float, 可选) — 节点 Y 坐标

**注意：**
- String 模式只写函数体，ShaderGraph 自动包裹签名
- File 模式需先用 `shader_graph_create_hlsl_file` 创建文件
- `functionName` 决定 `hlslFunctionName`（后者为只读，由前者派生）

---

### shader_graph_create_property

**作用：** 创建 Blackboard 属性（左侧面板的 Shader 属性）

**使用时机：** 需要在材质面板暴露可调参数时创建，如颜色、浮点数、纹理等

**参数：**
- `propertyType` (string, 必需) — 属性类型名，如 "Vector1ShaderProperty"、"ColorShaderProperty"、"Texture2DShaderProperty"
- `displayName` (string, 可选) — 显示名称
- `referenceName` (string, 可选) — 引用名（代码中使用，如 "_MyColor"）
- 类型特定参数：
  - Vector1: `value`(float), `floatType`("Default"/"Slider"/"Integer"), `rangeMin`/`rangeMax`(Slider模式)
  - Vector2~4: `x`/`y`/`z`/`w`(float)
  - Color: `r`/`g`/`b`/`a`(float, 0-1)
  - Boolean: `value`(bool)
  - Texture2D: `defaultType`, `modifiable`(bool), `texturePath`(string)

**返回：** referenceName

---

### shader_graph_create_property_node

**作用：** 创建属性引用节点，将 Blackboard 属性拖入图中使用

**使用时机：** 创建了 Blackboard 属性后，需要在图中引用它时使用

**参数：**
- `propertyReferenceName` (string, 必需) — 要引用的属性 referenceName
- `x` (float, 可选) — 节点 X 坐标
- `y` (float, 可选) — 节点 Y 坐标

---

### shader_graph_create_hlsl_file

**作用：** 创建 HLSL Include 文件，用于 Custom Function 的 File 模式

**使用时机：** Custom Function 需要嵌套函数、循环或复杂逻辑时，先用此工具创建 .hlsl 文件，再用 File 模式引用

**参数：**
- `filePath` (string, 必需) — 文件路径（如 "Assets/Shaders/MyFunc.hlsl"，可省略 Assets/ 前缀和 .hlsl 后缀）
- `content` (string, 必需) — HLSL 代码内容
- `overwrite` (bool, 可选, 默认 false) — 是否覆盖已有文件

---

### shader_graph_add_slot

**作用：** 为节点添加端口(Slot)，支持 Custom Function 等节点的端口扩展

**使用时机：** Custom Function 节点创建后端口为空时，用此工具添加输入/输出端口。也适用于其他需要动态添加端口的场景

**参数：**
- `nodeId` (string, 可选) — 节点 ID（与 nodeName 二选一）
- `nodeName` (string, 可选) — 节点名称（与 nodeId 二选一）
- `displayName` (string, 必需) — 端口显示名称（如 "uv"、"Out"）
- `slotType` (string, 必需) — 端口数据类型，支持以下值：
  - `"Float"` / `"Vector1"` — 标量
  - `"Vector2"` — 2D向量
  - `"Vector3"` — 3D向量
  - `"Vector4"` — 4D向量
  - `"Color"` — 颜色
  - `"Texture2D"` — 2D纹理
  - `"Texture3D"` — 3D纹理
  - `"Cubemap"` — 立方体贴图
  - `"SamplerState"` — 采样器状态
  - `"Boolean"` / `"Bool"` — 布尔值
  - `"Matrix2"` / `"Matrix3"` / `"Matrix4"` — 矩阵
  - `"DynamicVector"` — 动态向量
- `direction` (string, 可选, 默认 "Input") — 端口方向：`"Input"` 或 `"Output"`
- `slotId` (int, 可选) — 端口 ID，不提供时自动分配（现有最大 ID + 1）

**返回：** success/message/nodeId/slotId/displayName/slotType/direction

**示例：**
```json
// 为 Custom Function 节点添加 Vector2 输入端口 "uv"
{
  "nodeName": "PanoramaToOctahedron (Custom Function)",
  "displayName": "uv",
  "slotType": "Vector2",
  "direction": "Input"
}

// 添加 Vector2 输出端口 "equiUV"
{
  "nodeName": "PanoramaToOctahedron (Custom Function)",
  "displayName": "equiUV",
  "slotType": "Vector2",
  "direction": "Output"
}
```

**注意事项：**
- 添加端口后需调用 `shader_graph_get_node_slots` 确认端口已正确添加
- 对于 Custom Function 节点，端口 displayName 必须与 HLSL 函数参数名一致
- `slotId` 不提供时自动递增分配，避免 ID 冲突

---

## 三、修改工具

### shader_graph_modify_node

**作用：** 修改节点的属性值

**使用时机：** 需要修改已有节点的参数，如改颜色值、改向量值、改 Custom Function 代码等

**参数：**
- `nodeId` (string, 可选) — 节点 ID
- `nodeName` (string, 可选) — 节点名称
- `displayName` (string, 可选) — 修改节点显示名
- `x`/`y` (float, 可选) — 修改节点位置
- 类型特定参数：
  - ColorNode: `r`/`g`/`b`/`a`(float), `colorMode`(string)
  - Vector1~4Node: `x`[/`y`/`z`/`w`](float)
  - SamplerStateNode: `filter`/`wrap`/`anisotropic`(string)
  - SampleTexture2DNode: `textureType`/`normalMapSpace`(string)
  - PropertyNode: `propertyReferenceName`(string)
  - CustomFunctionNode: `functionName`/`sourceType`(string), `hlslCode`(String模式), `hlslFileName`(File模式)

---

### shader_graph_modify_property

**作用：** 修改 Blackboard 属性的参数

**使用时机：** 需要修改已有属性的值或设置，如改范围、改默认值等

**参数：**
- `referenceName` (string, 可选) — 属性引用名
- `displayName` (string, 可选) — 属性显示名
- 类型特定参数：同 `create_property`

---

### shader_graph_set_slot_value

**作用：** 设置节点 Slot 的值（直接修改端口默认值）

**使用时机：** 需要精确设置某个输入端口的默认值时使用

**参数：**
- `nodeId` (string, 必需) — 节点 ID
- `slotId` (int, 必需) — 端口 ID（通过 `get_node_slots` 获取）
- Vector1/Boolean: `value`(float/bool)
- Vector2: `x`/`y`(float)
- Vector3: `x`/`y`/`z`(float)
- Vector4: `x`/`y`/`z`/`w`(float)

**注意：** Vector2~4 类型必须用 x/y/z/w 参数，不支持 `value` 数组

---

## 四、连接工具

### shader_graph_connect_nodes

**作用：** 连接两个节点的端口（从输出到输入）

**使用时机：** 节点创建后需要连线时使用，这是最核心的操作之一

**参数：**
- `fromNodeId` (string, 必需) — 输出节点 ID
- `fromSlotId` (int, 必需) — 输出端口 ID
- `toNodeId` (string, 必需) — 输入节点 ID
- `toSlotId` (int, 必需) — 输入端口 ID

**使用流程：** 先用 `get_node_slots` 获取两端节点的端口 ID，再调用此工具连接

---

### shader_graph_disconnect_nodes

**作用：** 断开节点之间的连接

**使用时机：** 需要删除已有的连线，或替换连接前先断开旧连接

**参数：**
- `fromNodeId` (string, 可选) — 输出节点 ID（过滤条件）
- `fromSlotId` (int, 可选) — 输出端口 ID（过滤条件）
- `toNodeId` (string, 可选) — 输入节点 ID（过滤条件）
- `toSlotId` (int, 可选) — 输入端口 ID（过滤条件）

**注意：** 参数均为过滤条件，匹配的连线都会被断开。参数越少匹配范围越广

---

## 五、删除工具

### shader_graph_delete_node

**作用：** 删除指定节点

**使用时机：** 移除图中不需要的节点

**参数：**
- `nodeId` (string, 必需) — 节点 ID

---

### shader_graph_delete_nodes

**作用：** 批量删除节点

**使用时机：** 需要一次删除多个节点时使用，比逐个调用更高效

**参数：**
- `nodeIds` (string[], 必需) — 节点 ID 数组

---

### shader_graph_delete_property

**作用：** 删除 Blackboard 属性

**使用时机：** 移除不需要的属性，同时会移除引用该属性的 PropertyNode

**参数：**
- `referenceName` (string, 可选) — 属性引用名
- `displayName` (string, 可选) — 属性显示名

---

## 六、保存工具

### shader_graph_save

**作用：** 保存当前 Shader Graph

**使用时机：** 完成所有修改后保存，或在关键节点保存备份

**参数：**
- `savePath` (string, 可选) — 保存路径（不提供则保存到当前路径）
- `forceSave` (bool, 可选, 默认 false) — 是否强制保存（即使没有修改）

**返回：** path/hasErrors

---

## 七、Graph Inspector — Graph Settings

> 以下工具控制 Graph Inspector 面板中的设置项。所有 set 操作会自动触发 `ValidateGraph` + Inspector 刷新。
> 仅在 HDRP 项目且 Shader Graph 有 HDRP Target 时可用。

### shader_graph_get_graph_settings

**作用：** 获取 Graph Settings（Precision、Active Targets、SubTarget 等）

**使用时机：** 查看当前 Graph 级别配置，确认 Target 和精度

**参数：** 无

**返回：**
- `precision` — "Single" 或 "Half"
- `isSubGraph` — 是否为 Sub Graph
- `activeTargetCount` — 激活的 Target 数量
- `activeTargets[]` — 每个含 typeName/displayName/isHDTarget/subTarget/materialType
- `potentialTargets[]` — 每个含 typeName/displayName/isActive

---

### shader_graph_set_precision

**作用：** 设置 Graph 的精度

**使用时机：** 需要切换精度（如移动端用 Half）

**参数：**
- `precision` (string, 必需) — `"Single"` 或 `"Half"`

---

### shader_graph_set_target_active

**作用：** 激活一个 Target（添加到 Active Targets 列表）

**使用时机：** 需要添加新的渲染管线 Target（如从仅 HDRP 添加 VFX 支持）

**参数：**
- `targetTypeName` (string, 可选) — Target 类型名（如 `"VFXTarget"`、`"HDTarget"`），与 targetIndex 二选一
- `targetIndex` (int, 可选) — Target 在 potentialTargets 中的索引，与 targetTypeName 二选一

**注意：** 类型名来自 `get_graph_settings` 返回的 `potentialTargets[].typeName`

---

### shader_graph_set_target_inactive

**作用：** 停用一个 Target（从 Active Targets 移除）

**使用时机：** 移除不需要的 Target

**参数：**
- `targetTypeName` (string, 必需) — Target 类型名（如 `"VFXTarget"`）

---

## 八、Graph Inspector — HDRP Target Settings

> 操作 HDRP Target 级别的设置。需要 Graph 中有 HDRP Target。

### shader_graph_get_hdrp_target_settings

**作用：** 获取 HDRP Target 的设置

**使用时机：** 查看当前 HDRP Target 配置（SubTarget、Custom Editor GUI、VFX 支持）

**参数：** 无

**返回：**
- `activeSubTarget` — { typeName, displayName }
- `availableSubTargets[]` — 每个 { typeName, displayName }
- `customEditorGUI` — 自定义编辑器 GUI 类名
- `supportVFX` — 是否支持 VFX Graph
- `supportComputeForVertexSetup` — 是否支持 Compute Vertex

---

### shader_graph_set_hdrp_material

**作用：** 切换 HDRP Material 类型（即切换 SubTarget）

**使用时机：** 需要在 Lit / Unlit / Decal 等材质类型间切换

**参数：**
- `subTargetTypeName` (string, 必需) — SubTarget 类型名，如 `"HDLitSubTarget"`、`"HDUnlitSubTarget"`

**注意：** 可用 SubTarget 列表通过 `get_hdrp_target_settings` 的 `availableSubTargets` 获取

---

### shader_graph_set_custom_editor_gui

**作用：** 设置 Custom Editor GUI

**使用时机：** 需要指定材质的自定义 Inspector 类

**参数：**
- `customEditorGUI` (string, 必需) — 自定义编辑器 GUI 类名

---

### shader_graph_set_support_vfx

**作用：** 设置是否支持 VFX Graph

**使用时机：** 需要在 VFX Graph 中使用此 Shader 时启用

**参数：**
- `enabled` (bool, 必需) — 是否启用 VFX 支持

---

### shader_graph_set_support_compute_vertex

**作用：** 设置是否支持 Compute for Vertex Setup

**使用时机：** 需要使用 Compute Shader 驱动顶点变形时启用

**参数：**
- `enabled` (bool, 必需) — 是否启用

---

## 九、Graph Inspector — Surface Options

> 对应 Inspector 的 Surface Options 面板，是最常用的设置区域。
> 数据来源：SystemData + BuiltinData + LightingData。

### shader_graph_get_surface_options

**作用：** 获取 Surface Options 的所有设置（自动检测 URP / HDRP 管线）

**使用时机：** 读取当前表面设置，确认修改前状态

**参数：** 无

**返回属性一览（HDRP）：**

| 属性 | 类型 | 所属数据 | 说明 |
|------|------|----------|------|
| surfaceType | enum | SystemData | `"Opaque"` / `"Transparent"` |
| renderQueueType | enum | SystemData | `"Opaque"` / `"PreRefraction"` / `"Transparent"` / `"Background"` |
| blendMode | enum | SystemData | `"Alpha"` / `"Additive"` / `"Premultiply"` / `"Override"` |
| sortPriority | int | SystemData | 透明排序优先级（自动 clamp） |
| alphaTest | bool | SystemData | Alpha Clipping |
| doubleSidedMode | enum | SystemData | `"Disabled"` / `"Enabled"` / `"FlippedNormals"` / `"MirroredNormals"` |
| transparentZWrite | bool | SystemData | 透明 Z Write |
| zTest | enum | SystemData | CompareFunction 枚举值 |
| customVelocity | bool | SystemData | Custom Velocity |
| excludeFromTUAndAA | bool | SystemData | Exclude From TU And AA |
| tessellation | bool | SystemData | 启用细分 |
| tessellationMaxDisplacement | float | SystemData | 细分最大位移 |
| tessellationBackFaceCullEpsilon | float | SystemData | 细分背面剔除（clamp -1~0） |
| tessellationFactorMinDistance | float | SystemData | 细分起始距离 |
| tessellationFactorMaxDistance | float | SystemData | 细分结束距离 |
| tessellationFactorTriangleSize | float | SystemData | 细分三角形大小 |
| tessellationMode | enum | SystemData | `"None"` / `"Phong"` |
| tessellationShapeFactor | float | SystemData | 细分形状因子（clamp 0~1） |
| transparentCullMode | enum | SystemData | `"Front"` / `"Back"` |
| opaqueCullMode | enum | SystemData | `"Front"` / `"Back"` |
| transparencyFog | bool | BuiltinData | 透明雾效 |
| backThenFrontRendering | bool | BuiltinData | Back Then Front Rendering |
| transparentDepthPrepass | bool | BuiltinData | 透明 Depth Prepass |
| transparentDepthPostpass | bool | BuiltinData | 透明 Depth Postpass |
| transparentWritesMotionVec | bool | BuiltinData | 透明写入运动向量 |
| alphaTestShadow | bool | BuiltinData | Alpha Test Shadow |
| depthOffset | bool | BuiltinData | Depth Offset |
| conservativeDepthOffset | bool | BuiltinData | Conservative Depth Offset |
| supportLodCrossFade | bool | BuiltinData | LOD Cross Fade |
| addPrecomputedVelocity | bool | BuiltinData | Add Precomputed Velocity |
| distortion | bool | BuiltinData | 扭曲 |
| distortionMode | enum | BuiltinData | `"Add"` / `"Replace"` |
| distortionDepthTest | bool | BuiltinData | 扭曲深度测试 |
| normalDropOffSpace | enum | LightingData | `"Tangent"` / `"World"` / `"Object"` |
| receiveDecals | bool | LightingData | 接收 Decals |
| receiveSSR | bool | LightingData | 接收 SSR |
| receiveSSRTransparent | bool | LightingData | 接收透明 SSR |
| specularAA | bool | LightingData | Specular AA |
| blendPreserveSpecular | bool | LightingData | Blend Preserve Specular |

**返回属性一览（URP）：**

| 属性 | 类型 | 说明 |
|------|------|------|
| pipeline | string | "URP" |
| surfaceType | enum | `"Opaque"` / `"Transparent"` |
| alphaMode | enum | `"Alpha"` / `"Premultiply"` / `"Additive"` / `"Multiply"` |
| renderFace | enum | `"Front"` / `"Back"` / `"Both"` |
| alphaClip | bool | Alpha Clipping |
| castShadows | bool | Cast Shadows |
| receiveShadows | bool | Receive Shadows |
| zWriteControl | enum | `"Auto"` / `"ForceEnabled"` / `"ForceDisabled"` |
| zTestMode | enum | `"LessEqual"` / `"Never"` / `"Equal"` / `"Less"` / `"Greater"` / `"NotEqual"` / `"GreaterEqual"` / `"Always"` |
| allowMaterialOverride | bool | Allow Material Override |
| customEditorGUI | string | Custom Editor GUI |
| supportVFX | bool | Support VFX |
| supportsLodCrossFade | bool | LOD Cross Fade |
| overrideStencilState | bool | Override Stencil |
| stencilReference | int | Stencil Reference |
| stencilReadMask | int | Stencil Read Mask |
| stencilWriteMask | int | Stencil Write Mask |
| stencilCompareFunction | enum | Stencil Compare Function |
| activeSubTarget | object | { typeName, displayName } |

**注意：** 工具会自动检测 URP/HDRP，返回对应的管线数据。LightingData 属性在 HDRP Unlit 模式下为 null。

---

### shader_graph_set_surface_options

**作用：** 批量设置 Surface Options 的属性（自动检测 URP / HDRP 管线）

**使用时机：** 需要修改表面选项时使用，可一次设置多个属性

**参数（HDRP）：** 与 HDRP 属性表同名，只传需要修改的参数即可。额外支持：
- `subTargetTypeName` (string, 可选) — 切换 HDRP SubTarget（如 `"HDUnlitSubTarget"`、`"HDLitSubTarget"`）

示例：
```json
{
  "surfaceType": "Transparent",
  "blendMode": "Additive",
  "depthOffset": true,
  "tessellationMaxDisplacement": 0.5
}
```

**参数（URP）：**

| 属性 | 类型 | 说明 |
|------|------|------|
| surfaceType | enum | `"Opaque"` / `"Transparent"` |
| alphaMode | enum | `"Alpha"` / `"Premultiply"` / `"Additive"` / `"Multiply"` |
| renderFace | enum | `"Front"` / `"Back"` / `"Both"` |
| alphaClip | bool | Alpha Clipping |
| castShadows | bool | Cast Shadows |
| receiveShadows | bool | Receive Shadows |
| zWriteControl | enum | `"Auto"` / `"ForceEnabled"` / `"ForceDisabled"` |
| zTestMode | enum | ZTestMode 枚举值 |
| allowMaterialOverride | bool | Allow Material Override |
| supportVFX | bool | Support VFX |
| supportsLodCrossFade | bool | LOD Cross Fade |
| customEditorGUI | string | Custom Editor GUI |
| subTargetTypeName | string | 切换 URP SubTarget（如 `"UniversalUnlitSubTarget"`、`"UniversalLitSubTarget"`、`"UniversalDecalSubTarget"`），切换后自动调用 `InitializeOutputBlocks` 生成 Block 节点 |

示例：
```json
{
  "surfaceType": "Transparent",
  "alphaMode": "Alpha",
  "renderFace": "Both",
  "subTargetTypeName": "UniversalLitSubTarget"
}
```

**返回：** `pipeline`（"HDRP"/"URP"）、`changes[]` — 实际修改的属性列表

**注意事项：**
- 枚举值必须精确匹配（区分大小写），如 `"MirroredNormals"` 不是 `"Mirrored"`
- 切换 `surfaceType` 会自动调用 `TryChangeRenderingPass` 同步 renderQueueType
- `tessellation` 子属性仅在 `tessellation: true` 时可设置
- `sortPriority` 会自动 clamp 到 HDRP 允许范围
- `tessellationBackFaceCullEpsilon` 自动 clamp 到 [-1, 0]
- `tessellationShapeFactor` 自动 clamp 到 [0, 1]

---

## 十、Graph Inspector — Advanced Options

> 对应 Inspector 的 Advanced Options 面板。

### shader_graph_get_advanced_options

**作用：** 获取 Advanced Options 设置

**参数：** 无

**返回：**

| 属性 | 类型 | 说明 |
|------|------|------|
| specularOcclusionMode | enum | `"Off"` / `"FromAO"` / `"FromAOAndBentNormal"` / `"Custom"` |
| overrideBakedGI | bool | Override Baked GI |
| supportLodCrossFade | bool | LOD Cross Fade |
| addPrecomputedVelocity | bool | Add Precomputed Velocity |

---

### shader_graph_set_advanced_options

**作用：** 设置 Advanced Options 属性

**参数：** 只传需要修改的属性名

```json
{
  "specularOcclusionMode": "FromAO",
  "overrideBakedGI": true
}
```

---

## 十一、Graph Inspector — Lit-Specific Options

> 仅在 HDRP Lit SubTarget 激活时可用。

### shader_graph_get_lit_options

**作用：** 获取 Lit 特有的选项

**参数：** 无

**返回：**

| 属性 | 类型 | 说明 |
|------|------|------|
| materialTypeFlag | enum(flags) | Material Type：`"Standard"` / `"SubsurfaceScattering"` / `"SpecularColor"` / `"Translucent"` / `"Anisotropy"` / `"Iridescence"` / `"ClearCoat"` |
| clearCoat | bool | Clear Coat |
| sssTransmission | bool | SSS Transmission |
| refractionModel | enum | `"None"` / `"Planar"` / `"Sphere"` / `"Thin"` |
| energyConservingSpecular | bool | Energy Conserving Specular |
| rayTracing | bool | Ray Tracing |

---

### shader_graph_set_lit_options

**作用：** 设置 Lit 特有的选项

**参数：** 只传需要修改的属性名

```json
{
  "materialTypeFlag": "SubsurfaceScattering",
  "clearCoat": true,
  "refractionModel": "Planar"
}
```

**注意：**
- `refractionModel` 的枚举值是 `"Planar"` 不是 `"Plane"`
- `materialTypeFlag` 是 flags 枚举，可组合多个标志

---

## 十二、Graph Inspector — Utility

### shader_graph_refresh_inspector

**作用：** 强制刷新 Graph Inspector 面板，使数据修改在 UI 上可见

**使用时机：** 调用 set 工具后如果 Inspector 没有立即更新，手动调用此工具。正常情况下所有 set 工具已内置自动刷新

**参数：** 无

---

## 十三、URP 扩展工具

> 以下工具操作 URP SubTarget 的 Keywords、Passes 和 Removed Variants。
> 需要 Graph 中有 URP Target（UniversalLitSubTarget / UniversalUnlitSubTarget）。
> 定义在 `Editor/MCPTools/TJBasicEX/MCPtools.ShaderGraph.TJURP.cs`。

| 工具 | 用途 |
|------|------|
| `shader_graph_urp_get_keywords` | 获取 URP SubTarget 的 Keywords 列表及启用状态 |
| `shader_graph_urp_set_keywords` | 设置 URP SubTarget 的 Keywords 启用/禁用 |
| `shader_graph_urp_get_passes` | 获取 URP SubTarget 的 Passes 列表及启用状态 |
| `shader_graph_urp_set_passes` | 设置 URP SubTarget 的 Passes 启用/禁用 |
| `shader_graph_urp_get_removed_variants` | 获取被移除的 Variants 和 Passes |
| `shader_graph_urp_set_removed_variants` | 设置 Removed Variants 和 Passes |

> **详细参数、返回值和工作流见 `shadergraph-urp-tools` skill。**

---

## 十四、Material 工具集

> 以下工具操作 Material 资产，定义在 `Editor/MCPTools/MCPtools.Material.cs`。

### material_get_info

**作用：** 获取材质的详细信息和属性列表

**参数：**
- `path` (string, 与 guid 二选一) — Material 资产路径
- `guid` (string, 与 path 二选一) — Material GUID

**返回：** name/path/guid/shader/shaderPath/properties[]（每个含 name/displayName/type/value）

---

### material_set_shader

**作用：** 设置材质的 Shader

**参数：**
- `materialPath` / `materialGuid` — 定位 Material
- `shaderPath` / `shaderGuid` / `shaderName` — 定位 Shader（三选一）

---

### material_set_texture

**作用：** 设置材质的贴图属性

**参数：**
- `materialPath` / `materialGuid` — 定位 Material
- `propertyName` (string, 必需) — 贴图属性名（如 `_MainTex`、`_BaseMap`）
- `texturePath` / `textureGuid` — 贴图路径/GUID（为空则清除贴图）
- `offset` (float[2], 可选) — UV 偏移
- `scale` (float[2], 可选) — UV 缩放

---

### material_set_property

**作用：** 设置材质的数值属性（Float, Range, Color, Vector）

**参数：**
- `materialPath` / `materialGuid` — 定位 Material
- `propertyName` (string, 必需) — 属性名
- `value` — 属性值（float / [r,g,b,a] / [x,y,z,w]）

---

### material_create

**作用：** 创建新的 Material

**参数：**
- `path` (string, 必需) — 保存路径（如 `Assets/Materials/NewMat.mat`）
- `shaderPath` / `shaderName` (可选) — 指定 Shader

---

### fbx_get_embedded_materials

**作用：** 获取 FBX 文件中嵌入的材质列表

**参数：** `path` / `guid` — FBX 文件路径

---

### fbx_extract_materials

**作用：** 提取 FBX 嵌入的材质为独立资产

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `outputFolder` (可选) — 输出目录
- `materialNames` (string[], 可选) — 要提取的材质名称
- `useFbxNameAsPrefix` (bool, 默认 true) — 使用 FBX 名作为前缀

---

### fbx_set_embedded_material_shader

**作用：** 设置 FBX 嵌入材质的 Shader

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `materialName` (可选) — 指定材质名（默认设置所有）
- `shaderPath` / `shaderGuid` / `shaderName` — Shader 定位

---

### fbx_set_embedded_material_texture

**作用：** 设置 FBX 嵌入材质的贴图

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `materialName` (可选) — 指定材质名
- `propertyName` (必需) — 贴图属性名
- `texturePath` / `textureGuid` — 贴图定位

---

### fbx_auto_assign_textures

**作用：** 根据命名规则自动为 FBX 嵌入材质分配贴图

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `textureFolder` (可选) — 贴图目录（默认 FBX 同目录）
- `textureMappings` (array, 可选) — 贴图映射规则 `{ textureType, propertyName }`

**默认映射：** BaseColor→_Albedo, Normal→_Normal, AO→_AO, Roughness→_Roughness 等

---

### fbx_set_external_materials

**作用：** 设置 FBX 使用外部材质

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `materialMappings` (array) — 材质映射 `{ embeddedName, externalMaterialPath/externalMaterialGuid }`
- `searchMode` ("Local"/"Recursive"/"Everywhere") — 材质搜索模式

---

### fbx_auto_link_materials

**作用：** 自动将 FBX 嵌入材质映射到提取的外部材质

**参数：**
- `fbxPath` / `fbxGuid` — FBX 文件
- `materialsFolder` (可选) — 外部材质目录（默认 FBX 同目录下 Materials/）

---

### texture_get_import_settings

**作用：** 获取贴图的导入设置

**参数：** `path` / `guid` — 贴图路径

---

### texture_set_import_settings

**作用：** 设置贴图的导入设置

**参数：** `path` / `guid` — 贴图路径，加上要修改的设置项（textureType, sRGB, isReadable, maxTextureSize, wrapMode, filterMode, anisoLevel, mipMapEnabled）

---

### texture_batch_set_import_settings

**作用：** 批量设置多个贴图的导入设置

**参数：** `paths` / `guids` — 贴图路径/GUID 数组，加上要修改的设置项

---

### texture_find_by_name

**作用：** 按名称模式搜索贴图

**参数：** `folder` (默认 "Assets"), `pattern` (支持通配符)

---

### asset_find

**作用：** 按类型和名称模式搜索资产

**参数：** `folder`, `type` (如 "Material", "Texture2D", "Model"), `pattern` (可选)

---

### asset_get_subfolders

**作用：** 获取目录下的子文件夹列表

**参数：** `folder` (默认 "Assets")

---

## 枚举值速查表

### SurfaceType (HDRP Runtime)
| 值 | 说明 |
|----|------|
| Opaque | 不透明 |
| Transparent | 透明 |

### BlendMode (HDRP Runtime)
| 值 | 说明 |
|----|------|
| Alpha | Alpha 混合 |
| Additive | 加法混合 |
| Premultiply | 预乘 Alpha |
| Override | 覆盖 |

### DoubleSidedMode (HDRP Editor)
| 值 | 说明 |
|----|------|
| Disabled | 禁用 |
| Enabled | 启用 |
| FlippedNormals | 翻转法线 |
| MirroredNormals | 镜像法线 |

### OpaqueCullMode / TransparentCullMode (HDRP Runtime)
| 值 | 说明 |
|----|------|
| Front | 正面剔除 |
| Back | 背面剔除 |

### TessellationMode (HDRP Runtime)
| 值 | 说明 |
|----|------|
| None | 无 |
| Phong | Phong 细分 |

### DistortionMode (HDRP Editor)
| 值 | 说明 |
|----|------|
| Add | 加法 |
| Replace | 替换 |

### NormalDropOffSpace (ShaderGraph)
| 值 | 说明 |
|----|------|
| Tangent | 切线空间 |
| World | 世界空间 |
| Object | 对象空间 |

### CompareFunction (UnityEngine.Rendering)
| 值 | 说明 |
|----|------|
| Disabled / Never / Less / Equal / LessEqual / Greater / NotEqual / GreaterEqual / Always |

### RenderQueueType (HDRP Runtime)
| 值 | 说明 |
|----|------|
| Opaque / PreRefraction / Transparent / Background |

### SpecularOcclusionMode (HDRP Editor)
| 值 | 说明 |
|----|------|
| Off / FromAO / FromAOAndBentNormal / Custom |

### RefractionModel (HDRP Runtime)
| 值 | 说明 |
|----|------|
| None | 无折射 |
| Planar | 平面折射 |
| Sphere | 球体折射 |
| Thin | 薄折射 |

### HDLitData.MaterialTypeFlag (HDRP Editor, flags enum)
| 值 | 说明 |
|----|------|
| Standard | 标准 |
| SubsurfaceScattering | 次表面散射 |
| SpecularColor | 高光颜色 |
| Translucent | 半透明 |
| Anisotropy | 各向异性 |
| Iridescence | 虹彩 |
| ClearCoat | 清漆 |

---

## 已知限制

1. **Vector4 Slot 设值** — `set_slot_value` 对 Vector2~4MaterialSlot 必须用 x/y/z/w 参数，不支持 value 数组
2. **CustomFunction 创建后 Slot 为空** — `create_custom_function` 创建的节点 Slot 列表可能为空，需要手动添加或研究自动注册机制
3. **CacheMethod 同名冲突** — 反射缓存可能返回继承链中错误的方法（同名不同签名），需用 `GetMethod` 手动指定参数类型
4. **编译后窗口重置** — C# 脚本编译后 ShaderGraph 窗口内部字段为 null，需关闭并重新打开 .shadergraph 文件
5. **Inspector 刷新** — 所有 set 工具已内置 `RefreshAfterChange`（ValidateGraph + doesInspectorNeedUpdate + Update + Repaint），极少数情况下需手动调用 `shader_graph_refresh_inspector`
6. **SetTargetActive/Inactive 签名** — `GraphData.SetTargetActive(Target, bool)` / `SetTargetInactive(Target, bool)` 有额外的 `bool skipSortAndUpdate` 参数，不能使用 `CacheMethod`（会 Ambiguous），必须用 `GetMethod` 指定参数类型数组
7. **HDRP 枚举程序集** — `SurfaceType`/`BlendMode`/`TessellationMode`/`OpaqueCullMode`/`TransparentCullMode` 等枚举位于 `Unity.RenderPipelines.HighDefinition.Runtime`（不是 `.Editor`），加载错误程序集会导致 `InvalidCastException`
8. **GraphPrecision 命名空间** — 完整类型名为 `UnityEditor.ShaderGraph.Internal.GraphPrecision`，不是 `UnityEditor.ShaderGraph.GraphPrecision`
9. **URP SubTarget 切换** — `shader_graph_set_surface_options` 的 `subTargetTypeName` 切换 URP SubTarget 后会自动调用 `InitializeOutputBlocks` 生成 Block 节点，确保 Fragment/Vertex Context 正确
10. **SGReflection 独立文件** — 反射辅助类已从 MCPtools.ShaderGraph.cs 提取到 SGReflection.cs，所有 ShaderGraph 类型通过懒加载缓存访问
