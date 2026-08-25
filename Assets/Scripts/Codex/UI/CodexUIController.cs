using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 图鉴 UI 控制器 — 基于 CodexPanel 预制体。
/// 仿 TechTreeUIController：静态 Show/Hide/Toggle，首次调用自动实例化。
///
/// 界面流程:
///   主界面（三按钮） → 点击分类 → 列表+详情界面 → 返回主界面
/// </summary>
public class CodexUIController : MonoBehaviour
{
    // ========== 主界面 ==========
    [SerializeField] private GameObject _categoryView;
    [SerializeField] private Button _enemyButton;
    [SerializeField] private Button _skillButton;
    [SerializeField] private Button _itemButton;
    [SerializeField] private TMP_Text _enemyCountText;
    [SerializeField] private TMP_Text _skillCountText;
    [SerializeField] private TMP_Text _itemCountText;

    // ========== 列表+详情界面 ==========
    [SerializeField] private GameObject _listView;
    [SerializeField] private Button _backButton;
    [SerializeField] private Transform _listContainer;
    [SerializeField] private GameObject _slotPrefab;

    // ========== 详情面板 ==========
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailDescription;
    [SerializeField] private GameObject _detailLockedView;
    [SerializeField] private Transform _statsContainer;
    [SerializeField] private TMP_Text _statsLabelPrefab;

    // ========== 通用 ==========
    [SerializeField] private Button _closeButton;

    private static CodexUIController _instance;
    private CodexSO _currentCodex;
    private CodexEntrySO _selectedEntry;

    private readonly List<GameObject> _spawnedSlots = new();
    private readonly List<GameObject> _spawnedStats = new();

    // ========== 静态接口 ==========

    public static void Show()
    {
        var inst = GetInstance();
        if (inst == null) return;
        inst.gameObject.SetActive(true);
        inst.ShowCategoryView();
    }

    public static void Hide()
    {
        if (_instance != null)
            _instance.gameObject.SetActive(false);
    }

    public static void Toggle()
    {
        var inst = GetInstance();
        if (inst == null) return;
        if (inst.gameObject.activeSelf)
            Hide();
        else
            Show();
    }

