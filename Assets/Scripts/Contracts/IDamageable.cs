using UnityEngine;

/// <summary>
/// 可受伤目标统一入口：玩家攻击命中时只需 GetComponent 一次，调用 TakeDamage 即可。
/// 实现者（如 EnemyHealth）在内部自行协调扣血、击退、死亡等逻辑。
/// </summary>
public interface IDamageable
{
    /// <param name="damage">伤害值（正数）</param>
    /// <param name="attacker">攻击者 Transform，用于计算击退方向</param>
    void TakeDamage(int damage, Transform attacker);
}
