using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投射物对象池 — 按 prefab 分组，池化复用 GameObject。
/// </summary>
public static class ProjectilePool
{
    private static readonly Dictionary<int, Queue<GameObject>> _pools = new();

    /// <summary>从池中取出，池空则 Instantiate，自动标记来源 prefab</summary>
    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        int key = prefab.GetInstanceID();
        if (!_pools.TryGetValue(key, out var queue))
            queue = _pools[key] = new Queue<GameObject>();

        GameObject obj;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
            var t = obj.transform;
            t.position = position;
            t.rotation = rotation;
            obj.SetActive(true);
        }
        else
        {
            obj = Object.Instantiate(prefab, position, rotation);
            obj.name = prefab.name;
            // 贴上来源标记
            var marker = obj.AddComponent<PoolMarker>();
            marker.prefabId = key;
        }

        foreach (var p in obj.GetComponents<IPoolable>())
            p.OnSpawn();

        return obj;
    }

    /// <summary>归还实例到池（停用+入队）</summary>
    public static void Return(GameObject instance)
    {
        if (instance == null) return;

        foreach (var p in instance.GetComponents<IPoolable>())
            p.OnReturn();

        instance.transform.SetParent(null);
        instance.SetActive(false);

        var marker = instance.GetComponent<PoolMarker>();
        if (marker != null)
        {
            int key = marker.prefabId;
            if (!_pools.TryGetValue(key, out var queue))
                queue = _pools[key] = new Queue<GameObject>();
            queue.Enqueue(instance);
        }
        else
        {
            Object.Destroy(instance);
        }
    }

    /// <summary>预热（pool 已有同级实例则跳过）</summary>
    public static int Prewarm(GameObject prefab, int count)
    {
        int key = prefab.GetInstanceID();
        if (!_pools.TryGetValue(key, out var queue))
            queue = _pools[key] = new Queue<GameObject>();

        int created = 0;
        while (queue.Count < count && created < count)
        {
            var obj = Object.Instantiate(prefab);
            obj.name = prefab.name;
            obj.SetActive(false);
            queue.Enqueue(obj);
            if (obj.GetComponent<PoolMarker>() == null)
                obj.AddComponent<PoolMarker>().prefabId = key;
            created++;
        }
        return created; // 实际创建数（已存在则不创建）
    }

    /// <summary>清空</summary>
    public static void Clear()
    {
        foreach (var q in _pools.Values)
            while (q.Count > 0) Object.Destroy(q.Dequeue());
        _pools.Clear();
    }
}

/// <summary>池化生命周期回调</summary>
public interface IPoolable
{
    void OnSpawn();      // Get 取出时
    void OnReturn();     // Return 归还前
}

/// <summary>内部标记：记录此对象源自哪个 prefab</summary>
public class PoolMarker : MonoBehaviour
{
    public int prefabId;
}
