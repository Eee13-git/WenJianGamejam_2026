using UnityEngine;
using System.Collections;

/// <summary>
/// 地西泮 Stage2 Buff — 每清理2个房间后，下一个房间的敌人中幅减速（30%）。
/// 只影响进入房间时已生成的敌人，不影响子弹。不修改全局速度倍率。
/// 通过修改房间内每个 EnemyStats 的 PatrolSpeed/ChaseSpeed 实现。
/// </summary>
[CreateAssetMenu(fileName = "DiazepamSlowBuff", menuName = "Game/Buff Effect/Diazepam Slow")]
public class DiazepamSlowBuff : BuffEffectBase
{
    [Tooltip("减速倍率（0.7 = 30%减速）")]
    [SerializeField] private float _slowMultiplier = 0.7f;

    private PlayerStats _stats;
    private RoomManager _currentRoomManager;
    private MonoBehaviour _runner;
    private int _roomClearCount;
    private bool _slowNextRoom;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _stats = target.GetComponent<PlayerStats>();
        _runner = target.GetComponent<MonoBehaviour>();
        if (_stats == null || _runner == null) return;

        _roomClearCount = 0;
        _slowNextRoom = false;

        SubscribeRoomCleared();
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted += OnRoomSwitchCompleted;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        UnsubscribeRoomCleared();

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomSwitchCompleted -= OnRoomSwitchCompleted;
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
        UnsubscribeRoomCleared();
        SubscribeRoomCleared();

        // 如果上一波清房计数达到触发条件，进入新房间后减速其中所有敌人
        if (_slowNextRoom && _runner != null)
        {
            _slowNextRoom = false;
            _runner.StartCoroutine(SlowRoomEnemiesNextFrame());
        }
    }

    private void OnRoomCleared()
    {
        _roomClearCount++;

        // 每清理2个房间，标记下一个房间的敌人需要减速
        if (_roomClearCount % 2 == 0)
            _slowNextRoom = true;
    }

    /// <summary>延迟一帧后减速当前房间所有敌人（等待敌人生成完成）</summary>
    private IEnumerator SlowRoomEnemiesNextFrame()
    {
        yield return null;

        var enemies = Object.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            // 排除已同化的随从（tag=Player）
            if (enemy.CompareTag("Player")) continue;

            enemy.SetStatValue("PatrolSpeed", enemy.GetStatValue("PatrolSpeed") * _slowMultiplier);
            enemy.SetStatValue("ChaseSpeed", enemy.GetStatValue("ChaseSpeed") * _slowMultiplier);
            count++;
        }

        if (count > 0)
            Debug.Log($"[DiazepamSlow] 减速了 {count} 个房间内敌人");
    }
}

