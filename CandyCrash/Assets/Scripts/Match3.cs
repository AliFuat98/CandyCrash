using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;

public class Match3 : MonoBehaviour
{
    [SerializeField] int width = 8;
    [SerializeField] int height = 8;
    [SerializeField] float cellSize = 1f;
    [SerializeField] Vector3 originPosition = Vector3.zero;
    [SerializeField] bool debug = true;

    [SerializeField] Ease gemEase = Ease.InQuad;
    [SerializeField] Gem gemPrefab;
    [SerializeField] GemType[] gemTypes;
    [SerializeField] GameObject explosionVfx;

    GridSystem2D<GridObject<Gem>> grid;
    InputReader inputReader;
    AudioManager audioManager;

    Vector2Int selectedGem;
    bool isBussy;

    void Awake()
    {
        inputReader = GetComponent<InputReader>();
        audioManager = GetComponent<AudioManager>();
    }
    void Start()
    {
        InitializeGrid();
        inputReader.Fire += OnGemSelected;
        selectedGem = Vector2Int.one * -1;
        isBussy = false;
    }
    void OnDisable()
    {
        inputReader.Fire -= OnGemSelected;
    }
    bool IsEmptyPosition(Vector2Int gridPos) => grid.GetValue(gridPos.x, gridPos.y) == null;
    bool IsValidPosition(Vector2Int gridPos) => gridPos.x >= 0 && gridPos.y >= 0 && gridPos.x < width && gridPos.y < height;
    void SelectGem(Vector2Int gridPos) => selectedGem = gridPos;
    void DeselectGem() => selectedGem = Vector2Int.one * -1;
    bool IsSameGemType(Vector2Int posA, Vector2Int posB)
    {
        var gemtypeA = grid.GetValue(posA.x, posA.y).GetValue().GemType;
        var gemtypeB = grid.GetValue(posB.x, posB.y).GetValue().GemType;

        return gemtypeA == gemtypeB;
    }
    bool IsAdjacent(Vector2Int posA, Vector2Int posB)
    {
        Vector2Int[] adjancents = {
            new(posA.x,posA.y+1), // up
            new(posA.x,posA.y-1), // down
            new(posA.x+1,posA.y), // right
            new(posA.x-1,posA.y) // left
        };
        return adjancents.Contains(posB);
    }

