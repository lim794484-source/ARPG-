using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneToggler : MonoBehaviour
{
    [SerializeField] private SceneLoadEventSO loadEventSO;
    [SerializeField] private Vector3 newPosition;
    [SerializeField] private GameSceneSO sceneToLoad;
    [SerializeField] private bool isToFade = true;
    private void OnTriggerEnter2D(Collider2D collider)
    {
        loadEventSO.RaiseLoadRequestEvent(sceneToLoad, newPosition, isToFade);
    }
}
