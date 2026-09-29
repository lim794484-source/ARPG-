using UnityEngine;

public class ToggleSkillTree : MonoBehaviour, ICanvasManager
{
    [SerializeField] private CanvasGroup skillsCanvas;
    [SerializeField] private ToggleCanvasEventSO toggleSkillEvent;
    public ToggleCanvasEventSO ToggleCanvasEvent => toggleSkillEvent;
    private Canvas canvas;

    private void Awake()
    {
        canvas = skillsCanvas.GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        toggleSkillEvent.toggleCanvasEvent += OnToggleSkillEvent;
        toggleSkillEvent.focusEvent += OnFocus;
    }
    private void OnDisable()
    {
        toggleSkillEvent.toggleCanvasEvent -= OnToggleSkillEvent;
        toggleSkillEvent.focusEvent -= OnFocus;

    }
    private void OnToggleSkillEvent(bool state)
    {
        ((ICanvasManager)this).ToggleCanvas(skillsCanvas, canvas, MyEnums.CanvasToToggle.Skills, state);
    }
    private void OnFocus()
    {
        ((ICanvasManager)this).RefreshCanvaOrder(canvas, MyEnums.CanvasToToggle.Skills, true);
    }
}
