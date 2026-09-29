using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC按矩形路线巡逻
/// </summary>
public class NPCPatrol : MonoBehaviour
{
    [Header("Patrol Rectangle Settings")]
    [SerializeField] private Vector2 patrolSize = new Vector2(5f, 3f); // 矩形的长和宽
    [SerializeField] private bool clockwise = true; // true为顺时针，false为逆时针

    [Header("Movement Settings")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 1f;

    [Header("Component References")]
    [SerializeField] private Animator animator;
    [SerializeField] private MovementController aStarController;

    // 四个角的位置
    private Vector3[] cornerPositions = new Vector3[4];
    private int currentCornerIndex = 0; // 当前目标角索引

    private int facingDirec = 1; // 1表示朝右，-1表示朝左
    private Vector3 targetPosition; // 当前巡逻目标点
    private Vector3 posToGo; // 下一个寻路节点
    private Rigidbody2D rb;
    private bool isWaiting = false;
    private float threshold;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        rb.isKinematic = false;
    }

    private void Start()
    {
        if(aStarController==null)
        {
            Debug.LogError("NPCPatrol: Missing MovementController reference.");
            enabled = false;
            return;
        }
        CalculateCornerPositions();

        // 从当前角落开始，找最近的角作为起点
        currentCornerIndex = GetNearestCornerIndex();
        targetPosition = cornerPositions[currentCornerIndex];

        aStarController.ResetPath();
        posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);
        threshold = aStarController.GetThreshold() * 0.2f;
    }

    private void OnDisable()
    {
        animator.SetBool("isWalking", false);
    }

    private void Update()
    {
        if (isWaiting)
            return;

        // 计算朝向目标的方向
        Vector2 direction = (posToGo - transform.position).normalized;
        if (direction.x * facingDirec < 0)
        {
            Flip();
        }

        if (posToGo != Vector3.zero)
            rb.velocity = direction * speed;
        else
            rb.velocity = Vector2.zero;

        float thresholdSqr = threshold * threshold;

        // 到达寻路节点
        if ((transform.position - posToGo).sqrMagnitude < thresholdSqr)
        {
            aStarController.ArrivedPos();
            posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);
        }
        else
        {
            animator.SetBool("isWalking", true);
        }

        // 到达角落目标点
        if ((transform.position - targetPosition).sqrMagnitude < thresholdSqr || posToGo == Vector3.zero)
        {
            StartCoroutine(WaitAndMoveToNextCorner());
        }
    }

    private void Flip()
    {
        facingDirec *= -1;
        transform.localScale = new Vector3(-1 * transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    private void CalculateCornerPositions()
    {
        Vector3 center = transform.position;
        float halfWidth = patrolSize.x / 2f;
        float halfHeight = patrolSize.y / 2f;

        // 顺时针：右下 -> 左下 -> 左上 -> 右上
        cornerPositions[0] = center + new Vector3(halfWidth, -halfHeight, 0);
        cornerPositions[1] = center + new Vector3(-halfWidth, -halfHeight, 0);
        cornerPositions[2] = center + new Vector3(-halfWidth, halfHeight, 0);
        cornerPositions[3] = center + new Vector3(halfWidth, halfHeight, 0);
    }

    private int GetNearestCornerIndex()
    {
        int nearestIndex = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            float dist = (transform.position - cornerPositions[i]).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                nearestIndex = i;
            }
        }
        return nearestIndex;
    }

    IEnumerator WaitAndMoveToNextCorner()
    {
        isWaiting = true;
        animator.SetBool("isWalking", false);
        rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(waitTime);

        // 计算下一个角落索引
        if (clockwise)
        {
            currentCornerIndex = (currentCornerIndex + 1) % 4;
        }
        else
        {
            currentCornerIndex = (currentCornerIndex + 3) % 4;
        }

        targetPosition = cornerPositions[currentCornerIndex];
        aStarController.ResetPath();
        posToGo = aStarController.GetPosToGo(Vector3.zero, transform.position, targetPosition);

        isWaiting = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled)
            return;
        // 在编辑器中绘制巡逻路径
        Gizmos.color = Color.yellow;

        // 计算当前显示的角位置
        Vector3 center = Application.isPlaying ? cornerPositions[0] - (cornerPositions[0] - transform.position) : transform.position;
        float halfWidth = patrolSize.x / 2f;
        float halfHeight = patrolSize.y / 2f;


        // 绘制当前目标角
        if (Application.isPlaying && currentCornerIndex < 4)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(cornerPositions[currentCornerIndex], 0.3f);
        }
    }
}
