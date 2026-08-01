---
name: shadertoy-to-shadergraph
description: Convert ShaderToy GLSL code to Unity ShaderGraph HLSL, including type conversion, matrix construction differences, and Custom Function setup. Use for shadertoy conversion, GLSL to HLSL, or shader porting.
---

# ShaderToy 转 ShaderGraph Skill

## 核心准则

> **共享规则见 CODELY.md**（先验真管线、禁止手改 .shadergraph JSON、工具失败时停止并回问用户）

### 1. 先分析后转换
在开始转换前，必须完整分析 ShaderToy 代码结构：
- 识别所有函数（辅助函数、主函数）
- 识别输入依赖（uniforms: iTime, iResolution, iChannel0 等）
- 识别输出（fragColor）
- 确定是否需要 File 模式（循环、嵌套函数）

### 2. 向用户陈述实现逻辑并确认
在执行任何操作之前，**必须向用户陈述**：
- 需要创建哪些属性
- 需要创建哪些节点
- 节点之间如何连接
- 为什么这样做

**获得用户确认后，再执行操作。**

### 3. 选择正确的 Custom Function 模式

| 特征 | String 模式 | File 模式 |
|------|-------------|-----------|
| 简单计算（无循环） | ✅ | ✅ |
| 包含循环结构 | ❌ | ✅ |
| 包含嵌套/辅助函数 | ❌ | ✅ |
| 复杂算法（噪声、纹理采样等） | ❌ | ✅ |

---

## GLSL → HLSL 转换规则

### 1. 类型转换

| GLSL | HLSL | 说明 |
|------|------|------|
| `vec2` | `float2` | 2D 向量 |
| `vec3` | `float3` | 3D 向量 |
| `vec4` | `float4` | 4D 向量 |
| `mat2` | `float2x2` | 2x2 矩阵 |
| `mat3` | `float3x3` | 3x3 矩阵 |
| `mat4` | `float4x4` | 4x4 矩阵 |
| `sampler2D` | `UnityTexture2D` | **Unity 特有类型** |
| `sampler2D` | `Texture2D` | 仅用于内部采样 |

### 2. 函数转换

| GLSL | HLSL |
|------|------|
| `fract(x)` | `frac(x)` |
| `mix(a, b, t)` | `lerp(a, b, t)` |
| `texture(samp, uv)` | `SAMPLE_TEXTURE2D(tex, smp, uv)` |
| `textureGrad(samp, uv, dx, dy)` | `SAMPLE_TEXTURE2D_GRAD(tex, smp, uv, dx, dy)` |
| `dFdx(x)` | `ddx(x)` |
| `dFdy(x)` | `ddy(x)` |
| `mod(a, b)` | `fmod(a, b)` |
| `atan(y, x)` | `atan2(y, x)` |

### 3. Unity 特有类型（重要！）

ShaderGraph 中纹理和采样器必须使用 Unity 包装类型：

```hlsl
// ❌ 错误 - 标准 HLSL 类型
void MyFunc_float(Texture2D tex, SamplerState smp, float2 uv, out float3 Out)
{
    Out = tex.SampleGrad(smp, uv, ddx(uv), ddy(uv)).xyz;
}

// ✅ 正确 - Unity 包装类型
void MyFunc_float(UnityTexture2D tex, UnitySamplerState smp, float2 uv, out float3 Out)
{
    Out = SAMPLE_TEXTURE2D_GRAD(tex, smp, uv, ddx(uv), ddy(uv)).xyz;
}
```

### 4. ShaderToy Uniforms 映射

| ShaderToy | ShaderGraph 属性 | 类型 |
|-----------|------------------|------|
| `iTime` | 创建 `Time` 节点或 `Float` 属性 | Float |
| `iResolution` | 创建 `ScreenParams` 节点或 `Vector2` 属性 | Vector2 |
| `iChannel0` | `Texture2D` 属性 | Texture2D |
| `iMouse` | 创建 `Vector4` 属性 | Vector4 |

### 5. 矩阵构造顺序差异（重要坑！）

**GLSL 和 HLSL 的矩阵构造顺序不同，这是最容易出错的转换点！**

| 语言 | 构造方式 | 示例 |
|------|----------|------|
| GLSL | **按列填充** (Column-major) | `mat2(a, b, c, d)` → `[[a, c], [b, d]]` |
| HLSL | **按行填充** (Row-major) | `float2x2(a, b, c, d)` → `[[a, b], [c, d]]` |

