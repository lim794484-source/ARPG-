using Cinemachine;
using UnityEngine;

/// <summary>
/// 异形屏自适应 Ortho Size：宽高比变大（手机/带鱼屏）时缩小 Ortho Size，
/// 让水平视口不超过背景地图宽度，避免边缘穿帮。
///
/// 原理：正交相机 Ortho Size 固定 → 高度恒定，宽度随宽高比变大 → 超出背景 → 穿帮。
/// 解法：宽高比变大时按 maxHalfWidth 反推更小的 Ortho Size，宽度优先，宁可上下多显示也不穿帮。
///
/// 挂载：常驻虚拟相机（PersistentScene 的 Virtual Camera），所有场景共用。
/// Confiner 约束的是相机移动，本脚本约束的是视口大小，两者正交互不干扰。
/// </summary>
[RequireComponent(typeof(CinemachineVirtualCamera))]
public class AdaptiveOrthoSize : MonoBehaviour
{
    [Tooltip("基础 Ortho Size（标准宽高比下用这个值）。留 0 则自动取挂载时虚拟相机的当前值。")]
    [SerializeField] private float baseOrthoSize = 0;

    [Tooltip("背景地图允许的最大半宽。宽高比变大导致视口半宽超过此值时，自动缩小 Ortho Size。"
             + "取最严格场景（通常是菜单）的背景半宽，如菜单背景宽 24.18 则填 12.09。")]
    [SerializeField] private float maxHalfWidth = 12.09f;

    private CinemachineVirtualCamera vcam;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();

        // 若未手动配置 baseOrthoSize，自动取当前虚拟相机的值作为基准
        if (baseOrthoSize <= 0)
            baseOrthoSize = vcam.m_Lens.OrthographicSize;
    }

    private void Start()
    {
        ApplyOrthoSize();
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private void Update()
    {
        // 分辨率没变就不重算，零开销
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight)
            return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        ApplyOrthoSize();
    }

    private void ApplyOrthoSize()
    {
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
        float requiredHalfWidth = baseOrthoSize * aspect;

        // 需要的半宽超过背景限制 → 按 maxHalfWidth 反推更小的 Ortho Size
        // 否则用基础值（窄屏优先高度，宁可上下多显示）
        float orthoSize = requiredHalfWidth > maxHalfWidth
            ? maxHalfWidth / aspect
            : baseOrthoSize;

        vcam.m_Lens.OrthographicSize = orthoSize;
    }
}
