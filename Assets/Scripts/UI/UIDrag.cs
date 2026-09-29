using UnityEngine;
using UnityEngine.EventSystems;

public class UIDrag : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    [SerializeField] private RectTransform dragTarget;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private MyEnums.CanvasToToggle dragCanvasType;
    private Vector2 offset;
    private Vector2 originPosition;
    private bool wasVisible;

    private void Start()
    {
        originPosition = dragTarget.anchoredPosition;
        wasVisible = canvasGroup != null && canvasGroup.alpha > 0.01f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        offset = dragTarget.anchoredPosition - eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragTarget.anchoredPosition = eventData.position + offset;
        UIManager.Instance.HandleFocus(dragCanvasType);
    }

    private void LateUpdate()
    {
        if (canvasGroup == null) return;
        bool isVisible = canvasGroup.alpha > 0.01f;
        if (wasVisible && !isVisible)
            dragTarget.anchoredPosition = originPosition;
        wasVisible = isVisible;
    }
}
