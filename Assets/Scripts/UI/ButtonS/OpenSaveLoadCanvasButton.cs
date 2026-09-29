using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class OpenSaveLoadCanvasButton : MonoBehaviour
{
    [SerializeField] private ToggleCanvasEventSO toggleSaveLoadCanvasEvent;
    [SerializeField] private Button OpenButton;
    private void OnEnable()
    {
        OpenButton.onClick.AddListener(

            () =>
            {
                toggleSaveLoadCanvasEvent.RaiseToggleCanvasEvent(true);
            }
            );
    }
    private void OnDisable()
    {
        OpenButton.onClick.RemoveAllListeners();
    }
}
