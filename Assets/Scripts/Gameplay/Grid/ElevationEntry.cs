using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevation_Entry : MonoBehaviour
{
    [SerializeField] private Collider2D[] mountainColliders;
    [SerializeField] private Collider2D[] edgeColliders;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            foreach (Collider2D collider in mountainColliders)
            {
                collider.enabled = false;
            }
            foreach (Collider2D edge in edgeColliders)
            {
                edge.enabled = true;
            }

        }
        else return;
        collision.gameObject.GetComponent<SpriteRenderer>().sortingOrder = 15;
    }
}
