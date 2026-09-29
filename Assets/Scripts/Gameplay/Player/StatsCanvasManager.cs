using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatsCanvasManager : YSingleton<StatsCanvasManager>, ICanvasManager
{
    [SerializeField] private GameObject[] statsSlots;
    [SerializeField] private CanvasGroup statsCanvas;

    [SerializeField] private ToggleCanvasEventSO toggleStatsEvent;

    public ToggleCanvasEventSO ToggleCanvasEvent => toggleStatsEvent;
    private Canvas canvas;

    protected override void OnSingletonInitialized()
    {
        statsCanvas.alpha = 0;
        canvas = statsCanvas.GetComponent<Canvas>();
    }

    private void Start()
    {
        UpdateAllStats();
    }

    private void OnEnable()
    {
        toggleStatsEvent.toggleCanvasEvent += OnToggleStatsEvent;
        toggleStatsEvent.focusEvent += OnFocus;
    }

    private void OnDisable()
    {
        toggleStatsEvent.toggleCanvasEvent -= OnToggleStatsEvent;
        toggleStatsEvent.focusEvent -= OnFocus;
    }

    private void OnToggleStatsEvent(bool state)
    {
        UpdateAllStats();
        ((ICanvasManager)this).ToggleCanvas(statsCanvas, canvas, MyEnums.CanvasToToggle.Stats, state);
    }

    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.Stats, true);
    }

    public void UpdateDamage()
    {
        statsSlots[0].GetComponentInChildren<TMP_Text>().text = "攻击力:" + StatsManager.Instance.GetDamage();
        // 注意：Components是复数形式，返回的是数组。
    }

    public void UpdateSpeed()
    {
        statsSlots[1].GetComponentInChildren<TMP_Text>().text = "速度:" + StatsManager.Instance.GetSpeed();
    }

    public void UpdateAllStats()
    {
        UpdateDamage();
        UpdateSpeed();
    }
}
