# 程序化闪电弧（Procedural Lightning Arc）

## 用途
无预制体、纯代码生成锯齿状闪电弧，适合连锁闪电、电弧、能量传导等技能特效。

## 核心思路
职责分离：**几何锯齿走 LineRenderer 顶点，发光质感走自定义 shader**。

## 组件代码
```csharp
using UnityEngine;

/// 闪电弧运行时：按间隔刷新锯齿顶点 + 整体淡出
public class LightningArcRuntime : MonoBehaviour
{
    private LineRenderer _lr;
    private Vector3 _from, _to;
    private int _segments;
    private float _jitter, _duration;
    private int _refreshInterval = 4; // 帧节流：减少每帧跳动
    private int _frameCounter;
    private float _elapsed;

    public void Init(LineRenderer lr, Vector3 from, Vector3 to,
        int segments, float jitter, float duration)
    {
        _lr = lr; _from = from; _to = to;
        _segments = Mathf.Max(2, segments);
        _jitter = jitter; _duration = Mathf.Max(0.05f, duration);
        _frameCounter = 0; _elapsed = 0f;
        RefreshPoints();
    }

    private void Update()
    {
        if (_lr == null) return;
        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);
        if (t < 1f && ++_frameCounter >= _refreshInterval)
        {
            _frameCounter = 0;
            RefreshPoints();
        }
    }

    private void RefreshPoints()
    {
        Vector3 dir = (_to - _from).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        _lr.SetPosition(0, _from);
        _lr.SetPosition(_segments, _to);
        for (int i = 1; i < _segments; i++)
        {
            float t = (float)i / _segments;
            Vector3 p = Vector3.Lerp(_from, _to, t);
            p += perp * (Random.value - 0.5f) * 2f * _jitter;
            _lr.SetPosition(i, p);
        }
    }
}
```

## 生成弧的工厂方法
```csharp
GameObject SpawnArc(Vector3 from, Vector3 to, Material mat,
    int segments, float jitter, float width, float duration)
{
    var go = new GameObject("LightningArc");
    var lr = go.AddComponent<LineRenderer>();
    lr.useWorldSpace = true;
    lr.positionCount = segments + 1;
    lr.startWidth = width;
    lr.endWidth = width * 0.6f;
    lr.numCornerVertices = 2;
    lr.numCapVertices = 2;
    lr.sortingOrder = 25;
    lr.material = mat;
    go.AddComponent<LightningArcRuntime>().Init(lr, from, to, segments, jitter, duration);
    Destroy(go, duration); // 自动清理
    return go;
}
```

## Shader 要点（URP 2D 兼容）
- 加法混合：`Blend SrcAlpha One`、`ZWrite Off`
- **必须提供 `Universal2D` pass**（URP 2D Renderer 不渲染仅 `UniversalForward` 的 pass）
- 首尾淡出用 `uv.x`：`smoothstep(0,0.15,uv.x) * smoothstep(1,0.85,uv.x)`
- 沿线闪烁：`1 - _FlickerStrength * noise2D(float2(uv.x*6, _Time.y*_FlickerSpeed))`

## 关键参数
| 参数 | 作用 |
|------|------|
| segments | 锯齿段数（8 左右自然） |
| jitter | 抖动幅度（0.2~0.4） |
| refreshInterval | 帧节流（4 = 每秒约15次刷新，稳而活） |
| width | 线条宽度（0.06 纤细，0.12 粗壮） |

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
