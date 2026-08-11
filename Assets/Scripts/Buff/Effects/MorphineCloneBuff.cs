using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 分裂体 Buff — 每进入一个房间分裂自己（永久Buff）。
/// 分裂体围绕本体轨道运行，跟随本体攻击方向射击，享受本体藏品加成，恒为1血。
/// 参数驱动，可被多个分裂类效果复用。
/// </summary>
[CreateAssetMenu(fileName = "MorphineCloneBuff", menuName = "Game/Buff Effect/Morphine Clone")]
public class MorphineCloneBuff : BuffEffectBase
{
    [Header("分裂参数")]
    [SerializeField] private int _maxClones = 3;
    [Tooltip("每次分裂失去最大生命值比例 (0.2 = 20%)")]
    [SerializeField] private float _maxHealthLossRatio = 0.2f;
    [SerializeField] private float _cloneScale = 0.5f;
    [SerializeField] private float _cloneOrbitRadius = 1.5f;
    [SerializeField] private float _cloneOrbitSpeed = 120f;

    private readonly List<GameObject> _clones = new List<GameObject>();
    private GameObject _target;
    private float _totalHealthLost;

    public override void OnApply(GameObject target, BuffInstance buff)
    {
        _target = target;
        _clones.Clear();
        _totalHealthLost = 0f;

        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomChanged += OnRoomChanged;
    }

    public override void OnRemove(GameObject target, BuffInstance buff)
    {
        if (MapManager.Instance != null)
            MapManager.Instance.OnRoomChanged -= OnRoomChanged;

        foreach (var clone in _clones)
        {
            if (clone != null)
                Object.Destroy(clone);
        }
        _clones.Clear();

        // 补回累计失去的最大生命值
        var stats = target.GetComponent<PlayerStats>();
        if (stats != null && _totalHealthLost > 0f)
        {
            float currentMax = stats.GetStatValue("MaxHealth");
            stats.SetStatValue("MaxHealth", currentMax + _totalHealthLost);
            _totalHealthLost = 0f;
        }
    }

    private void OnRoomChanged(int fromRoomId, int toRoomId)
    {
        if (_target == null) return;

        _clones.RemoveAll(c => c == null);
        if (_clones.Count >= _maxClones) return;

        var stats = _target.GetComponent<PlayerStats>();
        if (stats == null) return;

        // 失去 20% 最大生命值，记录累计损失
        float currentMax = stats.GetStatValue("MaxHealth");
        float lostAmount = currentMax * _maxHealthLossRatio;
        stats.SetStatValue("MaxHealth", currentMax - lostAmount);
        _totalHealthLost += lostAmount;

        SpawnClone(stats);
    }

    private void SpawnClone(PlayerStats stats)
    {
        SpriteRenderer playerSr = _target.GetComponent<SpriteRenderer>();
        if (playerSr == null || playerSr.sprite == null) return;

        PlayerCombat combat = _target.GetComponent<PlayerCombat>();
        GameObject bulletPrefab = GetBulletPrefab(combat);

        var go = new GameObject($"MorphineClone_{_clones.Count}");
        go.tag = "Player";
        go.layer = 0;

        if (MapManager.Instance != null && MapManager.Instance.CurrentRoom != null)
            go.transform.SetParent(MapManager.Instance.CurrentRoom.transform, true);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = playerSr.sprite;
        sr.material = playerSr.material;
        sr.sortingOrder = playerSr.sortingOrder;
        sr.color = playerSr.color;

        go.transform.localScale = Vector3.one * _cloneScale;
        go.transform.position = _target.transform.position;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.isKinematic = true;

        var clone = go.AddComponent<MorphineClone>();
        clone.Initialize(
            _target.transform,
            stats,
            bulletPrefab,
            combat,
            _cloneOrbitRadius,
            _cloneOrbitSpeed,
            _clones.Count);

        clone.OnCloneDied += () => RedistributeAngles();

        _clones.Add(go);

        // 重新分配所有分裂体的轨道相位（均分360°）
        RedistributeAngles();
    }

    private void RedistributeAngles()
    {
        _clones.RemoveAll(c => c == null);
        // 移除已死亡但尚未销毁的分裂体（Destroy 有延迟）
        _clones.RemoveAll(c =>
        {
            var mc = c.GetComponent<MorphineClone>();
            return mc == null || mc.IsDead;
        });
        int count = _clones.Count;
        if (count == 0) return;

        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            var clone = _clones[i].GetComponent<MorphineClone>();
            if (clone != null)
                clone.SetOrbitAngle(i * step);
        }
    }

    private GameObject GetBulletPrefab(PlayerCombat combat)
    {
        if (combat == null) return null;
        var field = typeof(PlayerCombat).GetField("bulletPrefab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(combat) as GameObject;
    }
}
