using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 随从 UI 构建脚本（Editor Only）：
/// 1. FollowerAvatarView.prefab / FollowerSlotView.prefab / SkillEquipSlotView.prefab（行项）
/// 2. FollowerSlotPopup.prefab / SkillEquipSlotPopup.prefab（独立 Canvas 弹窗）
/// 3. 将两个弹窗以 PrefabInstance 挂入 UICanvas.prefab 的 SkillUI/Popups 下
/// 4. 在 UICanvas.prefab 中创建 FollowerPanel（StatsPanel 下方）
/// 菜单：Tools/Follower UI/Build All
/// </summary>
public static class FollowerUIBuilder
{
    private const string UiCanvasPath = "Assets/Prefabs/UI/UICanvas.prefab";
    private const string AvatarViewPath = "Assets/Prefabs/UI/Skill/FollowerAvatarView.prefab";
    private const string FollowerSlotViewPath = "Assets/Prefabs/UI/Skill/FollowerSlotView.prefab";
    private const string EquipSlotViewPath = "Assets/Prefabs/UI/Skill/SkillEquipSlotView.prefab";
    private const string FollowerSlotPopupPath = "Assets/Prefabs/UI/Skill/FollowerSlotPopup.prefab";
    private const string EquipSlotPopupPath = "Assets/Prefabs/UI/Skill/SkillEquipSlotPopup.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static TMP_FontAsset _font;

    private static TMP_FontAsset FontAsset
    {
        get
        {
            if (_font == null)
                _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            return _font;
        }
    }

    // ═══════════════ 菜单入口 ═══════════════

