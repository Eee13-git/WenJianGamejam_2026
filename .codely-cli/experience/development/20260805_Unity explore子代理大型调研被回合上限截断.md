# 开发经验文档

## 基本信息
- **创建日期**: 2026-08-05
- **相关任务**: 大型代码库只读调研（explore 子代理）
- **技术栈**: Unity / Codely explore 子代理

## 问题描述

### 问题表现
给 explore 子代理派发"通读整个项目"的大型调研任务（读取所有脚本 + 全部 prefab + resources + 渲染管线设置），子代理在任务中途被终止：`Terminated due to MAX_TURNS`，最终调研报告被截断（`Result truncated`），只返回了开头部分，后半段架构分析丢失。

### 触发条件
- 调研范围过大：要求读取 10+ 个目录、几十个文件
- 单任务要求"读完全部再汇总"——信息在最后才输出，一旦截断全部丢失
- 子代理回合预算（MAX_TURNS）不足以支撑大范围遍历 + 最终报告撰写

## 解决方案

### 关键步骤
1. **大任务拆分**：把"全项目调研"按子系统拆成 3-5 个独立子任务（如：Player/Skill 系统一个、Enemy/Map 一个、UI/Item/Buff 一个），各自返回独立报告
2. **要求阶段性输出**：让子代理先输出"发现清单"（文件路径+关键类），确认有中间产物后再让其实质化，避免一次性长报告
3. **优先聚焦**: 明确"只需要 X 信息"而非"通读所有"，缩小 glob 范围（如只读 `Assets/Scripts/` 排除 `Assets/Resources/` 下资产）
4. **预算提醒提前收尾**: 日志出现 `[Budget WARNING] Turns remaining: 10` 时，子代理应停止新搜索、立即汇编已收集内容

### 关键代码/命令
```markdown
# ❌ 错误：单任务贪大
"Read ALL C# scripts, ALL prefabs, ALL resources, check URP settings.
 Report back complete class hierarchy, all prefab components, map system,
 player, enemy, bullet, skill, buff, item, UI, currency, damage popup..."

# ✅ 正确：拆分 + 明确范围 + 要求中间产物
Task1: "只读 Assets/Scripts/Skill/ 和 Assets/Scripts/Player/，
 输出 SkillEffectBase 全文 + PlayerSkillManager 类签名。
 先列文件清单，再给关键代码。"
Task2: "只读 Assets/Prefabs/，列出每个 prefab 的组件列表即可，
 不需要读 prefab YAML 内容。"
```

### 最终方案
大型调研按子系统拆分为多个并行子任务，每个子任务范围明确、要求先输出"文件清单"这类中间产物。子代理收到预算警告后立即停止搜索开始汇编。这样即使单个子任务被截断，损失也只限于该子系统，不会丢失全项目级结论。

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: 2026-08-05
