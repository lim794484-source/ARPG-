using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItem : MonoBehaviour
{
    public void ApplyItemEffects(ItemSO item)
    {

        if (item.maxHealth > 0)
            StatsManager.Instance.UpdateMaxHealth(item.maxHealth);
        if (item.currentHealth > 0)
            StatsManager.Instance.UpdateHealth(item.currentHealth);
        if (item.speed > 0)
            StatsManager.Instance.UpdateSpeed(item.speed);
        if (item.damage > 0)
            StatsManager.Instance.UpdateDamage(item.damage);
        if (item.duration > 0)
            StartCoroutine(EffectTimer(item, item.duration));

    }

    private IEnumerator EffectTimer(ItemSO item, float duration)//用以计时，一段时间之后还原
    {
        yield return new WaitForSeconds(duration);

        if (item.maxHealth > 0)
            StatsManager.Instance.UpdateMaxHealth(-item.maxHealth);

        int healthDiff = StatsManager.Instance.GetCurrentHealth() - StatsManager.Instance.GetMaxHealth();
        if (healthDiff > 0)//如果更新前当前生命大于更新后（已经减小回去）最大生命，那就会减去healthDiff
            StatsManager.Instance.UpdateHealth(healthDiff);

        if (item.speed > 0)
            StatsManager.Instance.UpdateSpeed(-item.speed);

        if (item.damage > 0)
            StatsManager.Instance.UpdateDamage(-item.damage);

    }
}
