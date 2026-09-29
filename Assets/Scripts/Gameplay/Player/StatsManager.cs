using UnityEngine;
using System;
[Serializable]
public class PlayerStatsData
{
    public int damage;
    public float weaponRange;
    public float knockBackForce;
    public float knockBackTime;
    public float stunTime;
    public float coolDown;
    public float speed;
    public int maxHealth;
    public int currentHealth;
    public int skillPoints;
    public int level;
    public int currentExp;
    public int expToUpgrade;
    public float expMultiplier;

    public PlayerStatsData Copy()
    {
        return (PlayerStatsData)MemberwiseClone();
    }
}

public class StatsManager : YSingleton<StatsManager>
{
    [SerializeField] private PlayerStatsData stats = new();
    private PlayerStatsData initialStats;

    protected override void OnSingletonInitialized()
    {
        stats ??= new PlayerStatsData();
        ExpManager.NormalizeStats(stats);
        initialStats = stats.Copy();
    }
    public PlayerStatsData GetStats() => stats;
    public void LoadStats(PlayerStatsData data)
    {
        if (data == null) return;
        stats = data;
        ExpManager.NormalizeStats(stats);
        if (ExpManager.Instance != null)
        {
            ExpManager.Instance.UpdateUI();
        }
    }

    public void ResetToInitialStats()
    {
        stats = initialStats != null ? initialStats.Copy() : new PlayerStatsData();
        ExpManager.NormalizeStats(stats);
        if (ExpManager.Instance != null) ExpManager.Instance.UpdateUI();
        if (HealthCanvasManager.Instance != null) HealthCanvasManager.Instance.UpdateHealthText();
        if (StatsCanvasManager.Instance != null) StatsCanvasManager.Instance.UpdateAllStats();
    }
    public int GetDamage() => stats.damage;
    public float GetWeaponRange() => stats.weaponRange;
    public float GetKnockBackForce() => stats.knockBackForce;
    public float GetKnockBackTime() => stats.knockBackTime;
    public float GetStunTime() => stats.stunTime;
    public float GetCoolDown() => stats.coolDown;
    public float GetSpeed() => stats.speed;
    public int GetMaxHealth() => stats.maxHealth;
    public int GetCurrentHealth() => stats.currentHealth;
    public int GetSkillPoints() => stats.skillPoints;
    public int GetLevel() => stats.level;
    public int GetCurrentExp() => stats.currentExp;
    public int GetExpToUpgrade() => stats.expToUpgrade;
    public float GetExpMultiplier() => stats.expMultiplier;

    //注意setter需要伴随UI的更新一起编写，非必要不要直接用setter
    public void Respawn()
    {
        if (stats.currentHealth <= 0)
            stats.currentHealth = stats.maxHealth;
        HealthCanvasManager.Instance.UpdateHealthText();
    }

    public void UpdateMaxHealth(int amount)
    {
        stats.maxHealth += amount;
        HealthCanvasManager.Instance.UpdateHealthText();
    }
    public void UpdateHealth(int amount)
    {
        stats.currentHealth = Mathf.Clamp(stats.currentHealth + amount, 0, stats.maxHealth);
        HealthCanvasManager.Instance.UpdateHealthText();
    }
    public void UpdateSpeed(float amount)
    {
        stats.speed += amount;
        StatsCanvasManager.Instance.UpdateSpeed();
    }
    public void UpdateDamage(int amount)
    {
        stats.damage += amount;
        StatsCanvasManager.Instance.UpdateDamage();
    }
    public void UpdateSkillPoints(int amount) => stats.skillPoints += amount;

}
