using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Gem : MonoBehaviour
{
    GemType _gemType;
    public GemType GemType
    {
        get { return _gemType; }
        set
        {
            GetComponent<SpriteRenderer>().sprite = value.sprite;
            _gemType = value;
        }
    }

    public void DestroyGem()
    {
        Destroy(gameObject);
    }
}
