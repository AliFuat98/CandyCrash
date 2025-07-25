using System.Collections;
using UnityEngine;

public class ClearRowEffect : ISpecialGemEffect
{
    public IEnumerator Execute(Vector2Int position, Match3 context)
    {
        yield return context.ExplodeRow(position);
    }
}