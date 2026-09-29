using UnityEngine;

public class PlayerHealth : YSingleton<PlayerHealth>
{
    [SerializeField] private GameObject playerRoot;
    [SerializeField] private ToggleCanvasEventSO toggleGameOverEvent;


    protected override void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this); // 只销毁重复组件，保留玩家根节点
            return;
        }
        _instance = this;
    }

    void Start()
    {
        StatsManager.Instance.Respawn();
    }
    public void ChangeHealth(int amount)
    {
        StatsManager.Instance.UpdateHealth(amount);

        if (StatsManager.Instance.GetCurrentHealth() <= 0)
        {
            toggleGameOverEvent.RaiseToggleCanvasEvent(true);

            if (playerRoot != null)
                playerRoot.SetActive(false);
        }
    }
}
