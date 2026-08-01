---
name: hlsl-file-creator
description: Create HLSL Include files and Custom Function nodes (File mode) with nested functions and complex logic. Use for FBM, noise algorithms, or procedural generation in HLSL.
---

# HLSL File 创建 Skill

## 适用场景

当 Custom Function 需要以下功能时，使用 **File 模式** 而非 String 模式：

| 场景 | String 模式 | File 模式 |
|------|-------------|-----------|
| 简单计算（单行代码） | ✅ | ✅ |
| 需要嵌套函数 | ❌ | ✅ |
| 需要循环结构 | ❌ | ✅ |
| 复杂算法（噪声、侵蚀等） | ❌ | ✅ |
| 代码复用（多个节点共享） | ❌ | ✅ |

---

## 核心准则

> **共享规则见 CODELY.md**（先验真管线、禁止手改 .shadergraph JSON、工具失败时停止并回问用户）

### 1. HLSL 文件结构

```hlsl
#ifndef UNIQUE_GUARD_NAME
#define UNIQUE_GUARD_NAME

// 1. 常量定义（使用 #define，不用 static const）
#define PI 3.14159265358979

// 2. 辅助函数定义
float2 hash2(float2 x)
{
    // 函数实现
}

// 3. 主函数（命名规则：FunctionName_float）
void MyFunction_float(
    float2 UV,           // 输入参数
    float Scale,
    out float Height,    // 输出参数
    out float2 Normal)
{
    // 主逻辑
    Height = ...;
    Normal = ...;
}

#endif // UNIQUE_GUARD_NAME
```

### 2. 常量定义规范

```hlsl
// ❌ 错误 - static const 在 ShaderGraph include 中不支持
static const float PI = 3.14159;

// ✅ 正确 - 使用 #define
#define PI 3.14159265358979
```

### 3. 函数命名规范

```hlsl
// 主函数必须以 _float 结尾（ShaderGraph 约定）
void MountainTerrain_float(...)  // ✅ 正确
void MountainTerrain(...)        // ❌ 错误
```

### 4. 输入输出类型

| 类型 | HLSL 类型 | ShaderGraph 类型 |
|------|-----------|------------------|
| 标量 | `float` | `Vector1` |
| 2D向量 | `float2` | `Vector2` |
| 3D向量 | `float3` | `Vector3` |
| 4D向量 | `float4` | `Vector4` |
| 颜色 | `float3` 或 `float4` | `Color` |
| 纹理 | `Texture2D` | `Texture2D` |

---

## 工作流程

```
1. 设计函数 → 2. 编写 HLSL 文件 → 3. 创建文件 → 4. 创建节点 → 5. 连接测试
```

### 步骤详解

#### 1. 设计函数
- 确定输入参数（名称、类型）
- 确定输出参数（名称、类型）
- 规划辅助函数

#### 2. 编写 HLSL 文件
- 使用 `#ifndef/#define` 防止重复包含
- 定义常量用 `#define`
- 先定义辅助函数，后定义主函数
- 主函数使用 `FunctionName_float` 命名

#### 3. 创建 HLSL 文件
```
shader_graph_create_hlsl_file
├── filePath: "Assets/Shaders/MyFunction.hlsl"
├── content: "HLSL代码内容"
└── overwrite: true/false
```

#### 4. 创建 Custom Function 节点
```
shader_graph_create_custom_function
├── functionName: "MyFunction"
├── sourceType: "File"
├── hlslFilePath: "Assets/Shaders/MyFunction.hlsl"
├── inputs: [{"name": "UV", "type": "Vector2"}, ...]
└── outputs: [{"name": "Height", "type": "Vector1"}, ...]
```

#### 5. 连接测试
- 连接输入节点
- 连接到 Master 节点验证效果

---

## MCP 工具

