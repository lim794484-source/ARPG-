using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConversationHistoryManager : YSingleton<ConversationHistoryManager>, ISaveable
{
    private HashSet<string> charactersHasChated = new();
    private HashSet<string> dialogsHasChated = new();

    private void OnEnable()
    {
        if (DataManager.Instance != null) ((ISaveable)this).RegisterSaveable();
    }

    private void OnDisable()
    {
        if (DataManager.Instance != null) ((ISaveable)this).UnRegisterSaveable();
    }

    public void RecordCharacter(CharacterSO character)
    {
        string key = GetCharacterKey(character);
        if (!string.IsNullOrEmpty(key)) charactersHasChated.Add(key);
    }
    public bool HasChatedWith(CharacterSO character)
    {
        string key = GetCharacterKey(character);
        return !string.IsNullOrEmpty(key) && charactersHasChated.Contains(key);
    }

    public void RecordDialogHasChated(DialogSO dialog)
    {
        string key = GetDialogKey(dialog);
        if (!string.IsNullOrEmpty(key)) dialogsHasChated.Add(key);
    }
    public bool HasDialogChated(DialogSO dialog)
    {
        string key = GetDialogKey(dialog);
        return !string.IsNullOrEmpty(key) && dialogsHasChated.Contains(key);
    }

    public DataDefinition GetDataID() => null;

    public void SaveData(Data data)
    {
        if (data == null) return;
        data.chattedCharacterIds = new HashSet<string>(charactersHasChated);
        data.chattedDialogIds = new HashSet<string>(dialogsHasChated);
    }

    public void LoadData(Data data)
    {
        charactersHasChated = data?.chattedCharacterIds != null
            ? new HashSet<string>(data.chattedCharacterIds)
            : new HashSet<string>();
        dialogsHasChated = data?.chattedDialogIds != null
            ? new HashSet<string>(data.chattedDialogIds)
            : new HashSet<string>();
    }

    private static string GetCharacterKey(CharacterSO character)
    {
        if (character == null) return string.Empty;
        return !string.IsNullOrEmpty(character.Guid) ? character.Guid : character.name;
    }

    private static string GetDialogKey(DialogSO dialog)
    {
        return dialog != null ? dialog.name : string.Empty;
    }
}