    private static CodexUIController GetInstance()
    {
        if (_instance != null) return _instance;
        _instance = FindObjectOfType<CodexUIController>(true);
        if (_instance == null)
        {
            var prefab = Resources.Load<GameObject>("Codex/CodexPanel");
            if (prefab == null)
            {
                Debug.LogError("[CodexUIController] 预制体未找到: Resources/Codex/CodexPanel");
                return null;
            }
            var go = Instantiate(prefab);
            go.name = "CodexPanel";

            // 查找场景 Canvas；若无则创建专用 Canvas
            Canvas rootCanvas = FindObjectOfType<Canvas>();
            if (rootCanvas != null)
            {
                go.transform.SetParent(rootCanvas.transform, false);
            }
            else
            {
                var canvasGo = new GameObject("CodexCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
                go.transform.SetParent(canvasGo.transform, false);
            }

            _instance = go.GetComponent<CodexUIController>();
        }
        return _instance;
    }

    // ========== 生命周期 ==========

    private void Awake()
    {
        _instance = this;

        if (_enemyButton != null)
            _enemyButton.onClick.AddListener(() => ShowList(CodexManager.Instance?.EnemyCodex));
        if (_skillButton != null)
            _skillButton.onClick.AddListener(() => ShowList(CodexManager.Instance?.SkillCodex));
        if (_itemButton != null)
            _itemButton.onClick.AddListener(() => ShowList(CodexManager.Instance?.ItemCodex));
        if (_backButton != null)
            _backButton.onClick.AddListener(ShowCategoryView);
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
    }

    // ========== 主界面 ==========

    private void ShowCategoryView()
    {
        if (_categoryView != null) _categoryView.SetActive(true);
        if (_listView != null) _listView.SetActive(false);

        var mgr = CodexManager.Instance;
        if (mgr == null) return;

        UpdateCount(_enemyCountText, mgr.EnemyCodex, mgr);
        UpdateCount(_skillCountText, mgr.SkillCodex, mgr);
        UpdateCount(_itemCountText, mgr.ItemCodex, mgr);
    }

    private void UpdateCount(TMP_Text text, CodexSO codex, CodexManager mgr)
    {
        if (text == null || codex == null) return;
        int unlocked = mgr.GetUnlockedCount(codex);
        int total = codex.Entries.Count;
        text.text = $"{unlocked}/{total}";
    }

    // ========== 列表+详情界面 ==========

    private void ShowList(CodexSO codex)
    {
        if (codex == null) return;
        _currentCodex = codex;

        if (_categoryView != null) _categoryView.SetActive(false);
        if (_listView != null) _listView.SetActive(true);

        RefreshList();
        ShowLockedDetail();
    }

    private void RefreshList()
    {
        ClearSlots();

        if (_currentCodex == null || _listContainer == null || _slotPrefab == null) return;

        var mgr = CodexManager.Instance;
        foreach (var entry in _currentCodex.Entries)
        {
            if (entry == null) continue;

            bool unlocked = mgr != null && mgr.IsUnlocked(entry.entryId);
            var slotGo = Instantiate(_slotPrefab, _listContainer);
            _spawnedSlots.Add(slotGo);

            var slotUI = slotGo.GetComponent<CodexEntrySlotUI>();
            if (slotUI != null)
            {
                Sprite icon = unlocked ? GetEntryIcon(entry) : null;
                string name = unlocked ? entry.displayName : "???";
                slotUI.Setup(icon, name, unlocked);
            }

            var btn = slotGo.GetComponent<Button>();
            if (btn != null)
            {
                var captured = entry;
                btn.onClick.AddListener(() => OnSlotSelected(captured));
            }
        }
    }

    private void OnSlotSelected(CodexEntrySO entry)
    {
        _selectedEntry = entry;
        var mgr = CodexManager.Instance;
        bool unlocked = mgr != null && mgr.IsUnlocked(entry.entryId);

        if (!unlocked)
        {
            ShowLockedDetail();
            return;
        }

        ShowDetail(entry);
    }

    // ========== 详情面板 ==========

    private void ShowLockedDetail()
    {
        if (_detailLockedView != null) _detailLockedView.SetActive(true);
        if (_detailIcon != null) _detailIcon.gameObject.SetActive(false);
        if (_detailName != null) _detailName.text = "???";
        if (_detailDescription != null) _detailDescription.text = "尚未解锁";
        ClearStats();
    }

    private void ShowDetail(CodexEntrySO entry)
    {
        if (_detailLockedView != null) _detailLockedView.SetActive(false);
        if (_detailIcon != null)
        {
            _detailIcon.sprite = GetEntryIcon(entry);
            _detailIcon.gameObject.SetActive(_detailIcon.sprite != null);
        }
        if (_detailName != null)
        {
            _detailName.text = entry.displayName;
            _detailName.enableWordWrapping = true;
        }
        if (_detailDescription != null)
        {
            _detailDescription.text = entry.description;
            _detailDescription.enableWordWrapping = true;
            _detailDescription.overflowMode = TextOverflowModes.Overflow;
            _detailDescription.alignment = TextAlignmentOptions.TopLeft;
        }

        ClearStats();

        // 敌人额外显示属性
        if (entry is CodexEnemyEntrySO enemyEntry && enemyEntry.enemyConfig != null)
        {
            // 有属性表时：Description 区域上移，StatsContainer 扩大
            if (_detailDescription != null)
            {
                var descRT = _detailDescription.GetComponent<RectTransform>();
                descRT.anchorMin = new Vector2(0f, 1f);
                descRT.anchorMax = new Vector2(1f, 1f);
                descRT.pivot = new Vector2(0.5f, 1f);
                descRT.sizeDelta = new Vector2(-20f, 180f);
                descRT.anchoredPosition = new Vector2(0f, -140f);
            }
            if (_statsContainer != null)
            {
                var scRT = _statsContainer.GetComponent<RectTransform>();
                scRT.anchorMin = new Vector2(0f, 0f);
                scRT.anchorMax = new Vector2(1f, 0f);
                scRT.pivot = new Vector2(0.5f, 0f);
                scRT.sizeDelta = new Vector2(-20f, 220f);
                scRT.anchoredPosition = new Vector2(0f, 10f);
            }
            ShowEnemyStats(enemyEntry.enemyConfig);
        }
        else
        {
            // 非敌人条目：Description 填满，StatsContainer 收起
            if (_detailDescription != null)
            {
                var descRT = _detailDescription.GetComponent<RectTransform>();
                descRT.anchorMin = new Vector2(0f, 0f);
                descRT.anchorMax = new Vector2(1f, 1f);
                descRT.pivot = new Vector2(0.5f, 0.5f);
                descRT.sizeDelta = new Vector2(-20f, -150f);
                descRT.anchoredPosition = new Vector2(0f, -70f);
            }
            if (_statsContainer != null)
            {
                var scRT = _statsContainer.GetComponent<RectTransform>();
                scRT.sizeDelta = new Vector2(-20f, 60f);
            }
        }
    }

    private void ShowEnemyStats(EnemyConfig cfg)
    {
        if (_statsContainer == null) return;

        var stats = new (string, float)[]
        {
            ("生命值", cfg.maxHealth),
            ("碰撞伤害", cfg.contactDamage),
            ("碰撞冷却(秒)", cfg.contactDamageCooldown),
            ("巡逻速度", cfg.patrolSpeed),
            ("追击速度", cfg.chaseSpeed),
            ("感知范围", cfg.detectionRange),
            ("攻击范围", cfg.attackRange),
        };

        int index = 0;
        foreach (var (label, value) in stats)
        {
            var go = Instantiate(_statsLabelPrefab?.gameObject, _statsContainer);
            _spawnedStats.Add(go);

            // 确保每个属性行有足够高度
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 28f);
                rt.anchoredPosition = new Vector2(0f, -index * 30f);
            }

            var text = go.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.text = $"{label}: {value}";
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.alignment = TextAlignmentOptions.Left;
                text.fontSize = 18;
            }
            index++;
        }
    }

    // ========== 辅助 ==========

    private Sprite GetEntryIcon(CodexEntrySO entry)
    {
        return entry switch
        {
            CodexEnemyEntrySO e => e.icon,
            CodexSkillEntrySO s => s.skillData?.icon,
            CodexItemEntrySO i => i.itemData?.icon,
            _ => null
        };
    }

    private void ClearSlots()
    {
        foreach (var go in _spawnedSlots)
        {
            if (go != null) Destroy(go);
        }
        _spawnedSlots.Clear();
    }

    private void ClearStats()
    {
        foreach (var go in _spawnedStats)
        {
            if (go != null) Destroy(go);
        }
        _spawnedStats.Clear();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
