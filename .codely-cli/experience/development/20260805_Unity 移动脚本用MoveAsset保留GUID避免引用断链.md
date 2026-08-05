# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: 移动 C# 脚本文件到新文件夹，同时保持资产引用不丢失
- **技术栈**: Unity / C# / AssetDatabase

## 问题描述

### 问题表现
手动"新建文件 + 删除旧文件"移动脚本后，引用该脚本的 .asset / prefab（如 ScriptableObject 的 `m_Script` 字段）变成"Missing (Mono Script)"，资产数据丢失。原因是新文件的 .meta GUID 与旧文件不同，而资产按 GUID 引用脚本。

### 触发条件
- 把脚本从一个文件夹移到另一个文件夹（如从 ConcreteSkill/ 移到 Effects/）
- 已有 .asset 资产引用了该脚本类型（SkillEffect 资产、MonoBehaviour 预制体等）

## 解决方案

### 关键步骤
1. 使用 `AssetDatabase.MoveAsset(source, destination)` 移动，它会连带移动 .meta 文件
2. 验证：移动后 `AssetDatabase.AssetPathToGUID(newPath)` 应等于移动前的 GUID
3. 在资产中读取 `m_Script` 引用的 GUID 与新位置 GUID 对比确认一致

### 关键代码/命令
```csharp
// ✅ 正确：MoveAsset 保留 GUID
string result = AssetDatabase.MoveAsset(
    "Assets/Scripts/Skill/ConcreteSkill/X.cs",
    "Assets/Scripts/Skill/Effects/X.cs");
if (string.IsNullOrEmpty(result))
    Debug.Log("Moved, GUID preserved");

// 验证引用链
string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Skill/Effects/X.cs");
var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(effectPath);
var sp = new SerializedObject(so).FindProperty("m_Script");
string refGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sp.objectReferenceValue));
Debug.Log($"match: {guid == refGuid}"); // 应输出 True
```

### 最终方案
文件移动一律用 `AssetDatabase.MoveAsset`（或编辑器右键 Move），GUID 跟随 .meta 不变，资产引用不断链。避免手动剪切/粘贴后重建 .meta 导致 GUID 变化。类名保持不变时，资产无需任何修改。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
