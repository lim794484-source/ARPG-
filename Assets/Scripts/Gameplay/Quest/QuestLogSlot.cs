using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestLogSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questLevelText;
    public QuestSO currentQuest;
    public QuestLogUI questLogUI;
    private CanvasGroup slotCanvas;

    private void Awake()
    {
        slotCanvas = GetComponent<CanvasGroup>();
        ResetSlotState();
    }

    private void OnValidate()
    {
        gameObject.SetActive(false);
        if (currentQuest != null)
            SetQuest(currentQuest);
        
    }

    public void SetQuest(QuestSO quest)
    {

        currentQuest = quest;
        questNameText.text = quest.questName;
        questLevelText.text = "等级." + quest.lv;
        ResetSlotState();
        gameObject.SetActive(true);
    }
    public void SetQuestActive(bool state)
    {
        if (!state)
        {
            currentQuest = null;
        }

        ResetSlotState();
        gameObject.SetActive(state);
    }

    public void ResetSlotState()
    {
        if (slotCanvas == null)
        {
            slotCanvas = GetComponent<CanvasGroup>();
        }

        if (slotCanvas == null) return;

        slotCanvas.alpha = 1f;
        slotCanvas.interactable = true;
        slotCanvas.blocksRaycasts = true;
    }

    public void OnSlotClicked()
    {
        questLogUI.HandleQuestClicked(currentQuest);
    }

}
