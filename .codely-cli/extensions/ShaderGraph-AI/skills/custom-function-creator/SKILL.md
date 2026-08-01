---
name: custom-function-creator
description: Create and modify ShaderGraph Custom Function nodes (String mode) with HLSL coding rules and best practices. Use for custom function, noise, or HLSL in ShaderGraph.
---

# Custom Function 创建 Skill

## 核心准则

> **共享规则见 CODELY.md**（先验真管线、禁止手改 .shadergraph JSON、工具失败时停止并回问用户）

### 1. HLSL 代码规范

**关键：Custom Function 只需要写函数体内容，ShaderGraph 会自动包裹！**

```hlsl
// ❌ 错误写法 - 不要写函数签名和花括号
void MyFunction_float(float2 UV, out float Out)
{
    Out = UV.x;
}

// ✅ 正确写法 - 只写函数体
Out = UV.x;
```

ShaderGraph 会自动生成：
```hlsl
void MyFunction_float(float2 UV, out float Out)
{
    Out = UV.x;  // 你的代码被包裹在这里
}
```

### 2. 通过API添加node的输入输出

### 3. 禁止嵌套函数定义

```hlsl
// ❌ 错误 - 不能在函数体内定义函数
float hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}
Out = hash(UV);

// ✅ 正确 - 内联展开
float h = frac(sin(dot(UV, float2(127.1, 311.7))) * 43758.5453);
Out = h;
```

### 4. 输出归一化

**始终确保输出在有效范围内：**

```hlsl
// 噪声类 - 归一化到 0-1
Out = saturate(n * 0.5 + 0.5);

// 颜色类 - 确保在 0-1
Out = saturate(color);

// 方向类 - 归一化向量
Out = normalize(direction);
```

---

## 常用代码模板

### Perlin 噪声 2D

```hlsl
float2 i = floor(UV);
float2 f = frac(UV);
float2 u = f * f * (3.0 - 2.0 * f);

float2 p00 = i + float2(0.0, 0.0);
float2 p10 = i + float2(1.0, 0.0);
float2 p01 = i + float2(0.0, 1.0);
float2 p11 = i + float2(1.0, 1.0);

float2 h00 = float2(dot(p00, float2(127.1, 311.7)), dot(p00, float2(269.5, 183.3)));
float2 h10 = float2(dot(p10, float2(127.1, 311.7)), dot(p10, float2(269.5, 183.3)));
float2 h01 = float2(dot(p01, float2(127.1, 311.7)), dot(p01, float2(269.5, 183.3)));
float2 h11 = float2(dot(p11, float2(127.1, 311.7)), dot(p11, float2(269.5, 183.3)));

float2 g00 = -1.0 + 2.0 * frac(sin(h00) * 43758.5453123);
float2 g10 = -1.0 + 2.0 * frac(sin(h10) * 43758.5453123);
float2 g01 = -1.0 + 2.0 * frac(sin(h01) * 43758.5453123);
float2 g11 = -1.0 + 2.0 * frac(sin(h11) * 43758.5453123);

float n00 = dot(g00, f - float2(0.0, 0.0));
float n10 = dot(g10, f - float2(1.0, 0.0));
float n01 = dot(g01, f - float2(0.0, 1.0));
float n11 = dot(g11, f - float2(1.0, 1.0));

float nx0 = lerp(n00, n10, u.x);
float nx1 = lerp(n01, n11, u.x);
float n = lerp(nx0, nx1, u.y);

Out = saturate(n * 0.5 + 0.5);
```

### 简单 Hash

```hlsl
Out = frac(sin(dot(UV, float2(12.9898, 78.233))) * 43758.5453);
```

### UV 旋转

```hlsl
float2 center = float2(0.5, 0.5);
float2 centered = UV - center;
float c = cos(Angle);
float s = sin(Angle);
float2 rotated = float2(
    centered.x * c - centered.y * s,
    centered.x * s + centered.y * c
);
Out = rotated + center;
```

### 颜色混合

```hlsl
Out = lerp(ColorA, ColorB, T);
```

---

## 工作流程

```
1. 确认需求 → 2. 设计输入输出 → 3. 编写 HLSL 函数体 → 4. 创建节点 → 5. 连接测试
```

### 步骤详解

1. **确认需求**
   - 用户想要实现什么效果？
   - 是否可以用内置节点替代？

2. **设计输入输出**
   - 确定需要的参数类型
   - 确定输出类型

3. **编写 HLSL 函数体**
   - 只写函数体，不含签名
   - 不定义嵌套函数
   - 确保输出归一化

4. **创建节点**
   ```
   shader_graph_create_custom_function
   ```

5. **连接测试**
   - 连接输入源
   - 连接到输出节点验证效果

---

## MCP 工具

| 工具 | 用途 |
|------|------|
| `shader_graph_create_custom_function` | 创建 Custom Function 节点 |
| `shader_graph_get_node_slots` | 获取节点的输入/输出端口 |
| `shader_graph_connect_nodes` | 连接节点 |
| `shader_graph_delete_node` | 删除节点 |
| `shader_graph_modify_node` | 修改节点属性 |

---

## 常见错误

| 错误 | 原因 | 解决 |
|------|------|------|
| 节点显示红色错误 | 写了完整函数签名 | 只写函数体 |
| 输出全黑/全白 | 输出未归一化 | 使用 `saturate()` |
| 编译失败 | 定义了嵌套函数 | 内联展开代码 |
| 类型不匹配 | 输入输出类型声明错误 | 检查 Vector1/2/3/4 |
| `unexpected token 'point'` 等 | 使用了 HLSL 保留字作为变量名 | 避免使用保留字 |

### HLSL 保留字列表

以下词汇在 HLSL 中有特殊含义，**不能用作变量名**：

```
point, line, triangle, linear, centroid, noperspective,
sample, center, smooth, flat, invariant, precise,
row_major, column_major, packoffset, register, cbuffer,
tbuffer, technique, pass, compile, const, static, uniform,
volatile, extern, shared, groupshared, inline, target,
in, out, inout, uniform, snorm, unorm, signed, unsigned
```

**示例：**
```hlsl
// ❌ 错误 - point 是保留字
float2 point = frac(sin(dot(UV, float2(12.9898, 78.233))) * 43758.5453);

// ✅ 正确 - 使用其他名称
float2 randPoint = frac(sin(dot(UV, float2(12.9898, 78.233))) * 43758.5453);


```

##一定不要出现的错误

1. Custom Function下File无法解决的，String跟不可能解决，你应该先检查File的文件内容，而不是改成string

2. 如何遇到File无法解决的问题，向用户通报并寻求解决方案，不要私自做决定把方案推翻或者使用String