#### 旋转矩阵示例

```glsl
// GLSL 原始代码
float co = cos(angle);
float si = sin(angle);
mat2 rot = mat2(co, -si, si, co);  // 按列填充
p = rot * p;  // 旋转
```

```hlsl
// ❌ 错误转换 - 直接替换类型名
float2x2 rot = float2x2(co, -si, si, co);  // 按行填充，结果错误！

// ✅ 正确转换 - 需要转置
float2x2 rot = float2x2(co, si, -si, co);  // 转置后正确
p = mul(rot, p);
```

#### 转换规则

对于 `mat2x2(a, b, c, d)` 转换为 HLSL：

```
GLSL: mat2(a, b, c, d) = | a  c |
                         | b  d |

HLSL: float2x2(a, b, c, d) = | a  b |
                             | c  d |

正确转换: float2x2(a, c, b, d)  // 交换 b 和 c 的位置
```

#### 通用转换公式

对于 NxN 矩阵，GLSL `matN(e0, e1, ..., eN*N-1)` 转换为 HLSL：

```
HLSL 矩阵元素 [i][j] = GLSL 矩阵元素 [j][i]
```

即：**转置矩阵的元素顺序**

### 6. 避免使用 Unity 内置变量名

创建属性时，避免使用以下 Unity 内置变量名，否则会导致 `redefinition` 错误：

| 保留名称 | 用途 |
|----------|------|
| `_Time` | Unity 内置时间 |
| `_SinTime` | 正弦时间 |
| `_CosTime` | 余弦时间 |
| `unity_ObjectToWorld` | 对象到世界矩阵 |
| `unity_WorldToObject` | 世界到对象矩阵 |

**解决方案**：使用不同的名称，如 `AnimTime`、`CustomTime` 等。

---

## 工作流程

```
分析代码 → 设计转换方案 → 用户确认 → 创建 HLSL 文件 → 创建节点 → 连接测试 → 验证错误
```

### 步骤详解

#### 1. 分析 ShaderToy 代码

```javascript
// 识别结构
- hash4()        // 辅助函数 → 需要保留
- textureNoTile() // 核心函数 → 需要保留
- mainImage()    // 入口函数 → 转换为 Custom Function

// 识别依赖
- iChannel0      // 纹理输入
- iTime          // 时间（可选）
- iResolution    // 分辨率（可选）
```

#### 2. 设计转换方案

向用户陈述：
- 需要创建的属性（名称、类型、用途）
- 需要创建的节点
- HLSL 文件结构
- 连接方式

#### 3. 创建 HLSL 文件

```hlsl
#ifndef UNIQUE_GUARD_NAME
#define UNIQUE_GUARD_NAME

// 辅助函数（保持原样，仅转换语法）
float4 hash4(float2 p)
{
    return frac(sin(float4(...)) * 103.0);
}

// 主函数（使用 Unity 类型）
void FunctionName_float(
    UnityTexture2D tex,      // Unity 纹理类型
    UnitySamplerState smp,   // Unity 采样器类型
    float2 uv,
    float v,
    out float3 Out)
{
    // 转换后的代码
    // textureGrad → SAMPLE_TEXTURE2D_GRAD
    // fract → frac
    // mix → lerp
}

#endif
```

#### 4. 创建 Custom Function 节点

```
shader_graph_create_custom_function
├── functionName: "FunctionName"
├── sourceType: "File"
├── hlslFilePath: "Assets/Shaders/FunctionName.hlsl"
├── inputs: [
│   {"name": "tex", "type": "Texture2D"},
│   {"name": "smp", "type": "SamplerState"},
│   {"name": "uv", "type": "Vector2"},
│   {"name": "v", "type": "Vector1"}
│   ]
└── outputs: [{"name": "Out", "type": "Vector3"}]
```

#### 5. 创建属性和节点

```
shader_graph_create_property     // 创建 Blackboard 属性
shader_graph_create_property_node // 创建属性引用节点
shader_graph_create_by_name       // 创建 UV、Sampler State 等节点
```

#### 6. 连接节点

```
shader_graph_get_node_slots      // 获取端口信息
shader_graph_connect_nodes       // 逐个连接
```

