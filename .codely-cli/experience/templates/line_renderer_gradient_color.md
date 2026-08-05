# LineRenderer 颜色渐变（中间亮白两端边缘色）

## 用途
让 LineRenderer 线条沿长度方向呈现"中间亮、两端暗/彩色"的渐变，例如闪电白热核心 + 两端青蓝光晕。

## 核心思路
用 `LineRenderer.colorGradient`（Gradient 色标曲线）替代 `startColor/endColor`。色标支持多个颜色键 + 透明度键，可精确控制渐变位置。

## 代码
```csharp
using UnityEngine;

// 中间亮白、两端 edgeColor（如青蓝），整体 alpha 随时间衰减
void ApplyGradient(LineRenderer lr, Color edgeColor, float alpha)
{
    var grad = new Gradient();
    grad.SetKeys(
        new[]
        {
            new GradientColorKey(edgeColor, 0f),     // 起点：边缘色
            new GradientColorKey(Color.white, 0.5f), // 中间：亮白
            new GradientColorKey(edgeColor, 1f),     // 终点：边缘色
        },
        new[]
        {
            new GradientAlphaKey(alpha, 0f),
            new GradientAlphaKey(alpha, 0.5f),
            new GradientAlphaKey(alpha * 0.6f, 1f),  // 端部略透明
        });
    lr.colorGradient = grad;
}
```

## 注意
- `startColor/endColor` 与 `colorGradient` 互斥：设置了 Gradient 后不要再设 start/endColor，否则被覆盖
- 每帧重建 Gradient 开销很小（键数量少），适合做整体淡出动画：固定色标位置，只改 alpha 键
- 透明度键要单独设置，不能只靠颜色键——`GradientColorKey` 不含 alpha
- 端点 alpha 比中部低（×0.6）配合 shader 的 uv.x 首尾淡出，可让两端呈柔和光晕

## 渐变位置调整
- 白热区宽度：把白色键位置从 0.5f 移到 0.3f~0.7f（白区变宽/变窄）
- 更多颜色段：SetKeys 数组加键即可（如红→白→蓝三色）

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
