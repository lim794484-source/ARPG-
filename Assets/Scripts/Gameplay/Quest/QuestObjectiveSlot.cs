using TMPro;
using UnityEngine;

public class QuestObjectiveSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text trackingText;
    public void RefreshObjectives(string description, string progress, bool isCompleted)
    {
        objectiveText.text = description;
        trackingText.text = progress;

        Color color = isCompleted ? Color.gray : Color.white;
        objectiveText.color = color;
        trackingText.color = color;
    }
}