#### 7. 验证和调试

```
shader_graph_get_errors          // 检查编译错误
shader_graph_save                // 保存
```

---

## MCP 工具清单

| 工具 | 用途 |
|------|------|
| `shader_graph_get_info` | 获取当前 Shader Graph 信息 |
| `shader_graph_create_property` | 创建 Blackboard 属性 |
| `shader_graph_create_property_node` | 创建属性引用节点 |
| `shader_graph_create_by_name` | 通过类型名创建节点 |
| `shader_graph_create_custom_function` | 创建 Custom Function 节点 |
| `shader_graph_create_hlsl_file` | 创建 HLSL Include 文件 |
| `shader_graph_get_node_slots` | 获取节点的输入/输出端口 |
| `shader_graph_connect_nodes` | 连接两个节点的端口 |
| `shader_graph_get_nodes` | 获取图中所有节点列表 |
| `shader_graph_get_errors` | 获取编译错误 |
| `shader_graph_save` | 保存 Shader Graph |

---

## 完整示例：TextureNoTile

### 原始 ShaderToy 代码

```glsl
vec4 hash4(vec2 p) { 
    return fract(sin(vec4(
        1.0+dot(p,vec2(37.0,17.0)), 
        2.0+dot(p,vec2(11.0,47.0)),
        3.0+dot(p,vec2(41.0,29.0)),
        4.0+dot(p,vec2(23.0,31.0))))*103.0); 
}

vec3 textureNoTile(sampler2D samp, in vec2 uv, float v) {
    vec2 p = floor(uv);
    vec2 f = fract(uv);
    vec2 ddx = dFdx(uv);
    vec2 ddy = dFdy(uv);
    
    vec3 va = vec3(0.0);
    float w1 = 0.0;
    float w2 = 0.0;
    
    for(int j=-1; j<=1; j++)
    for(int i=-1; i<=1; i++) {
        vec2 g = vec2(float(i),float(j));
        vec4 o = hash4(p + g);
        vec2 r = g - f + o.xy;
        float d = dot(r,r);
        float w = exp(-5.0*d);
        vec3 c = textureGrad(samp, uv + v*o.zw, ddx, ddy).xyz;
        va += w*c;
        w1 += w;
        w2 += w*w;
    }
    
    float mean = 0.3;
    vec3 res = mean + (va-w1*mean)/sqrt(w2);
    return mix(va/w1, res, v);
}
```

### 转换后的 HLSL

```hlsl
#ifndef TEXTURE_NO_TILE_HLSL
#define TEXTURE_NO_TILE_HLSL

float4 hash4(float2 p)
{
    return frac(sin(float4(
        1.0 + dot(p, float2(37.0, 17.0)),
        2.0 + dot(p, float2(11.0, 47.0)),
        3.0 + dot(p, float2(41.0, 29.0)),
        4.0 + dot(p, float2(23.0, 31.0))
    )) * 103.0);
}

void TextureNoTile_float(
    UnityTexture2D tex,
    UnitySamplerState smp,
    float2 uv,
    float v,
    out float3 Out)
{
    float2 p = floor(uv);
    float2 f = frac(uv);
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    
    float3 va = float3(0.0, 0.0, 0.0);
    float w1 = 0.0;
    float w2 = 0.0;
    
    for (int j = -1; j <= 1; j++)
    {
        for (int i = -1; i <= 1; i++)
        {
            float2 g = float2(float(i), float(j));
            float4 o = hash4(p + g);
            float2 r = g - f + o.xy;
            float d = dot(r, r);
            float w = exp(-5.0 * d);
            float3 c = SAMPLE_TEXTURE2D_GRAD(tex, smp, uv + v * o.zw, dx, dy).xyz;
            va += w * c;
            w1 += w;
            w2 += w * w;
        }
    }
    
    float mean = 0.3;
    float3 res = mean + (va - w1 * mean) / sqrt(w2);
    Out = lerp(va / w1, res, v);
}

#endif
```

### 关键转换点

| 原始 GLSL | 转换后 HLSL | 说明 |
|-----------|-------------|------|
| `sampler2D samp` | `UnityTexture2D tex` | Unity 纹理类型 |
| `textureGrad(samp, ...)` | `SAMPLE_TEXTURE2D_GRAD(tex, smp, ...)` | Unity 采样宏 |
| `fract` | `frac` | 函数名差异 |
| `mix` | `lerp` | 函数名差异 |
| `vec3(0.0)` | `float3(0.0, 0.0, 0.0)` | 类型名 + 显式构造 |

