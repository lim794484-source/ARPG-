using UnityEngine;

/// <summary>
/// 可池化对象接口：对象从池中取出/归还时由 PoolManager 自动调用。
/// 实现者负责在此重置自身状态（速度、精灵、父级等）。
/// </summary>
public interface IPoolable
{
    /// <summary>从池中取出时调用：初始化本实例本次使用的状态</summary>
    void OnGetFromPool();

    /// <summary>归还池中时调用：重置为"干净"状态，等待下次取出</summary>
    void OnReturnToPool();
}
