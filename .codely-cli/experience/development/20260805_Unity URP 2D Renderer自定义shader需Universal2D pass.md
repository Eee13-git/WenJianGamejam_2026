# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: URP 2D 项目中使用自定义 shader 渲染 LineRenderer/非 Sprite 物体
- **技术栈**: Unity 2022.3 / URP 14 / HLSL

## 问题描述

### 问题表现
自定义 HLSL shader 材质引用正常、编译无错误，但 LineRenderer 渲染的线条在场景中完全不可见（同样的材质用在 SpriteRenderer 上正常）。纯色输出（`return half4(0.3,1,1,1)*2`）也不可见，排除计算逻辑问题。

### 触发条件
- 项目使用 URP 2D Renderer（`UniversalRP.asset` + `Renderer2D.asset`）
- shader 只写了 `LightMode="UniversalForward"` 的 pass
- 渲染对象是 LineRenderer（或其他非 Sprite 渲染器）

## 解决方案

### 关键步骤
1. 用对比实验定位：同场景创建 3 个 LineRenderer，分别用自定义 shader、`Sprites/Default`、`Universal Render Pipeline/Particles/Unlit`——后两者可见，自定义不可见 → 确认是 shader pass 缺失
2. 给 shader 增加第二个 pass，`Tags { "LightMode" = "Universal2D" }`
3. 两个 pass 使用相同的 vert/frag 逻辑即可

### 关键代码/命令
```hlsl
// ❌ 错误写法：只有 UniversalForward pass（URP 2D Renderer 不渲染）
Pass
{
    Name "ForwardLit"
    Tags { "LightMode" = "UniversalForward" }
    ...
}

// ✅ 正确写法：追加 Universal2D pass
Pass
{
    Name "ForwardLit"
    Tags { "LightMode" = "UniversalForward" }
    ...
}
Pass
{
    Name "ForwardLit2D"
    Tags { "LightMode" = "Universal2D" }
    // 相同 HLSLPROGRAM
    ...
}
```

### 最终方案
URP 2D Renderer 用 `Universal2D` 而不是 `UniversalForward` 渲染非 Sprite 渲染器。为兼容 2D/3D 管线，自定义 shader 应同时提供两个 pass。判断项目是否为 2D Renderer：检查 `Assets/Settings/` 下的 Renderer 资产是否为 `Renderer2D`。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
