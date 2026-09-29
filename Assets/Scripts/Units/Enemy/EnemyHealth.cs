using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable, ISaveable
{
    [SerializeField] private int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private int expReward = 2;
    [SerializeField, HideInInspector] private string persistentId;

    private bool isDead;

    public delegate void MonsterDefeated(int exp);//观察者模式
    public static event MonsterDefeated OnDefeated;

    private EnemyKnockBack knockBack;

    private void Awake()
    {
        knockBack = GetComponent<EnemyKnockBack>();
        currentHealth = maxHealth;
        isDead = false;

        if (DataManager.Instance != null)
        {
            ((ISaveable)this).RegisterSaveable();
        }
    }

    private void OnDestroy()
    {
        if (DataManager.Instance != null)
        {
            ((ISaveable)this).UnRegisterSaveable();
        }
    }

    public void ChangeHealth(int amount)
    {
        if (isDead) return;
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        else if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            PersistCurrentState();
            OnDefeated?.Invoke(expReward);//事件被触发
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// IDamageable：扣血 + 击退一次完成。玩家攻击命中只需调这个方法。
    /// </summary>
    public void TakeDamage(int damage, Transform attacker)
    {
        if (isDead) return;
        ChangeHealth(-damage);
        if (!isDead && knockBack != null)
        {
            knockBack.Knockback(
                attacker,
                StatsManager.Instance.GetKnockBackForce(),
                StatsManager.Instance.GetStunTime(),
                StatsManager.Instance.GetKnockBackTime());
        }
    }

    public string PersistentId => persistentId;

    public DataDefinition GetDataID()
    {
        return GetComponent<DataDefinition>();
    }

    public void SaveData(Data data)
    {
        if (data == null) return;
        data.enemiesStatsDic ??= new Dictionary<string, EnemyStatus>();
        data.enemiesStatsDic[GetPersistenceKey()] = new EnemyStatus(isDead, currentHealth);
    }

    public void LoadData(Data data)
    {
        if (data?.enemiesStatsDic == null
            || !data.enemiesStatsDic.TryGetValue(GetPersistenceKey(), out EnemyStatus status))
        {
            currentHealth = maxHealth;
            isDead = false;
            return;
        }

        isDead = status.isDead || status.currentHealth <= 0;
        currentHealth = Mathf.Clamp(status.currentHealth, 0, maxHealth);
        if (isDead)
        {
            // 从记录恢复死亡状态时不触发经验奖励。
            Destroy(gameObject);
        }
    }

    private void PersistCurrentState()
    {
        if (DataManager.Instance != null)
        {
            SaveData(DataManager.Instance.GetData);
        }
    }

    private string GetPersistenceKey()
    {
        if (!string.IsNullOrEmpty(persistentId)) return persistentId;

        // 旧场景或临时生成对象没有显式 ID 时仍提供稳定兜底键。
        var path = new StringBuilder();
        Transform current = transform;
        while (current != null)
        {
            path.Insert(0, $"/{current.GetSiblingIndex()}:{current.name}");
            current = current.parent;
        }
        string sceneKey = string.IsNullOrEmpty(gameObject.scene.path)
            ? gameObject.scene.name
            : gameObject.scene.path;
        return $"enemy:{sceneKey}{path}";
    }
}
