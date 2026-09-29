using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    [SerializeField] private PlayerCombat combat;
    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointSpent;
    }
    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointSpent;
    }
    private void HandleAbilityPointSpent(SkillSlot skillSlot)
    {
        string skillName = skillSlot.skillSO.skillName;

        switch (skillName)
        {
            case "生命强化":
                StatsManager.Instance.UpdateMaxHealth(1);
                StatsManager.Instance.UpdateHealth(1);
                break;
            case "剑刃斩击":
                if (combat != null)
                    combat.SetActive(true);
                else
                    Debug.LogError("技能管理器缺少 PlayerCombat 引用，无法激活剑刃斩击。");
                break;

            default:
                Debug.Log($"技能 {skillName} 未找到对应效果");
                break;
    
        }
    }
}
