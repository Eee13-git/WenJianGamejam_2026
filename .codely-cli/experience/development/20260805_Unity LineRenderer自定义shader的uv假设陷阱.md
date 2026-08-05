# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: 自定义 shader 在 LineRenderer 上做程序化线条（闪电、光束等）
- **技术栈**: Unity 2022.3 / URP / HLSL

## 问题描述

### 问题表现
自定义 shader 的 frag 中用 `abs(uv.y - 0.5)` 假设"uv.y=0.5 是线条中心"来计算线条宽度/发光，结果整个线条 alpha≈0 不可见；但同一套噪声/颜色计算在 Quad/粒子等对象上正常。

### 触发条件
- shader 针对 LineRenderer 编写，依赖 `uv.y` 判断像素到"线条中心"的距离
- LineRenderer 的 UV 分布不像 Sprite 那样居中（uv.y 不保证 0.5 为中心，且顶点色/UV 由 Unity 自动生成，与期待不符）

## 解决方案

### 关键步骤
1. 诊断：frag 直接返回纯色（不依赖 uv）确认渲染通路正常 → 定位到 uv 假设错误
2. 重构：把"线条宽度/锯齿外形"交给**几何层**（LineRenderer 顶点位置）实现，shader 只负责颜色、闪烁、首尾淡出
3. 首尾淡出可用 `uv.x`（沿线条长度方向 0~1）实现，不要依赖 uv.y

### 关键代码/命令
```hlsl
// ❌ 错误写法：依赖 uv.y 假设线条中心
float distFromArc = abs(uv.y - 0.5 - arcCenter);
float alpha = smoothstep(_ArcWidth, 0.0, distFromArc);

// ✅ 正确写法：uv.x 沿线条长度，只做首尾淡出 + 闪烁
float edgeFade = smoothstep(0.0, 0.15, uv.x) * smoothstep(1.0, 0.85, uv.x);
float flicker = 1.0 - _FlickerStrength * noise2D(float2(uv.x * 6.0, _Time.y * _FlickerSpeed));
float alpha = saturate(edgeFade * flicker);
```

### 关键代码/命令（C# 侧：锯齿几何）
```csharp
// 锯齿外形由顶点生成，每帧（或按间隔）刷新
Vector3 dir = (_to - _from).normalized;
Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
for (int i = 1; i < segments; i++)
{
    float t = (float)i / segments;
    Vector3 point = Vector3.Lerp(_from, _to, t);
    float j = (Random.value - 0.5f) * 2f * jitter;
    point += perp * j;
    lr.SetPosition(i, point);
}
```

### 最终方案
程序化线条遵循"几何外形走顶点、发光质感走 shader"的职责分离。shader 避免对 LineRenderer uv.y 做中心假设。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
