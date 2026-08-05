# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: 通过 Unity 桥接脚本工具做编辑器自动化（改资产、跑测试）
- **技术栈**: Unity 2022.3 / C# Roslyn Scripting

## 问题描述

### 问题表现
两个常见失败：
1. 在脚本顶层写 `yield return null;`（想等一帧再继续）报 `CS7020: Cannot use 'yield' in top-level script code`
2. 给 ScriptableObject 资产设置**新加的字段**时报 `CS1061: does not contain a definition for 'X'`，即使 C# 文件已更新

### 触发条件
- 一次性自动化脚本需要在帧间等待（如生成敌人后触发技能、录制多帧）
- 修改了类的字段后，未等 Unity 编译完成就立即用脚本访问该字段

## 解决方案

### 关键步骤
1. 顶层不能用 yield → 定义 `IEnumerator Run()` 方法并 `return Run();`，让桥接框架调度为协程；或把多帧逻辑拆成多次脚本调用
2. 新字段访问报 CS1061 → 先触发编译（编译管线），确认编译完成、控制台无错误后，再执行设置资产的脚本
3. 脚本执行模式与编辑器状态要匹配：改资产的脚本用 editor 模式，录制/触发的脚本用 play 模式

### 关键代码/命令
```csharp
// ❌ 错误：顶层 yield
yield return null;

// ✅ 正确：返回 IEnumerator 协程
using System.Collections;
IEnumerator Run()
{
    yield return null;
    // ... 帧间逻辑
    return;
}
return Run();
```

### 最终方案
一次性自动化脚本遵循"顶层无阻塞"原则：需要跨帧用 `IEnumerator Run()` + `return Run()`；访问新代码成员前必须先完成编译。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
