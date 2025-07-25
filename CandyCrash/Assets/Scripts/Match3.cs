using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
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
    [SerializeField] GameObject horizontalExplosionVfx;
    [SerializeField] int seed = 1234;

    GridSystem2D<GridObject<Gem>> grid;
    InputReader inputReader;
    AudioManager audioManager;
    HashSet<Vector2Int> dirtyPoints;
    const int minMatchCount = 3;

    Vector2Int selectedGem;
    System.Random gemRandom;
    bool isBussy;
    int testCount;

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
        dirtyPoints = new();
    }
    void OnDisable()
    {
        inputReader.Fire -= OnGemSelected;
    }
    bool IsEmptyPosition(Vector2Int gridPos) => grid.GetValue(gridPos.x, gridPos.y) == null;
    bool IsValidPosition(Vector2Int gridPos) => gridPos.x >= 0 && gridPos.y >= 0 && gridPos.x < width && gridPos.y < height;
    void SelectGem(Vector2Int gridPos)
    {
        selectedGem = gridPos;
        var gem = grid.GetValue(gridPos.x, gridPos.y).GetValue();
        gem.OnSelect();
    }
    void DeselectGem()
    {
        var gem = grid.GetValue(selectedGem.x, selectedGem.y).GetValue();
        selectedGem = Vector2Int.one * -1;
        gem.OnDeselect();
    }
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
        testCount = 0;
        dirtyPoints.Clear();

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
            DeselectGem();

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
                yield break;
            }

            audioManager.PlayMatch();

            yield return StartCoroutine(ProcessChainReactions(new List<List<Vector2Int>> { firstMatches.ToList(), secondMatches.ToList() }));
        }
        finally
        {
            isBussy = false;
            Debug.Log(testCount);
        }
    }
    IEnumerator ProcessChainReactions(List<List<Vector2Int>> matchesLists)
    {
        // Process initial matches
        yield return StartCoroutine(ExplodeGems(matchesLists));
        yield return StartCoroutine(MakeGemsFall());
        yield return StartCoroutine(FillEmptySpots());

        // Keep checking for new matches until no more are found
        bool foundNewMatches;
        do
        {
            foundNewMatches = false;
            var newMachesList = GetNewMatchList();

            // If we found new matches, process them
            if (newMachesList.Count > 0)
            {
                audioManager.PlayMatch();
                yield return StartCoroutine(ExplodeGems(newMachesList));
                yield return StartCoroutine(MakeGemsFall());
                yield return StartCoroutine(FillEmptySpots());
                foundNewMatches = true;
            }

        } while (foundNewMatches);
    }

    List<List<Vector2Int>> GetNewMatchList()
    {
        List<List<Vector2Int>> newMatchList = new();
        HashSet<Vector2Int> visitedPoints = new();

        foreach (var point in dirtyPoints)
        {
            if (grid.GetValue(point.x, point.y) == null) continue;
            if (visitedPoints.Contains(point)) continue;

            var newMatches = FindMatches(point);
            if (newMatches.Count > 0)
            {
                // Filter out already-visited points from this match group
                List<Vector2Int> uniqueMatches = newMatches
                    .Where(p => !visitedPoints.Contains(p))
                    .ToList();

                if (uniqueMatches.Count > 0)
                {
                    newMatchList.Add(uniqueMatches);
                    visitedPoints.UnionWith(uniqueMatches); // Mark these as visited
                }
            }
        }

        dirtyPoints.Clear();
        return newMatchList;
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
                    dirtyPoints.Add(new Vector2Int(x, y));

                    audioManager.PlayPop();

                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
        yield return null;
    }
    IEnumerator MakeGemsFall()
    {
        for (int y = 0; y < height; y++)
        {
            bool fallHappenned = false;
            for (int x = 0; x < width; x++)
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

                    dirtyPoints.Add(new Vector2Int(x, y));

                    fallHappenned = true;
                    break;
                }
            }
            if (fallHappenned)
            {
                audioManager.PlayWoosh();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    IEnumerator ExplodeGems(List<List<Vector2Int>> matchesLists)
    {
        audioManager.PlayPop();

        int maxCount = 0;
        foreach (var list in matchesLists)
        {
            if (list.Count > maxCount)
                maxCount = list.Count;
        }

        for (int i = 0; i < maxCount; i++)
        {
            foreach (var list in matchesLists)
            {
                if (i < list.Count)
                {
                    if (i == 0)
                    {
                        yield return StartCoroutine(TryCreateSpecialGem(list));
                    }
                    else
                    {
                        yield return StartCoroutine(ExplodeSingleGem(list[i]));
                    }
                }
            }
        }
    }

    IEnumerator TryCreateSpecialGem(List<Vector2Int> matchList)
    {
        var match = matchList[0];
        var gridObject = grid.GetValue(match.x, match.y);
        if (gridObject == null) yield break;

        var gem = gridObject.GetValue();

        bool allXAreDifferent = matchList.Select(point => point.x).Distinct().Count() == 3;
        if (allXAreDifferent)
        //if (allXAreDifferent && matchList.Count == 4)
        {
            gem.SpecialGemEffect = new ClearRowEffect();
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        // couldnt create special gem
        yield return StartCoroutine(ExplodeSingleGem(match));
    }

    IEnumerator ExplodeSingleGem(Vector2Int match)
    {
        var gridObject = grid.GetValue(match.x, match.y);
        if (gridObject == null) yield break;

        var gem = gridObject.GetValue();

        ExplodeVFX(match);
        gem.transform.DOPunchScale(punch: Vector3.one * 0.1f, duration: 0.1f, vibrato: 1, elasticity: 0.5f);
        grid.SetValue(match.x, match.y, null);
        dirtyPoints.Add(match);

        gem.DestroyGem();

        if (gem.SpecialGemEffect != null)
        {
            yield return StartCoroutine(gem.SpecialGemEffect.Execute(match, this));
        }

        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator ExplodeRow(Vector2Int match)
    {
        ExplodeHorizontalVFX(match);
        yield return new WaitForSeconds(0.4f);

        for (int x = 0; x < width; x++)
        {
            var gridObject = grid.GetValue(x, match.y);
            if (gridObject == null) continue;

            StartCoroutine(ExplodeSingleGem(new Vector2Int(x, match.y)));
        }

        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator ExplodeColumn(int column)
    {
        for (int y = 0; y < height; y++)
        {
            var gridObject = grid.GetValue(column, y);
            if (gridObject == null) continue;

            yield return StartCoroutine(ExplodeSingleGem(new Vector2Int(column, y)));
        }
    }

    void ExplodeVFX(Vector2Int match)
    {
        Vector3 spawnPos = grid.GetWorldPositionCenter(match.x, match.y);
        StartCoroutine(RelaseVfxDelay(ObjectPoolManager.SpawnObject(explosionVfx, spawnPos, Quaternion.identity, ObjectPoolManager.PoolType.ParticleSystem)));
    }
    void ExplodeHorizontalVFX(Vector2Int match)
    {
        Vector3 spawnPos = grid.GetWorldPositionCenter(match.x, match.y);
        StartCoroutine(RelaseVfxDelay(ObjectPoolManager.SpawnObject(horizontalExplosionVfx, spawnPos, Quaternion.identity, ObjectPoolManager.PoolType.ParticleSystem)));
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

        if (result.Count < minMatchCount)
        {
            result.Clear();
        }

        if (result.Count == minMatchCount)
        {
            bool allXAreDifferent = result.Select(point => point.x).Distinct().Count() == 3;
            bool allYAreDifferent = result.Select(point => point.y).Distinct().Count() == 3;

            if (allXAreDifferent || allYAreDifferent)
            {
                return result;
            }
            result.Clear();
        }

        return result;
    }
    void FindMatchesDFS(GemType gemType, Vector2Int currentPoint, HashSet<Vector2Int> result)
    {
        testCount++;

        // bound check
        if (!IsValidPosition(currentPoint)) return;

        // null grid
        if (IsEmptyPosition(currentPoint)) return;

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
        gemRandom = new(seed);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                CreateGem(x, y);
                CheckMatch(x, y);
            }
        }
    }
    void CheckMatch(int x, int y)
    {
        List<GemType> possibleTypes = new(gemTypes);

        var result = FindMatches(new Vector2Int(x, y));
        Gem createdGem = grid.GetValue(x, y).GetValue();
        if (result.Count >= minMatchCount)
        {
            possibleTypes.Remove(createdGem.GemType);
            createdGem.GemType = possibleTypes[gemRandom.Next(0, possibleTypes.Count)];
        }
    }
    void CreateGem(int x, int y)
    {
        var gem = ObjectPoolManager.SpawnObject(gemPrefab, grid.GetWorldPositionCenter(x, y), Quaternion.identity);
        gem.GemType = gemTypes[gemRandom.Next(0, gemTypes.Length)];

        var gridObject = new GridObject<Gem>(grid, x, y);
        gridObject.SetValue(gem);
        grid.SetValue(x, y, gridObject);
    }
}