| 工具 | 用途 |
|------|------|
| `shader_graph_create_hlsl_file` | 创建 HLSL Include 文件 |
| `shader_graph_create_custom_function` | 创建 Custom Function 节点 |
| `shader_graph_get_node_slots` | 获取节点的输入/输出端口 |
| `shader_graph_connect_nodes` | 连接节点 |
| `shader_graph_delete_node` | 删除节点 |

---

## 完整示例

### 示例：带导数的梯度噪声

**HLSL 文件内容：**

```hlsl
#ifndef NOISE_WITH_DERIVATIVES_HLSL
#define NOISE_WITH_DERIVATIVES_HLSL

#define PI 3.14159265358979

// Hash function
float2 hash2(float2 x)
{
    float2 k = float2(0.3183099, 0.3678794);
    x = x * k + k.yx;
    return -1.0 + 2.0 * frac(16.0 * k * frac(x.x * x.y * (x.x + x.y)));
}

// Main function
void NoiseWithDerivatives_float(
    float2 UV,
    float Scale,
    out float Value,
    out float2 Derivatives)
{
    float2 p = UV * Scale;
    
    float2 i = floor(p);
    float2 f = frac(p);
    
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
    
    float2 ga = hash2(i + float2(0.0, 0.0));
    float2 gb = hash2(i + float2(1.0, 0.0));
    float2 gc = hash2(i + float2(0.0, 1.0));
    float2 gd = hash2(i + float2(1.0, 1.0));
    
    float va = dot(ga, f - float2(0.0, 0.0));
    float vb = dot(gb, f - float2(1.0, 0.0));
    float vc = dot(gc, f - float2(0.0, 1.0));
    float vd = dot(gd, f - float2(1.0, 1.0));
    
    Value = va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * (va - vb - vc + vd);
    Derivatives = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd) +
                  du * (u.yx * (va - vb - vc + vd) + float2(vb, vc) - va);
}

#endif // NOISE_WITH_DERIVATIVES_HLSL
```

**创建步骤：**

```
1. shader_graph_create_hlsl_file
   - filePath: "Assets/Shaders/NoiseWithDerivatives.hlsl"
   - content: [上述代码]

2. shader_graph_create_custom_function
   - functionName: "NoiseWithDerivatives"
   - sourceType: "File"
   - hlslFilePath: "Assets/Shaders/NoiseWithDerivatives.hlsl"
   - inputs: [{"name": "UV", "type": "Vector2"}, {"name": "Scale", "type": "Vector1"}]
   - outputs: [{"name": "Value", "type": "Vector1"}, {"name": "Derivatives", "type": "Vector2"}]
```

---

## 常见错误

| 错误 | 原因 | 解决 |
|------|------|------|
| `unexpected float constant` | 使用了 `static const` | 改用 `#define` |
| `undeclared identifier` | 函数顺序错误 | 辅助函数定义在主函数之前 |
| 噪声块状不连续 | 导数计算符号错误 | 检查 `+gd` vs `-gd` |
| 节点显示红色 | 函数名不以 `_float` 结尾 | 重命名为 `FunctionName_float` |
| 文件找不到 | 路径不以 `Assets/` 开头 | 使用完整路径 `Assets/Shaders/xxx.hlsl` |

---

## 调试技巧

### 1. 逐步验证

```hlsl
// 先输出简单值验证连接
void MyFunction_float(float2 UV, out float Out)
{
    Out = UV.x;  // 简单测试
}
```

### 2. 分离复杂逻辑

将复杂算法拆分为多个辅助函数：

```hlsl
float2 hash2(float2 x) { ... }
float3 noised(float2 p) { ... }
float3 erosion(float2 p, float2 dir) { ... }

void MountainTerrain_float(...) 
{
    // 调用辅助函数
    float3 n = noised(uv);
}
```

### 3. 检查导数公式

对于带导数的噪声，确保公式正确：

```hlsl
// 导数公式中的符号
ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd)
//                                                                    ^^^ 这里是 +gd
```
