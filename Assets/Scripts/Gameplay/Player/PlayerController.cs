using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : YSingleton<PlayerController>
{
    [SerializeField, FormerlySerializedAs("Transform")] private Transform playerTransform;

    public Vector3 GetPosition() => playerTransform.position;
}
