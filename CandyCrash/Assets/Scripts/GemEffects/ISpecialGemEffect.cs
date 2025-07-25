using System.Collections;
using UnityEngine;

public interface ISpecialGemEffect
{
    IEnumerator Execute(Vector2Int position, Match3 context);
}
