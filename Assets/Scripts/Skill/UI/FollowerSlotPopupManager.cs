using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 同化槽位选择弹窗 —— 侵蚀·同化后显示所有随从槽位（空槽/占用）。
/// 点击槽位：空槽=放置；同种=升级并消耗敌人；异种=替换。
/// 成功后回调 onSuccess（进化倾向变化），关闭后回调 onClose（恢复时间）。
/// </summary>
public class FollowerSlotPopupManager : MonoBehaviour
{
    public static FollowerSlotPopupManager Instance { get; private set; }

    [Header("预制体")]
    [SerializeField] private FollowerSlotView _slotPrefab;

    [Header("弹窗 UI（预制体中预设）")]
    [SerializeField] private GameObject _backdrop;
    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _cancelText;
    [SerializeField] private Button _cancelButton;

    private Font _runtimeFont;

    private EnemyCore _targetEnemy;
    private PlayerSkillManager _playerSkillManager;
    private Action _onSuccess;
    private Action _onClose;
    private bool _isShowing;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // 动态字体无法被预制体序列化，运行时创建（全局像素字体）
        _runtimeFont = Resources.Load<Font>("Fonts/ark-pixel-12px-monospaced-zh_cn");
        if (_runtimeFont == null)
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
    /// 显示同化槽位选择弹窗。
    /// </summary>
    /// <param name="enemy">被侵蚀的敌人（将同化/消耗）</param>
    /// <param name="player">玩家技能管理器（提供施法者 Transform）</param>
    /// <param name="onSuccess">槽位操作成功回调（进化倾向等）</param>
    /// <param name="onClose">弹窗关闭回调（恢复时间等）</param>
    public void ShowPopup(EnemyCore enemy, PlayerSkillManager player, Action onSuccess, Action onClose)
    {
        if (_isShowing) return;
        _isShowing = true;

        _targetEnemy = enemy;
        _playerSkillManager = player;
        _onSuccess = onSuccess;
        _onClose = onClose;

        // 关键：提升为根画布（sortingOrder=100 只对根画布有效）。
        // 弹窗嵌套在 UICanvas 下时是子画布，sortingOrder 被忽略，
        // 会被拾取提示框（ItemUI/ItemDetailPopup，常驻屏幕中央且 raycastTarget=true）遮挡点击。
        if (transform.parent != null)
        {
            transform.SetParent(null);
            transform.SetAsLastSibling();
        }

        // 根画布必须为 ScreenSpaceOverlay 才铺满屏幕（prefab 序列化为 WorldSpace 以规避保存归零）
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (_popupPanel == null) { _isShowing = false; return; }

        if (_titleText != null)
        {
            _titleText.font = _runtimeFont;
            _titleText.text = "选择随从槽位";
        }
        if (_cancelText != null)
            _cancelText.font = _runtimeFont;

        // 清理旧槽位
        if (_slotContainer != null)
        {
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);
        }

        // 动态生成槽位（MaxFollowerCount 个，空槽为 null）
        var slots = EnemyFollower.GetSlotsInOrder();
        for (int i = 0; i < slots.Count; i++)
        {
            int captured = i;
            EnemyFollower follower = slots[i];

            if (_slotPrefab == null || _slotContainer == null) break;

            FollowerSlotView slotView = Instantiate(_slotPrefab, _slotContainer);
            slotView.Refresh(captured, follower, _targetEnemy);

            Button slotBtn = slotView.GetComponent<Button>();
            if (slotBtn == null) slotBtn = slotView.gameObject.AddComponent<Button>();
            slotBtn.onClick.RemoveAllListeners();
            slotBtn.onClick.AddListener(() => OnSlotSelected(captured));
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(ClosePopup);
        }

        if (_backdrop != null)
            _backdrop.SetActive(true);
        _popupPanel.SetActive(true);
    }

    private void OnSlotSelected(int slotIndex)
    {
        if (_targetEnemy == null || _targetEnemy.IsDead) return;

        FollowerSlotResult result = EnemyFollower.AssimilateToSlot(
            _targetEnemy,
            _playerSkillManager != null ? _playerSkillManager.CasterTransform : null,
            slotIndex);

        if (result == FollowerSlotResult.Failed) return;

        _onSuccess?.Invoke();
        ClosePopup();
    }

    public void ClosePopup()
    {
        if (!_isShowing) return;
        _isShowing = false;

        if (_backdrop != null)
            _backdrop.SetActive(false);
        if (_popupPanel != null)
            _popupPanel.SetActive(false);

        _onClose?.Invoke();
        _onClose = null;
        _onSuccess = null;
    }
}
