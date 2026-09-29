using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池管理器：按预制体分类管理多个池。
/// 使用方式：
///   取出：PoolManager.Instance.Get(prefab, pos, rot)
///   归还：PoolManager.Instance.Return(gameObject)
/// 挂载位置：PersistentScene → GlobalManagers 下新建 PoolManager 物体并挂载此脚本。
/// </summary>
public class PoolManager : YSingleton<PoolManager>
{
    /// <summary>每个预制体对应一个队列</summary>
    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

    /// <summary>实例 → 所属预制体的映射，归还时据此分类</summary>
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

    /// <summary>
    /// 从池中取出一个对象。池空则自动实例化新对象。
    /// </summary>
    /// <param name="prefab">预制体</param>
    /// <param name="position">生成位置</param>
    /// <param name="rotation">生成旋转</param>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Get 失败：prefab 为空");
            return null;
        }

        // 确保该预制体有对应队列
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        GameObject obj = null;

        // 从队列中取一个非空对象
        while (pools[prefab].Count > 0)
        {
            obj = pools[prefab].Dequeue();
            if (obj != null) break;         // 跳过已被销毁的空引用
            obj = null;
        }

        // 队列为空或全是空引用 → 实例化新对象
        if (obj == null)
        {
            obj = Instantiate(prefab);
            instanceToPrefab[obj] = prefab;
        }

        // 设置位置和旋转
        obj.transform.SetPositionAndRotation(position, rotation);

        // 激活对象
        obj.SetActive(true);

        // 调用池化对象的初始化回调
        if (obj.TryGetComponent<IPoolable>(out var poolable))
            poolable.OnGetFromPool();

        return obj;
    }

    /// <summary>
    /// 归还一个对象到池中。对象会被设为非激活状态并重置。
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        // 查找该实例属于哪个预制体
        if (!instanceToPrefab.TryGetValue(obj, out var prefab))
        {
            // 不在池中管理范围内 → 直接销毁
            Destroy(obj);
            return;
        }

        // 先调用归还回调（此时对象仍激活，可停止协程等）
        if (obj.TryGetComponent<IPoolable>(out var poolable))
            poolable.OnReturnToPool();

        // 脱离父级（例如箭矢插在敌人身上时）
        obj.transform.SetParent(null);

        // 设为非激活，等待下次取出
        obj.SetActive(false);

        // 放回队列
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();
        pools[prefab].Enqueue(obj);
    }

    /// <summary>
    /// 预热：提前实例化 N 个对象放入池中。
    /// 在场景初始化时调用，避免运行时首次实例化的卡顿。
    /// </summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(prefab);
            instanceToPrefab[obj] = prefab;

            if (obj.TryGetComponent<IPoolable>(out var poolable))
                poolable.OnReturnToPool();

            obj.transform.SetParent(null);
            obj.SetActive(false);
            pools[prefab].Enqueue(obj);
        }
    }

    /// <summary>
    /// 清空指定预制体的池（场景切换时调用）。
    /// 注意：只清理池中空闲对象，已取出正在使用的对象不受影响。
    /// </summary>
    public void Clear(GameObject prefab)
    {
        if (prefab == null || !pools.ContainsKey(prefab)) return;

        foreach (var obj in pools[prefab])
        {
            if (obj != null) Destroy(obj);
        }
        pools[prefab].Clear();
    }

    /// <summary>清空所有池</summary>
    public void ClearAll()
    {
        foreach (var pair in pools)
        {
            foreach (var obj in pair.Value)
            {
                if (obj != null) Destroy(obj);
            }
        }
        pools.Clear();

        // 清理实例映射中的空引用
        var keysToRemove = new List<GameObject>();
        foreach (var key in instanceToPrefab.Keys)
        {
            if (key == null) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
            instanceToPrefab.Remove(key);
    }
}
