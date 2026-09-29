using UnityEngine;

[DisallowMultipleComponent]
public class ShopPortraitCameraController : MonoBehaviour
{
    [SerializeField] private Vector3 followOffset = new(0f, 0f, -2f);
    [SerializeField] private bool hideWhenNoShopKeeper = true;

    private Camera cachedCamera;
    private Transform currentTarget;

    private void Awake()
    {
        cachedCamera = GetComponent<Camera>();
        SetCameraState(false);
    }

    private void OnEnable()
    {
        SetCameraState(false);
    }

    private void OnDisable()
    {
        currentTarget = null;
        SetCameraState(false);
    }

    private void LateUpdate()
    {
        if (ShopManager.Instance == null) return;

        Transform target = ShopManager.Instance.CurrentPortraitTarget;
        if (target != currentTarget)
        {
            currentTarget = target;
            SetCameraState(currentTarget != null);
        }

        if (currentTarget != null)
        {
            transform.position = currentTarget.position + followOffset;
        }
    }

    private void SetCameraState(bool state)
    {
        if (cachedCamera == null || !hideWhenNoShopKeeper) return;

        cachedCamera.enabled = state;
    }
}
