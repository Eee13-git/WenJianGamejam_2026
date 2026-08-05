# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: 修改 prefab 中嵌套子对象上的组件属性（如子物体上的 Light2D、ParticleSystem）
- **技术栈**: Unity / C# / AssetDatabase

## 问题描述

### 问题表现
`unity_asset.modify` 对 prefab 资产修改组件属性时报 `"No applicable or modifiable properties found"`，即使用正确的组件类型名也不生效。但修改 prefab 根对象上的组件属性可以成功。

### 触发条件
- 目标组件挂在 prefab 的**子对象**上（如 `Player/PlayerLight` 上的 `Light2D`）
- 用资产修改工具直接按路径/prefab 修改，工具无法定位嵌套子对象组件

## 解决方案

### 关键步骤
1. 改用 C# 脚本（Editor 模式）操作
2. `AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)` 加载预制体
3. `GetComponentsInChildren<T>(true)` 获取所有嵌套子对象的目标组件
4. 修改属性后对组件和根对象都调用 `EditorUtility.SetDirty()`，最后 `AssetDatabase.SaveAssets()`

### 关键代码/命令
```csharp
// ❌ 错误：资产修改工具直接按 prefab 路径改子对象组件 → "No applicable properties"
// ✅ 正确：C# 脚本遍历子对象
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
var lights = go.GetComponentsInChildren<Light2D>(true); // 含子对象、含未激活
foreach (var l in lights)
{
    l.intensity = 0.12f;
    EditorUtility.SetDirty(l);
}
EditorUtility.SetDirty(go);
AssetDatabase.SaveAssets();
```

### 最终方案
嵌套子对象的 prefab 属性修改统一走 C# 脚本：`LoadAssetAtPath → GetComponentsInChildren → SetDirty → SaveAssets`。`true` 参数确保包含未激活子对象。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
