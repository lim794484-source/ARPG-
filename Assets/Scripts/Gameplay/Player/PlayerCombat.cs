using UnityEngine;
using MyEnums;
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform attackPoint;
    [SerializeField] private LayerMask enemyMask;//需要在unity中创建并且标记
    [SerializeField] private VoidEventSO slashActionFinishedEvent;

    /// <summary>
    /// 当前是否为激活的武器模式。
    /// Player 根节点上的 PlayerAnimationEventRelay 会按此状态转发动画事件。
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public void SetActive(bool active) => IsActive = active;



    public void DealDamage()
    {
        //TODO:可以引入空间优化算法
        Collider2D[] enemis = Physics2D.OverlapCircleAll(
            attackPoint.position,
            StatsManager.Instance.GetWeaponRange(),
            enemyMask);

        foreach (Collider2D enemy in enemis)
        {
            var damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(StatsManager.Instance.GetDamage(), transform);
            }
        }

    }
    public void FinishCombat()
    {
        // 动画事件触发：通知 PlayerMovement（及其它订阅者）近战动作结束，由其统一重置状态
        if (slashActionFinishedEvent != null) slashActionFinishedEvent.OnEventRaised();
    }

    // 兼容仍使用旧拼写 "FinshCombat" 的动画资源。
    public void FinshCombat()
    {
        FinishCombat();
    }
    //private void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(attackPoint.position, StatsManager.Instance.weaponRange);
    //}
}
