using System;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "LoadQuestEventSO", menuName = "Events/LoadQuestEventSO", order = 0)]

public class LoadQuestEventSO : ScriptableObject
{
    public event Action<List<QuestSO>> LoadQuestEvent;
    public void OnLoadQuestEventRaised(List<QuestSO> quests)
    {
        LoadQuestEvent?.Invoke(quests);
    }
}