    [MenuItem("Tools/Follower UI/Build All")]
    public static void BuildAll()
    {
        BuildFollowerAvatarViewPrefab();
        BuildFollowerSlotViewPrefab();
        BuildSkillEquipSlotViewPrefab();
        BuildFollowerSlotPopupPrefab();
        BuildSkillEquipSlotPopupPrefab();
        NestPopupsIntoUiCanvas();
        BuildFollowerPanelInUiCanvas();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FollowerUIBuilder] 全部构建完成");
    }

    [MenuItem("Tools/Follower UI/Build FollowerPanel (in UICanvas)")]
    public static void BuildPanelMenu() => BuildFollowerPanelInUiCanvas();

    [MenuItem("Tools/Follower UI/Build Popups & Nest into UICanvas")]
    public static void BuildPopupsMenu() => NestPopupsIntoUiCanvas();

    // ═══════════════ 行项 prefab ═══════════════

    /// <summary>FollowerAvatarView.prefab —— 随从列表行（头像 + 名称Lv + 技能冷却图标组）</summary>
    public static void BuildFollowerAvatarViewPrefab()
    {
        DeleteAssetIfExists(AvatarViewPath);

        var root = NewUI("FollowerAvatarView", null, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(236, 52));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        // 行根透明热区：接收整行悬停（FollowerAvatarView 的 IPointerEnter/Exit），
        // 子元素（头像/文本）raycastTarget=false 后悬停命中这里
        AddBg(root, new Color(0f, 0f, 0f, 0f), true);

        // 头像（悬停由行根热区统一接收，头像本身不拦截射线）
        var avatar = NewUI("Avatar", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4, 4), new Vector2(44, 44));
        var avatarImg = AddBg(avatar, new Color(0.1f, 0.1f, 0.12f, 0.9f), false);

        // 名称：pivot(0,0.5) 左缘对齐，放在头像右侧，避免文字画到头像上
        var nameObj = NewUI("NameText", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32, 0), new Vector2(88, 16));
        nameObj.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        var nameText = AddTMP(nameObj, "", 12, TextAlignmentOptions.Left, Color.white);

        // 技能图标根（右侧）+ 横向布局
        var skillRoot = NewUI("SkillRoot", root.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4, 4), new Vector2(116, 30));
        var hLayout = skillRoot.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 2f;
        hLayout.childAlignment = TextAnchor.MiddleLeft;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        // 技能图标模板（默认隐藏）
        var item = NewUI("SkillItemTemplate", skillRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
        var itemIcon = NewUI("Icon", item.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddBg(itemIcon, Color.white, false);
        var itemOverlay = NewUI("Overlay", item.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var overlayImg = AddBg(itemOverlay, new Color(0f, 0f, 0f, 0.55f), false);
        overlayImg.type = Image.Type.Filled;
        overlayImg.fillMethod = Image.FillMethod.Horizontal;
        overlayImg.fillOrigin = 0;
        var itemCd = NewUI("CdText", item.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddTMP(itemCd, "", 9, TextAlignmentOptions.Center, Color.white);
        item.SetActive(false);

        // 脚本 + 绑定字段
        var view = root.AddComponent<FollowerAvatarView>();
        var so = new SerializedObject(view);
        so.FindProperty("_avatar").objectReferenceValue = avatarImg;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_skillRoot").objectReferenceValue = skillRoot.transform;
        so.FindProperty("_skillItemTemplate").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, AvatarViewPath);
    }

    /// <summary>FollowerSlotView.prefab —— 同化槽位弹窗行（头像 + 名称Lv + 升级/替换徽标）</summary>
    public static void BuildFollowerSlotViewPrefab()
    {
        DeleteAssetIfExists(FollowerSlotViewPath);

        var root = NewUI("FollowerSlotView", null, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(460, 56));
        var bg = AddBg(root, new Color(0.12f, 0.12f, 0.15f, 0.9f), true);
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;

        var avatar = NewUI("Avatar", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8, 0), new Vector2(44, 44));
        var avatarImg = AddBg(avatar, Color.white, false);

        // 名称：pivot(0,0.5) 左缘对齐，避免 Left 对齐文字从负坐标开始画到头像上
        var nameObj = NewUI("NameText", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40, 0), new Vector2(360, 24));
        nameObj.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        var nameText = AddTMP(nameObj, "", 16, TextAlignmentOptions.Left, Color.white);

        var badgeObj = NewUI("BadgeText", root.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8, 0), new Vector2(70, 24));
        var badgeText = AddTMP(badgeObj, "", 16, TextAlignmentOptions.Right, Color.white);

        var view = root.AddComponent<FollowerSlotView>();
        var so = new SerializedObject(view);
        so.FindProperty("_avatar").objectReferenceValue = avatarImg;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_badgeText").objectReferenceValue = badgeText;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, FollowerSlotViewPath);
    }

    /// <summary>SkillEquipSlotView.prefab —— 装配槽弹窗行（键位 + 图标 + 技能名 + 徽标）</summary>
    public static void BuildSkillEquipSlotViewPrefab()
    {
        DeleteAssetIfExists(EquipSlotViewPath);

        var root = NewUI("SkillEquipSlotView", null, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(460, 56));
        var bg = AddBg(root, new Color(0.12f, 0.12f, 0.15f, 0.9f), true);
        var btn = root.AddComponent<Button>();
        btn.targetGraphic = bg;

        var keyObj = NewUI("KeyText", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(40, 24));
        var keyText = AddTMP(keyObj, "Q", 18, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.4f, 1f));

        var iconObj = NewUI("Icon", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56, 0), new Vector2(40, 40));
        var iconImg = AddBg(iconObj, Color.white, false);

        // 名称：pivot(0,0.5) 左缘对齐，避免 Left 对齐文字从负坐标开始画到图标上
        var nameObj = NewUI("NameText", root.transform,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(88, 0), new Vector2(272, 24));
        nameObj.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        var nameText = AddTMP(nameObj, "", 16, TextAlignmentOptions.Left, Color.white);

        var badgeObj = NewUI("BadgeText", root.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8, 0), new Vector2(60, 24));
        var badgeText = AddTMP(badgeObj, "", 16, TextAlignmentOptions.Right, Color.white);

        var view = root.AddComponent<SkillEquipSlotView>();
        var so = new SerializedObject(view);
        so.FindProperty("_keyText").objectReferenceValue = keyText;
        so.FindProperty("_icon").objectReferenceValue = iconImg;
        so.FindProperty("_nameText").objectReferenceValue = nameText;
        so.FindProperty("_badgeText").objectReferenceValue = badgeText;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, EquipSlotViewPath);
    }

    // ═══════════════ 弹窗 prefab ═══════════════

    /// <summary>FollowerSlotPopup.prefab —— 同化槽位选择弹窗</summary>
    public static void BuildFollowerSlotPopupPrefab()
    {
        BuildFollowerSlotViewPrefab();
        DeleteAssetIfExists(FollowerSlotPopupPath);

        var (root, panel, container, titleText, cancelBtn, cancelText) =
            BuildPopupBase("FollowerSlotPopup", FollowerSlotPopupPath, "选择随从槽位", 540, 420, 480, 300);

        var slotViewPrefab = AssetDatabase.LoadAssetAtPath<FollowerSlotView>(FollowerSlotViewPath);
        var manager = root.AddComponent<FollowerSlotPopupManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_slotPrefab").objectReferenceValue = slotViewPrefab;
        so.FindProperty("_backdrop").objectReferenceValue = root.transform.Find("Backdrop").gameObject;
        so.FindProperty("_popupPanel").objectReferenceValue = panel;
        so.FindProperty("_slotContainer").objectReferenceValue = container;
        so.FindProperty("_titleText").objectReferenceValue = titleText;
        so.FindProperty("_cancelText").objectReferenceValue = cancelText;
        so.FindProperty("_cancelButton").objectReferenceValue = cancelBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, FollowerSlotPopupPath);
    }

    /// <summary>SkillEquipSlotPopup.prefab —— 吞噬装配槽选择弹窗</summary>
    public static void BuildSkillEquipSlotPopupPrefab()
    {
        BuildSkillEquipSlotViewPrefab();
        DeleteAssetIfExists(EquipSlotPopupPath);

        var (root, panel, container, titleText, cancelBtn, cancelText) =
            BuildPopupBase("SkillEquipSlotPopup", EquipSlotPopupPath, "选择装配槽位", 540, 360, 480, 260);

        var slotViewPrefab = AssetDatabase.LoadAssetAtPath<SkillEquipSlotView>(EquipSlotViewPath);
        var manager = root.AddComponent<SkillEquipSlotPopupManager>();
        var so = new SerializedObject(manager);
        so.FindProperty("_slotPrefab").objectReferenceValue = slotViewPrefab;
        so.FindProperty("_backdrop").objectReferenceValue = root.transform.Find("Backdrop").gameObject;
        so.FindProperty("_popupPanel").objectReferenceValue = panel;
        so.FindProperty("_slotContainer").objectReferenceValue = container;
        so.FindProperty("_titleText").objectReferenceValue = titleText;
        so.FindProperty("_cancelText").objectReferenceValue = cancelText;
        so.FindProperty("_cancelButton").objectReferenceValue = cancelBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, EquipSlotPopupPath);
    }

    /// <summary>创建独立 Canvas 弹窗骨架（Backdrop + PopupPanel + Title + SlotContainer + CancelButton）</summary>
    private static (GameObject root, GameObject panel, Transform container, Text titleText, Button cancelBtn, Text cancelText)
        BuildPopupBase(string name, string path, string title, float panelW, float panelH, float slotW, float slotH)
    {
        var root = NewUI(name, null, Vector2.zero, Vector2.zero, Vector2.zero,
            new Vector2(1920, 1080));

        var canvas = root.AddComponent<Canvas>();
        // 关键：构建时用 WorldSpace 渲染模式，避免 ScreenSpaceOverlay 根画布在
        // 无窗口编辑环境下保存/导入时被按屏幕(0尺寸)重驱动为 scale=0 size=0。
        // 运行时该弹窗作为 UICanvas 的子画布，renderMode 会被父画布覆盖，不受影响。
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        root.AddComponent<GraphicRaycaster>();

        // Backdrop
        var backdrop = NewUI("Backdrop", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddBg(backdrop, new Color(0f, 0f, 0f, 0.7f), true);
        backdrop.SetActive(false);

        // PopupPanel
        var panel = NewUI("PopupPanel", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, panelH));
        AddBg(panel, new Color(0.08f, 0.08f, 0.1f, 0.95f), true);

        // Title
        var titleObj = NewUI("Title", panel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(panelW - 20, 40));
        var titleText = AddLegacyText(titleObj, title, 22);

        // SlotContainer
        var container = NewUI("SlotContainer", panel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(slotW, slotH));
        var vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 8f;
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = false;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = false;
        vLayout.childForceExpandHeight = false;

        // CancelButton
        var cancel = NewUI("CancelButton", panel.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(120, 36));
        var cancelBg = AddBg(cancel, new Color(0.2f, 0.2f, 0.2f, 1f), true);
        var cancelBtn = cancel.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelBg;
        var cancelTextObj = NewUI("Text", cancel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var cancelText = AddLegacyText(cancelTextObj, "取消", 16);

        // 关键：设置 ScreenSpaceOverlay 时 Unity 会按当前屏幕重驱动根 Canvas 的 RectTransform
        //（无窗口/后台编辑环境下会得到 scale=0 size=0），必须在最后强制恢复根 RectTransform。
        var rootRt = (RectTransform)root.transform;
        rootRt.localScale = Vector3.one;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.zero;
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = new Vector2(1920, 1080);
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        return (root, panel, container.transform, titleText, cancelBtn, cancelText);
    }

    // ═══════════════ 挂入 UICanvas ═══════════════

    /// <summary>将两个弹窗以 PrefabInstance 挂入 UICanvas.prefab 的 SkillUI/Popups 下</summary>
    public static void NestPopupsIntoUiCanvas()
    {
        BuildFollowerSlotPopupPrefab();
        BuildSkillEquipSlotPopupPrefab();

        var root = PrefabUtility.LoadPrefabContents(UiCanvasPath);
        var popups = root.transform.Find("SkillUI/Popups");
        if (popups == null)
        {
            Debug.LogError("[FollowerUIBuilder] 未找到 SkillUI/Popups 容器");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // 移除旧实例
        var old1 = popups.Find("FollowerSlotPopup");
        if (old1 != null) Object.DestroyImmediate(old1.gameObject);
        var old2 = popups.Find("SkillEquipSlotPopup");
        if (old2 != null) Object.DestroyImmediate(old2.gameObject);

        var p1 = AssetDatabase.LoadAssetAtPath<GameObject>(FollowerSlotPopupPath);
        var p2 = AssetDatabase.LoadAssetAtPath<GameObject>(EquipSlotPopupPath);
        if (p1 != null)
        {
            var inst1 = (GameObject)PrefabUtility.InstantiatePrefab(p1, popups);
            inst1.name = "FollowerSlotPopup";
            FixNestedPopupRect(inst1);
        }
        if (p2 != null)
        {
            var inst2 = (GameObject)PrefabUtility.InstantiatePrefab(p2, popups);
            inst2.name = "SkillEquipSlotPopup";
            FixNestedPopupRect(inst2);
        }

        PrefabUtility.SaveAsPrefabAsset(root, UiCanvasPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[FollowerUIBuilder] 弹窗已挂入 UICanvas/Popups");
    }

    /// <summary>嵌套进 UICanvas 时强制弹窗根 RectTransform 为全屏 stretch（覆盖父画布，保证居中）</summary>
    private static void FixNestedPopupRect(GameObject popupRoot)
    {
        var rt = (RectTransform)popupRoot.transform;
        rt.localScale = Vector3.one;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>在 UICanvas.prefab 中创建 FollowerPanel（StatsPanel 下方）</summary>
    public static void BuildFollowerPanelInUiCanvas()
    {
        BuildFollowerAvatarViewPrefab();

        var rowPrefab = AssetDatabase.LoadAssetAtPath<FollowerAvatarView>(AvatarViewPath);
        var root = PrefabUtility.LoadPrefabContents(UiCanvasPath);

        // 移除旧 FollowerPanel
        var old = root.transform.Find("FollowerPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 面板：anchor(0,1) pos(20,-370)，位于 StatsPanel(20,-150,230x210) 下方
        var panel = NewUI("FollowerPanel", root.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20, -370), new Vector2(240, 200));
        panel.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        AddBg(panel, new Color(0.06f, 0.06f, 0.09f, 0.9f), true);

        // 标题
        var titleObj = NewUI("Title", panel.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8, -4), new Vector2(120, 20));
        var titleText = AddTMP(titleObj, "随从", 18, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f, 1f));

        // 行容器（Vertical Layout）
        var container = NewUI("Container", panel.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0, 0), new Vector2(-6, -26));
        var vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 2f;
        vLayout.childAlignment = TextAnchor.UpperLeft;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        // Tooltip（默认隐藏，位于面板右侧）
        var tooltip = NewUI("Tooltip", panel.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(248, -4), new Vector2(250, 210));
        tooltip.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        AddBg(tooltip, new Color(0.06f, 0.06f, 0.09f, 0.95f), false);
        var tooltipTextObj = NewUI("Text", tooltip.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12, -12));
        var tooltipText = AddTMP(tooltipTextObj, "", 14, TextAlignmentOptions.TopLeft, Color.white);
        tooltipText.enableWordWrapping = true;
        tooltip.SetActive(false);

        // 脚本 + 绑定字段
        var fp = panel.AddComponent<FollowerPanel>();
        var so = new SerializedObject(fp);
        so.FindProperty("_container").objectReferenceValue = container.GetComponent<RectTransform>();
        so.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
        so.FindProperty("_tooltip").objectReferenceValue = tooltip;
        so.FindProperty("_tooltipText").objectReferenceValue = tooltipText;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, UiCanvasPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("[FollowerUIBuilder] FollowerPanel 已创建");
    }

    // ═══════════════ 工具函数 ═══════════════

    private static void DeleteAssetIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    private static void SaveAsPrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static GameObject NewUI(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        if (parent != null)
            go.transform.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return go;
    }

    private static Image AddBg(GameObject go, Color color, bool raycastTarget)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    private static TMP_Text AddTMP(GameObject go, string text, int fontSize,
        TextAlignmentOptions align, Color color)
    {
        var t = go.AddComponent<TextMeshProUGUI>();
        if (FontAsset != null)
        {
            t.font = FontAsset;
            t.fontSharedMaterial = FontAsset.material;
        }
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        // 关键：文本不参与射线检测，让点击直接命中按钮/热区图形
        //（否则文本 raycastTarget=true 会拦截点击且部分事件路径不会冒泡到父级 Button）
        t.raycastTarget = false;
        return t;
    }

    private static Text AddLegacyText(GameObject go, string text, int fontSize)
    {
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) t.font = font;
        return t;
    }
}