---

## 完整示例：Gaussian Splatting（含矩阵转换）

### 原始 ShaderToy 代码（矩阵部分）

```glsl
// GLSL 旋转矩阵
float co = cos(an);
float si = sin(an);
p = mat2(co, -si, si, co) * p;  // 按列填充
```

### 转换后的 HLSL

```hlsl
// HLSL 旋转矩阵 - 需要转置！
float co = cos(an);
float si = sin(an);
// GLSL mat2(co, -si, si, co) 按列填充 = | co  si |
//                                         | -si co |
// HLSL 需要按行填充，所以要转置：
p = mul(float2x2(co, si, -si, co), p);  // 按行填充，结果正确
```

### 矩阵转换对照表

| GLSL 代码 | GLSL 矩阵 | HLSL 正确转换 | HLSL 矩阵 |
|-----------|-----------|---------------|-----------|
| `mat2(a, b, c, d)` | `[[a,c],[b,d]]` | `float2x2(a, c, b, d)` | `[[a,c],[b,d]]` |
| `mat3(a,b,c,d,e,f,g,h,i)` | 3x3 按列 | 交换行列位置 | 3x3 按行 |

---

## 常见错误与解决

| 错误信息 | 原因 | 解决方案 |
|----------|------|----------|
| `cannot convert from 'struct UnityTexture2D' to 'Texture2D'` | 使用了标准 HLSL 类型 | 改用 `UnityTexture2D` |
| `cannot convert from 'const struct UnitySamplerState' to 'SamplerState'` | 使用了标准 HLSL 类型 | 改用 `UnitySamplerState` |
| `undefined identifier 'fract'` | GLSL 函数名 | 改为 `frac` |
| `undefined identifier 'mix'` | GLSL 函数名 | 改为 `lerp` |
| `cannot convert from 'vec3' to 'float3'` | GLSL 类型名 | 改为 `float3` |
| 循环不执行 | String 模式不支持循环 | 使用 File 模式 |
| 辅助函数未定义 | String 模式不支持嵌套函数 | 使用 File 模式 |
| `redefinition of '_Time'` | 属性名与 Unity 内置变量冲突 | 改用其他名称如 `AnimTime` |
| 旋转/变换结果错误 | GLSL/HLSL 矩阵构造顺序不同 | 转置矩阵元素顺序 |
| 图形位置偏移/错位 | 矩阵构造顺序错误 | 参见"矩阵构造顺序差异"章节 |

---

## 调试技巧

### 1. 逐步验证

先创建最简版本验证连接：

```hlsl
void TextureNoTile_float(
    UnityTexture2D tex,
    UnitySamplerState smp,
    float2 uv,
    float v,
    out float3 Out)
{
    // 简单测试：直接采样
    Out = SAMPLE_TEXTURE2D(tex, smp, uv).xyz;
}
```

### 2. 分离问题

如果编译失败：
1. 检查类型是否使用 Unity 包装类型
2. 检查函数名是否转换（fract→frac, mix→lerp）
3. 检查纹理采样是否使用正确的宏

### 3. 检查节点连接

```
shader_graph_get_node_slots  // 确认端口 ID
shader_graph_get_nodes       // 确认节点存在
```

---

## 注意事项

1. **File 模式优先**：涉及循环、嵌套函数、纹理采样的情况，必须使用 File 模式

2. **Unity 类型必须**：纹理和采样器参数必须使用 `UnityTexture2D` 和 `UnitySamplerState`

3. **采样宏必须**：使用 `SAMPLE_TEXTURE2D` 系列宏而非 `tex.Sample()`

4. **矩阵构造要转置**：GLSL `mat2(a,b,c,d)` 转换为 HLSL `float2x2(a,c,b,d)`，需要交换元素位置

5. **避免内置变量名**：不要使用 `_Time`、`_SinTime` 等 Unity 内置变量名作为属性名

6. **不要私自更改方案**：如果 File 模式遇到问题，向用户通报并寻求解决方案，不要私自改成 String 模式

7. **保存前验证**：使用 `shader_graph_get_errors` 确认无编译错误后再保存
