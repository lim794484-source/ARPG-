using System;
using UnityEngine;

[Serializable]
public class RetryConfig
{
    [SerializeField] private GameSceneSO scene;      // 场景索引键：匹配当前所在场景
    [SerializeField] private Vector3 spawnPosition;  // 可选出生点，zero 则使用场景初始位置

    public GameSceneSO Scene => scene;
    public Vector3 SpawnPosition => spawnPosition;
}

public class RetryManager : YSingleton<RetryManager>
{

    [SerializeField] private SceneLoadEventSO loadEventSO;
    [Header("Retry Event")]
    [SerializeField] private VoidEventSO retryEventSO;
    [Header("Retry Config")]
    [SerializeField] private RetryConfig[] retryConfigs; // 按场景索引配置，由 PersistentScene 统一维护


    private void OnEnable()
    {
        retryEventSO.VoidEvent += OnReTry;
    }
    private void OnDisable()
    {
        retryEventSO.VoidEvent -= OnReTry;
    }

    private void OnReTry()
    {
        GameSceneSO currentScene = SceneChanger.Instance.GetCurrentGameScene();
        RetryConfig config = GetConfig(currentScene);
        if (config == null)
        {
            Debug.LogWarning($"[RetryManager] 未找到场景 {currentScene?.name} 的重试配置");
            return;
        }

        Vector3 position = config.SpawnPosition == Vector3.zero ? config.Scene.initialPosition : config.SpawnPosition;
        loadEventSO.RaiseLoadRequestEvent(config.Scene, position, true);
        StatsManager.Instance.Respawn();
    }

    private RetryConfig GetConfig(GameSceneSO scene)
    {
        if (retryConfigs == null || scene == null) return null;
        foreach (var config in retryConfigs)
        {
            if (config != null && config.Scene == scene)
                return config;
        }
        return null;
    }
}
