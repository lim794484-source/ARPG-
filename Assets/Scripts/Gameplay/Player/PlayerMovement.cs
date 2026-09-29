using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using MyEnums;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerBow playerBow;
    [SerializeField] private Joystick joystick;
    [SerializeField] private Transform visualRoot;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference slashAction;
    [SerializeField] private InputActionReference shootAction;

    [Header("Action Finished Events")]
    [SerializeField] private VoidEventSO slashActionFinishedEvent;
    [SerializeField] private VoidEventSO shootActionFinishedEvent;

    private int facingDirection = 1;//默认朝向为右
    private bool canBeInterrupted = true;//是否可以被打断，攻击和射击动画期间不可被打断

    private float timer = 0;//计时器，暂时未使用

    private PlayerState playerState = PlayerState.Idle;

    private void OnEnable()
    {
        canBeInterrupted = true;
        timer = 0;

        // 场景切换（ForbidInput/AllowInput）会禁用再启用 PlayerMovement，
        // 必须走 AnimatorSM 清掉残留动画布尔并清空速度，否则进新场景仍保持旧的移动状态
        AnimatorSM(PlayerState.Idle);
        if (rb != null) rb.velocity = Vector2.zero;

        if (moveAction != null) moveAction.action.Enable();
        if (slashAction != null) slashAction.action.Enable();
        if (shootAction != null) shootAction.action.Enable();

        if (slashActionFinishedEvent != null) slashActionFinishedEvent.VoidEvent += OnActionFinished;
        if (shootActionFinishedEvent != null) shootActionFinishedEvent.VoidEvent += OnActionFinished;
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (slashAction != null) slashAction.action.Disable();
        if (shootAction != null) shootAction.action.Disable();

        if (slashActionFinishedEvent != null) slashActionFinishedEvent.VoidEvent -= OnActionFinished;
        if (shootActionFinishedEvent != null) shootActionFinishedEvent.VoidEvent -= OnActionFinished;
    }

    /// <summary>
    /// 统一的动作结束处理：回到 Idle、清除攻击/射击动画标记、解锁输入、重置冷却。
    /// 由近战结束、射击结束、切换装备三个来源通过 SO 事件触发。
    /// </summary>
    private void OnActionFinished()
    {
        AnimatorSM(PlayerState.Idle);
        animator.SetBool("isAttacking", false);
        animator.SetBool("isShooting", false);
        SetCanBeInterrupted(true);
        ResetTimer();
    }

    public void AnimatorSM(PlayerState newState)//用于切换动画
    {
        //退出当前动画

        if (playerState == PlayerState.Attacking)
        {
            animator.SetBool("isAttacking", false);
        }
        else if (playerState == PlayerState.Shooting)
        {
            animator.SetBool("isShooting", false);
        }
        else if (playerState == PlayerState.Running)
        {
            animator.SetBool("isRunning", false);
        }
        // else if (playerState == PlayerState.Idle)
        // {
        // }
        //更新状态
        playerState = newState;
        //进入新动画
        if (playerState == PlayerState.Attacking)
        {
            animator.SetBool("isAttacking", true);
        }
        else if (playerState == PlayerState.Shooting)
        {
            animator.SetBool("isShooting", true);
        }
        else if (playerState == PlayerState.Running)
        {
            animator.SetBool("isRunning", true);
        }
        else if (playerState == PlayerState.KnockBack)
        {
        }

    }

    private void Update()
    {
        if (timer >= 0)
            timer -= Time.deltaTime;

        if (!canBeInterrupted)
            return;
        else MovementSM();
    }
    private void MovementSM()
    {
        // 如果处于KnockBack状态，不处理其他状态转换
        if (playerState == PlayerState.KnockBack)
        {
            HandleKnockBackState();
            return;
        }
        if (slashAction != null && slashAction.action.WasPressedThisFrame() && playerCombat.IsActive && timer < 0)
        {
            AnimatorSM(PlayerState.Attacking);
        }
        else if (shootAction != null && shootAction.action.WasPressedThisFrame() && playerBow.IsActive && timer < 0)
        {
            AnimatorSM(PlayerState.Shooting);
        }
        else if (IsToRunning())
        {
            AnimatorSM(PlayerState.Running);
        }
        else if (!IsToRunning())
        {
            AnimatorSM(PlayerState.Idle);
        }

        switch (playerState)//用于执行逻辑
        {
            case PlayerState.Idle:
                HandleIdleState();
                break;
            case PlayerState.Running:
                HandleRunningState();
                break;
            case PlayerState.Attacking:
                HandleAttackingState();
                break;
            case PlayerState.Shooting:
                HandleShootingState();
                break;

        }
    }
    public void SetCanBeInterrupted(bool value)
    {
        canBeInterrupted = value;
    }
    public void ResetTimer()
    {
        timer = StatsManager.Instance.GetCoolDown();
    }
    private bool IsToRunning()
    {
        if (joystick != null && (Mathf.Abs(joystick.Horizontal) > 0.1f || Mathf.Abs(joystick.Vertical) > 0.1f))
            return true;
        Vector2 v = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        return Mathf.Abs(v.x) > 0 || Mathf.Abs(v.y) > 0;
    }

    private void HandleKnockBackState()
    {
    }

    private void HandleShootingState()
    {
        SetMovement(0, 0);
        canBeInterrupted = false;
    }

    private void HandleAttackingState()
    {
        SetMovement(0, 0);
        canBeInterrupted = false;
    }

    private void HandleIdleState()
    {
        SetMovement(0, 0);
    }

    private void HandleRunningState()
    {
        float horizontal, vertical;
        if (joystick != null && (Mathf.Abs(joystick.Horizontal) > 0.1f || Mathf.Abs(joystick.Vertical) > 0.1f))
        {
            horizontal = joystick.Horizontal;
            vertical = joystick.Vertical;
        }
        else
        {
            Vector2 v = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            horizontal = v.x;
            vertical = v.y;
        }

        //判断（仅）水平输入值和当前角色朝向的符号是否一致，否（意味着玩家将要转向）则调用翻转
        Transform root = GetPlayerRoot();
        if (horizontal * root.localScale.x < 0)
            Flip();

        //将animator的horizontal参数的值设定为变量的值
        SetMovement(horizontal, vertical);
    }
    private void SetMovement(float horizontal, float vertical)
    {
        rb.velocity = new Vector2(horizontal, vertical) * StatsManager.Instance.GetSpeed();
    }

    private void Flip()
    {
        facingDirection *= -1;
        Transform root = GetPlayerRoot();
        root.localScale =
            new Vector3(-root.localScale.x,
            root.localScale.y,
            root.localScale.z);
    }
    public int getFacingDirection()
    {
        return this.facingDirection;
    }
    public void KnockBack(Transform enemy, float force, float stunTime)
    {
        if (!isActiveAndEnabled)
            return;
        playerState = PlayerState.KnockBack;
        Vector2 direction = (GetPlayerRoot().position - enemy.position).normalized;

        Vector2 knockBackVelocity = direction * force;
        SetMovement(knockBackVelocity.x, knockBackVelocity.y);

        StartCoroutine(KnockBackCounter(stunTime));
    }

    private Transform GetPlayerRoot()
    {
        if (visualRoot != null)
            return visualRoot;
        if (rb != null)
            return rb.transform;
        return transform;
    }
    public PlayerState GetPlayerState()
    {
        return playerState;
    }
    IEnumerator KnockBackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        canBeInterrupted = true;
        AnimatorSM(PlayerState.Idle);
    }

}

