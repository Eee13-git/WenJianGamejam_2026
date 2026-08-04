

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-08-04 19:58:48] 项目所有预制体(SpriteRenderer)已从 Sprites/Default 材质批量修复为 Sprite-Lit-Default (Assets/Materials/SpriteLit.mat)，以支持 URP 2D 光照。新增预制体务必使用 Sprite-Lit-Default 材质，否则不响应 2D 灯光。
- [2026-08-04 19:58:48] 2D 场景相机需要 z=-10 才能正常渲染。RoomCameraController.LateUpdate 会锁定 transform.position 到 _targetPosition 但保留 z 轴，因此场景中相机的初始 z 值很关键。
- [2026-08-04 20:16:34] ShaderGraph-AI 扩展包 (cn.tuanjie.shadergraph-mcptools) 的 MCPtools.GraphSettings.cs 曾因缺少条件编译守卫导致 77 个编译错误（HDRP 类型在 URP-only 项目中不可用）。已修复：asmdef 添加 versionDefines（URP_INSTALLED/HDRP_INSTALLED），代码中所有 HDRP/URP 专用段用 #if 守卫。注意 #region/#endregion 必须在 #if/#endif 外面，否则 false 分支时 #endregion 被跳过导致 CS1027。

### Reference

