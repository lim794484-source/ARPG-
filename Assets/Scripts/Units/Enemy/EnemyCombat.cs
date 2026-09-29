using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float weaponRange;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float knockBackForce = 5;
    [SerializeField] private float stunTime = 0.3f;

    
    public void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);
        //意为在玩家层的一个以weaponRange为半径，attackPoint为圆心的圆形范围内查找目标，玩家层在unity中添加，能够排除其它对象

        if (hits.Length > 0 && hits[0].enabled)//如果有目标
        {
            var health = hits[0].GetComponentInChildren<PlayerHealth>();
            if (health != null)
                health.ChangeHealth(-damage);

            var movement = hits[0].GetComponentInChildren<PlayerMovement>();
            if (movement != null) movement.KnockBack(transform, knockBackForce, stunTime);
        }
    }
}
