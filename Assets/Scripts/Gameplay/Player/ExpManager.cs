using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ExpManager : YSingleton<ExpManager>
{
    private const int DefaultExpToUpgrade = 10;
    private const float DefaultExpMultiplier = 1.5f;

    [SerializeField] private Slider expSlider;
    [SerializeField] private TMP_Text currentLevelText;
    public static event Action<int> OnLevelUp;

    private void Start()
    {
        EnsureCanvasVisible();
        UpdateUI();
    }

    private void OnEnable()
    {
        EnsureCanvasVisible();
        EnemyHealth.OnDefeated += GainExp;
    }

    private void OnDisable()
    {
        EnemyHealth.OnDefeated -= GainExp;
    }

    public void EnsureCanvasVisible()
    {
        // CanvasScaler 会根据分辨率驱动根 Canvas 的缩放，不要强行写 localScale。
        // 只修复真正会导致“有数值但看不到”的渲染开关。
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = true;

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (expSlider != null && !expSlider.gameObject.activeSelf)
        {
            expSlider.gameObject.SetActive(true);
        }
    }

    public void GainExp(int amount)
    {
        if (amount <= 0 || StatsManager.Instance == null) return;

        var stats = StatsManager.Instance.GetStats();
        NormalizeStats(stats);
        stats.currentExp += amount;

        // 一次获得大量经验时允许连续升级，并始终保证下一级阈值递增。
        while (stats.currentExp >= stats.expToUpgrade)
        {
            LevelUp(stats);
        }
        UpdateUI();
    }
    public void UpdateUI()
    {
        if (StatsManager.Instance == null) return;
        var stats = StatsManager.Instance.GetStats();
        NormalizeStats(stats);

        if (expSlider != null)
        {
            expSlider.minValue = 0;
            expSlider.maxValue = stats.expToUpgrade;
            expSlider.value = stats.currentExp;
        }
        if (currentLevelText != null)
        {
            currentLevelText.text = "等级:" + stats.level;
        }
    }

    public static void NormalizeStats(PlayerStatsData stats)
    {
        if (stats == null) return;
        stats.level = Mathf.Max(1, stats.level);
        stats.currentExp = Mathf.Max(0, stats.currentExp);
        stats.expToUpgrade = Mathf.Max(DefaultExpToUpgrade, stats.expToUpgrade);
        if (stats.expMultiplier <= 1f)
        {
            stats.expMultiplier = DefaultExpMultiplier;
        }
    }

    private void LevelUp(PlayerStatsData stats)
    {
        stats.level++;
        stats.currentExp -= stats.expToUpgrade;
        int nextThreshold = Mathf.RoundToInt(stats.expToUpgrade * stats.expMultiplier);
        stats.expToUpgrade = Mathf.Max(stats.expToUpgrade + 1, nextThreshold);
        OnLevelUp?.Invoke(1);
    }
}
