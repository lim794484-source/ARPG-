using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public class NPCWander : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private MovementController aStarController;

    [Header("Movement Settings")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 1f;
    [SerializeField] private int patrolRadius = 5;

    private int facingDirec = 1; // 1表示朝右，-1表示朝左
    private Vector3 circleCenter;
    private Vector3 targetPosition;
    private Vector3 posToGo;
    private Rigidbody2D rb;
    private bool isWaiting = false;
    private float threshold = 0;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>(); //这里要早点获取组件，因为Start可能在OnEnable之后被调用，而OnEnable里需要用到rb，避免空引用错误
    }
    private void OnEnable()
    {
        if (rb != null) rb.isKinematic = false;
    }
    private void Start()
    {
        if (aStarController == null || rb == null) return;
        circleCenter = transform.position;
        targetPosition = circleCenter + randomDirection();
        aStarController.ResetPath();
        posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);
        threshold = aStarController.GetThreshold() * .2f;
    }

    private void OnDisable()
    {
        if (animator != null) animator.SetBool("isWalking", false);
    }
    private void Update()
    {
        if (isWaiting || aStarController == null || rb == null)
            return;

        // 计算朝向目标的方向
        Vector2 direction = (posToGo - transform.position).normalized;
        if (direction.x * facingDirec < 0)
        {
            Flip();
        }
        if (posToGo != Vector3.zero)
            rb.velocity = direction * speed;
        else rb.velocity = Vector2.zero;

        float thresholdSqr = threshold * threshold;

        if ((transform.position - posToGo).sqrMagnitude < thresholdSqr)//到寻路节点则告知controller
        {
            aStarController.ArrivedPos();
            posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);
        }
        else
        {
            if (animator != null) animator.SetBool("isWalking", true);
        }

        if ((transform.position - targetPosition).sqrMagnitude < thresholdSqr || posToGo == Vector3.zero)//到终点则重新获取巡逻点
        {
            StartCoroutine(WaitAndContinue());
        }
    }
    private void Flip()
    {
        facingDirec *= -1;
        transform.localScale = new Vector3(-1 * transform.localScale.x, transform.localScale.y, transform.localScale.z);
        //这个向量应该乘-1而不是facingDirec，因为facingDirec仅记录当前的状态（朝向），可以为1
    }
    IEnumerator WaitAndContinue()
    {
        isWaiting = true;
        if (animator != null) animator.SetBool("isWalking", false);
        rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(waitTime);

        // 如果没有路径，最多尝试有限次数并让出一帧；不能在协程中无界同步重试，
        // 否则 NPC 被堵住时会锁死主线程并持续刷屏。
        bool foundPath = false;
        const int maxPathAttempts = 8;
        for (int attempt = 0; attempt < maxPathAttempts; attempt++)
        {
            targetPosition = circleCenter + randomDirection();
            //如果随机点太近了就重新随机，避免被卡住
            if ((targetPosition - transform.position).sqrMagnitude < threshold * threshold)
            {
                targetPosition = circleCenter + (transform.position - circleCenter).normalized * patrolRadius;
            }
            aStarController.ResetPath();
            posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);
            if (posToGo != Vector3.zero)
            {
                foundPath = true;
                break;
            }
            yield return null;
        }

        if (!foundPath)
        {
            // 保持等待状态一小段时间后再试，避免每帧重新启动协程。
            posToGo = transform.position;
            targetPosition = transform.position;
            yield return new WaitForSeconds(Mathf.Max(0.5f, waitTime));
        }
        isWaiting = false;
    }

    private Vector3 randomDirection()
    {
        Vector2 dir = Random.insideUnitCircle; // 均匀分布在圆内
        return new Vector3(dir.x, dir.y, 0) * patrolRadius;
    }
    private void OnDrawGizmos()
    {
        if (!enabled)//非运行时不着色
            return;
        if (patrolRadius == 0)
            return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? circleCenter : transform.position, patrolRadius);//运行时着色取初始点
    }
}