    void OnGemSelected(Vector2 screenPosition)
    {
        if (isBussy) return;

        var gridPos = grid.GetXY(Camera.main.ScreenToWorldPoint(screenPosition));

        // Invalid Position
        if (!IsValidPosition(gridPos) || IsEmptyPosition(gridPos))
        {
            return;
        }

        // SELECT
        if (selectedGem == Vector2Int.one * -1)
        {
            SelectGem(gridPos);
            audioManager.PlayClick();
            return;
        }

        // Same Position selected
        if (selectedGem == gridPos)
        {
            DeselectGem();
            audioManager.PlayDeselect();
            return;
        }

        // not neighbour
        if (!IsAdjacent(gridPos, selectedGem))
        {
            DeselectGem();
            audioManager.PlayNoMatch();
            return;
        }

        isBussy = true;
        StartCoroutine(RunGameLoop(selectedGem, gridPos));
    }
    IEnumerator RunGameLoop(Vector2Int gridPosA, Vector2Int gridPosB)
    {
        try
        {
            yield return StartCoroutine(SwapGems(gridPosA, gridPosB));

            HashSet<Vector2Int> firstMatches = FindMatches(gridPosA);
            HashSet<Vector2Int> secondMatches;

            if (!IsSameGemType(gridPosA, gridPosB))
            {
                secondMatches = FindMatches(gridPosB);
            }
            else
            {
                secondMatches = new();
            }

            if (firstMatches.Count == 0 && secondMatches.Count == 0)
            {
                audioManager.PlayNoMatch();
                yield return StartCoroutine(SwapGems(gridPosB, gridPosA));
                DeselectGem();
                yield break;
            }

            audioManager.PlayMatch();

            yield return StartCoroutine(ExploadeGems(firstMatches.ToList(), secondMatches.ToList()));
            yield return StartCoroutine(MakeGemsFall());
            yield return StartCoroutine(FillEmptySpots());

            DeselectGem();
        }
        finally
        {
            isBussy = false;
        }
    }
    IEnumerator FillEmptySpots()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid.GetValue(x, y) == null)
                {
                    CreateGem(x, y);
                    audioManager.PlayPop();

                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
        yield return null;
    }
    IEnumerator MakeGemsFall()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid.GetValue(x, y) != null) continue;
                // x,y is empty here

                for (int i = y + 1; i < height; i++)
                {
                    if (grid.GetValue(x, i) == null) continue;
                    // x,y+1 is filled

                    var upperGem = grid.GetValue(x, i).GetValue();
                    grid.SetValue(x, y, grid.GetValue(x, i));
                    grid.SetValue(x, i, null);

                    upperGem.transform
                        .DOLocalMove(endValue: grid.GetWorldPositionCenter(x, y), duration: 0.1f)
                        .SetEase(gemEase);

                    // play sfx
                    audioManager.PlayWoosh();

                    yield return new WaitForSeconds(0.1f);
                    break;
                }
            }
        }
    }
    IEnumerator ExploadeGems(List<Vector2Int> firstMatches, List<Vector2Int> secondMatches)
    {
        audioManager.PlayPop();

        int firstCount = firstMatches.Count;
        int secondCount = secondMatches.Count;

        int minCount = Mathf.Min(firstCount, secondCount);
        for (int i = 0; i < minCount; i++)
        {
            yield return StartCoroutine(ExplodeSingleGem(firstMatches[i]));
            yield return StartCoroutine(ExplodeSingleGem(secondMatches[i]));
        }

        // Handle remaining gems in the longer list (if any)
        if (firstCount > secondCount)
        {
            for (int i = minCount; i < firstCount; i++)
            {
                yield return StartCoroutine(ExplodeSingleGem(firstMatches[i]));
            }
        }
        else if (secondCount > firstCount)
        {
            for (int i = minCount; i < secondCount; i++)
            {
                yield return StartCoroutine(ExplodeSingleGem(secondMatches[i]));
            }
        }
    }
    IEnumerator ExplodeSingleGem(Vector2Int match)
    {
        var gem = grid.GetValue(match.x, match.y).GetValue();
        grid.SetValue(match.x, match.y, null);

        ExplodeVFX(match);

        gem.transform.DOPunchScale(punch: Vector3.one * 0.1f, duration: 0.1f, vibrato: 1, elasticity: 0.5f);
        gem.DestroyGem();
        yield return new WaitForSeconds(0.1f);
    }

    void ExplodeVFX(Vector2Int match)
    {
        Vector3 spawnPos = grid.GetWorldPositionCenter(match.x, match.y);
        StartCoroutine(RelaseVfxDelay(ObjectPoolManager.SpawnObject(explosionVfx, spawnPos, Quaternion.identity, ObjectPoolManager.PoolType.ParticleSystem)));
    }

    IEnumerator RelaseVfxDelay(GameObject obj)
    {
        yield return new WaitForSeconds(0.5f);
        ObjectPoolManager.ReturnObjectToPool(obj, ObjectPoolManager.PoolType.ParticleSystem);
    }

    HashSet<Vector2Int> FindMatches(Vector2Int gridPos)
    {
        var gemType = grid.GetValue(gridPos.x, gridPos.y).GetValue().GemType;
        HashSet<Vector2Int> result = new();
        FindMatchesDFS(gemType, gridPos, result);

        if (result.Count < 3)
        {
            result.Clear();
        }

        return result;
    }

    void FindMatchesDFS(GemType gemType, Vector2Int currentPoint, HashSet<Vector2Int> result)
    {
        // bound check
        if (!IsValidPosition(currentPoint)) return;

        // already visited
        if (result.Contains(currentPoint)) return;

        // not same type
        var gem = grid.GetValue(currentPoint.x, currentPoint.y);
        if (gem == null || gem.GetValue().GemType != gemType) return;

        result.Add(currentPoint);

        FindMatchesDFS(gemType, new Vector2Int(currentPoint.x, currentPoint.y + 1), result); // up
        FindMatchesDFS(gemType, new Vector2Int(currentPoint.x, currentPoint.y - 1), result); // down
        FindMatchesDFS(gemType, new Vector2Int(currentPoint.x + 1, currentPoint.y), result); // right
        FindMatchesDFS(gemType, new Vector2Int(currentPoint.x - 1, currentPoint.y), result); // left
    }

    IEnumerator SwapGems(Vector2Int gridPosA, Vector2Int gridPosB)
    {
        var gridObjectA = grid.GetValue(gridPosA.x, gridPosA.y);
        var gridObjectB = grid.GetValue(gridPosB.x, gridPosB.y);

        gridObjectA.GetValue().transform
            .DOLocalMove(endValue: grid.GetWorldPositionCenter(gridPosB.x, gridPosB.y), duration: 0.5f)
            .SetEase(gemEase);

        gridObjectB.GetValue().transform
            .DOLocalMove(endValue: grid.GetWorldPositionCenter(gridPosA.x, gridPosA.y), duration: 0.5f)
            .SetEase(gemEase);

        grid.SetValue(gridPosA.x, gridPosA.y, gridObjectB);
        grid.SetValue(gridPosB.x, gridPosB.y, gridObjectA);

        yield return new WaitForSeconds(0.5f);
    }

    void InitializeGrid()
    {
        grid = GridSystem2D<GridObject<Gem>>.VerticalGrid(width, height, cellSize, originPosition, debug);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateGem(x, y);
            }
        }
    }
    void CreateGem(int x, int y)
    {
        Gem gem = Instantiate(gemPrefab, grid.GetWorldPositionCenter(x, y), Quaternion.identity, transform);
        gem.GemType = gemTypes[Random.Range(0, gemTypes.Length)];

        var gridObject = new GridObject<Gem>(grid, x, y);
        gridObject.SetValue(gem);
        grid.SetValue(x, y, gridObject);
    }
}
