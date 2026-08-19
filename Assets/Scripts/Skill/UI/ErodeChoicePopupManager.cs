using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 侵蚀二选一弹窗：时停后显示"吞噬"和"同化"两个选项。
/// 吞噬 → 打开技能夺取弹窗（SkillStealPopupManager）
/// 同化 → 将敌人转化为随从
/// </summary>
public class ErodeChoicePopupManager : MonoBehaviour
{
    public static ErodeChoicePopupManager Instance { get; private set; }

    [Header("弹窗 UI（预制体中预设）")]
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private Text _titleText;
    [SerializeField] private Button _devourButton;
    [SerializeField] private Text _devourText;
    [SerializeField] private Button _assimilateButton;
    [SerializeField] private Text _assimilateText;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Text _cancelText;

    private Font _runtimeFont;

    [Header("进化倾向")]
    [Tooltip("吞噬成功时进化倾向增加量（正值=朝向宿主）")]
    [SerializeField] private float _evolveDevourDelta = 5f;
    [Tooltip("同化成功时进化倾向降低量（负向=朝向独特）")]
    [SerializeField] private float _evolveAssimilateDelta = 5f;

    /// <summary>吞噬体道具：进化倾向变化的绝对值削减量</summary>
    public static float EvolveDeltaReduction = 0f;

    // 状态
    private EnemyCore _targetEnemy;
    private IReadOnlyList<SkillInstance> _enemySkills;
    private PlayerSkillManager _playerSkillManager;
    private Action _onClose;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        _runtimeFont = Font.CreateDynamicFontFromOSFont("Arial", 16);

        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 显示二选一弹窗。
    /// </summary>
    /// <param name="enemy">被命中的敌人</param>
    /// <param name="hasSkills">敌人是否有技能（无技能时吞噬按钮禁用）</param>
    /// <param name="enemySkills">敌人技能列表</param>
    /// <param name="player">玩家技能管理器</param>
    /// <param name="onClose">关闭回调</param>
    public void ShowPopup(EnemyCore enemy, bool hasSkills, IReadOnlyList<SkillInstance> enemySkills,
        PlayerSkillManager player, Action onClose)
    {
        if (_isShowing) return;
        _isShowing = true;

        _targetEnemy = enemy;
        _enemySkills = enemySkills;
        _playerSkillManager = player;
        _onClose = onClose;

        if (_popupPanel == null) { _isShowing = false; return; }

        // 标题
        if (_titleText != null)
        {
            _titleText.font = _runtimeFont;
            _titleText.text = "选择行动";
        }

        // 吞噬按钮
        if (_devourButton != null)
        {
            _devourButton.onClick.RemoveAllListeners();
            _devourButton.onClick.AddListener(OnDevour);
            _devourButton.interactable = hasSkills;
        }
        if (_devourText != null)
        {
            _devourText.font = _runtimeFont;
            _devourText.text = hasSkills ? "吞噬" : "吞噬（无技能）";
        }

        // 同化按钮
        if (_assimilateButton != null)
        {
            _assimilateButton.onClick.RemoveAllListeners();
            _assimilateButton.onClick.AddListener(OnAssimilate);
            _assimilateButton.interactable = !_targetEnemy.IsDead;
        }
        if (_assimilateText != null)
        {
            _assimilateText.font = _runtimeFont;
            _assimilateText.text = "同化";
        }

        // 取消按钮
        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(ClosePopup);
        }
        if (_cancelText != null)
            _cancelText.font = _runtimeFont;

        // 显示
        if (_backdrop != null)
            _backdrop.SetActive(true);
        _popupPanel.SetActive(true);
    }

    private void OnDevour()
    {
        HideUI();

        // 吞噬成功 → 进化倾向增加（朝向宿主）
        ApplyEvolutionTendency(_evolveDevourDelta);

        SkillStealPopupManager popup = SkillStealPopupManager.Instance;
        if (popup != null && _enemySkills != null && _enemySkills.Count > 0)
        {
            popup.ShowPopup(_enemySkills, _playerSkillManager, Finish);
        }
        else
        {
            // 无弹窗或无技能 → 直接关闭
            Finish();
        }
    }

    private void OnAssimilate()
    {
        HideUI();

        if (_targetEnemy == null || _targetEnemy.IsDead || _playerSkillManager == null)
        {
            Finish();
            return;
        }

        // 无槽位可用（上限被设为 0）→ 直接关闭
        if (EnemyFollower.MaxFollowerCount <= 0)
        {
            Finish();
            return;
        }

        // 打开随从槽位选择弹窗：槽位操作成功后降低进化倾向（朝向独特）
        FollowerSlotPopupManager popup = FollowerSlotPopupManager.Instance;
        if (popup != null)
        {
            popup.ShowPopup(_targetEnemy, _playerSkillManager,
                () => ApplyEvolutionTendency(-_evolveAssimilateDelta),
                Finish);
        }
        else
        {
            Finish();
        }
    }

    /// <summary>统一收尾：关闭回调（恢复时间）+ 复位状态</summary>
    private void Finish()
    {
        _onClose?.Invoke();
        _onClose = null;
        _isShowing = false;
    }

    /// <summary>调整玩家进化倾向（通过 PlayerStats.SetStatValue，自动 Clamp ±100）</summary>
    private void ApplyEvolutionTendency(float delta)
    {
        PlayerStats stats = GetPlayerStats();
        if (stats == null) return;

        // 吞噬体：削减倾向变化的绝对值
        if (EvolveDeltaReduction > 0f)
        {
            if (delta > 0f)
                delta = Mathf.Max(delta - EvolveDeltaReduction, 0f);
            else
                delta = Mathf.Min(delta + EvolveDeltaReduction, 0f);
        }

        float current = stats.GetStatValue("EvolutionTendency");
        stats.SetStatValue("EvolutionTendency", current + delta);
    }

    /// <summary>获取玩家 PlayerStats</summary>
    private static PlayerStats GetPlayerStats()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        return player.GetComponent<PlayerStats>();
    }

    private void HideUI()
    {
        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);
    }

    public void ClosePopup()
    {
        if (!_isShowing) return;
        _isShowing = false;

        HideUI();

        _onClose?.Invoke();
        _onClose = null;
    }
}
