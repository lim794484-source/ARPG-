using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "QuestOptionsEventSO", menuName = "Events/QuestOptionsEventSO", order = 0)]

public class QuestOptionsEventSO : ScriptableObject
{
    public event Action<MyEnums.QuestState> questOptionsEvent;

    public void OnQuestOptionsEventRaised(MyEnums.QuestState questState)
    {
        questOptionsEvent?.Invoke(questState);
    }

}

