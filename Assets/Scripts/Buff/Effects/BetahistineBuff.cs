using UnityEngine;

/// <summary>
/// 倍他司汀 Buff — 每次清理房间提高攻速（+5），最多+50。
/// 受到伤害（TakeDamage）时攻速加成清零。永久 Buff。
/// </summary>
[CreateAssetMenu(fileName = "BetahistineBuff", menuName = "Game/Buff Effect/Betahistine")]
public class BetahistineBuff : BuffEffectBase
{
    [Header("配置")]
    [SerializeField] private float _fireRateBonusPerRoom = 5f;
    [SerializeField] private float _maxBonus = 50f;

    private PlayerStats _stats;
    private float _currentBonus;
    private RoomManager _currentRoomManager;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _stats = target.GetComponent<PlayerStats>();
        if (_stats == null) return;

        _currentBonus = 0f;

        // 订阅当前房间清空事件
        SubscribeRoomCleared();
        // 房间切换完成后重新订阅新房间的清空事件
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted += OnRoomSwitchCompleted;

        _stats.OnDamaged += OnDamaged;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        UnsubscribeRoomCleared();

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted -= OnRoomSwitchCompleted;

        if (_stats != null)
        {
            _stats.OnDamaged -= OnDamaged;

            if (_currentBonus > 0f)
            {
                float current = _stats.GetStatValue("ShotsPerMinute");
                _stats.SetStatValue("ShotsPerMinute", current - _currentBonus);
            }
        }
    }

    private void SubscribeRoomCleared()
    {
        if (MapManager.Instance == null || MapManager.Instance.CurrentRoom == null) return;
        _currentRoomManager = MapManager.Instance.CurrentRoom.GetComponent<RoomManager>();
        if (_currentRoomManager != null)
            _currentRoomManager.OnRoomCleared += OnRoomCleared;
    }

    private void UnsubscribeRoomCleared()
    {
        if (_currentRoomManager != null)
            _currentRoomManager.OnRoomCleared -= OnRoomCleared;
        _currentRoomManager = null;
    }

    private void OnRoomSwitchCompleted(int roomId)
    {
        // 切换到新房间后，重新订阅新房间的清空事件
        UnsubscribeRoomCleared();
        SubscribeRoomCleared();
    }

    private void OnRoomCleared()
    {
        if (_stats == null) return;

        if (_currentBonus < _maxBonus)
        {
            float add = Mathf.Min(_fireRateBonusPerRoom, _maxBonus - _currentBonus);
            _currentBonus += add;

            float current = _stats.GetStatValue("ShotsPerMinute");
            _stats.SetStatValue("ShotsPerMinute", current + add);
        }
    }

    private void OnDamaged(float damage)
    {
        if (_stats == null || _currentBonus <= 0f) return;

        float spm = _stats.GetStatValue("ShotsPerMinute");
        _stats.SetStatValue("ShotsPerMinute", spm - _currentBonus);
        _currentBonus = 0f;
    }
}
