using System.Collections;
using UnityEngine;

public class Arrow : MonoBehaviour, IPoolable
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite buriedSprite;

    [SerializeField] private float lifeSpan = 1;
    [SerializeField] private float speed = 2;
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockBackForce = 2;
    [SerializeField] private float knockBackTime = .2f;
    [SerializeField] private float stunTime = .2f;

    private Vector2 direction = Vector2.right;
    private Sprite originalSprite;       // 原始精灵，归还池时恢复
    private Coroutine returnCoroutine;  // 自动归还协程引用

    private void Awake()
    {
        // Awake 只在首次实例化时调用一次，记录原始精灵
        if (spriteRenderer != null)
            originalSprite = spriteRenderer.sprite;
    }

    /// <summary>
    /// 发射箭矢：设置飞行方向、初速度、旋转角度。由外部（PlayerBow）在取出后调用一次。
    /// </summary>
    public void Launch(Vector2 direction)
    {
        this.direction = direction;
        rb.velocity = direction * speed;
        RotateArrow();
    }

    // ──── IPoolable 实现 ────

    /// <summary>从池中取出时调用：替代原 Start() 的职责</summary>
    public void OnGetFromPool()
    {
        damage = StatsManager.Instance.GetDamage();

        // 启动自动归还计时器（替代原 Destroy(gameObject, lifeSpan)）
        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnAfterDelay(lifeSpan));
    }

    /// <summary>归还池中时调用：重置为干净状态</summary>
    public void OnReturnToPool()
    {
        // 停止自动归还计时器
        if (returnCoroutine != null)
        {
            StopCoroutine(returnCoroutine);
            returnCoroutine = null;
        }

        // 重置物理状态
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = false;
        }

        // 恢复原始精灵（AttachToTarget 会改成 buriedSprite）
        if (spriteRenderer != null && originalSprite != null)
            spriteRenderer.sprite = originalSprite;
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        returnCoroutine = null;
        if (this != null && gameObject.activeSelf)
        {
            // 通过对象池归还而非 Destroy
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                Destroy(gameObject); // PoolManager 不存在时兜底
        }
    }

    private void RotateArrow()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if ((enemyLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            var damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, transform);
                AttachToTarget(collision.gameObject.transform);
            }
        }
        else if ((obstacleLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            AttachToTarget(collision.gameObject.transform);
        }
    }

    private void AttachToTarget(Transform target)
    {
        spriteRenderer.sprite = buriedSprite;
        rb.velocity = Vector3.zero;
        rb.isKinematic = true;
        transform.SetParent(target);
    }
}
