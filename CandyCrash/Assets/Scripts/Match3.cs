using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

    GridSystem2D<GridObject<Gem>> grid;
    InputReader inputReader;

    Vector2Int selectedGem = Vector2Int.one * -1;

    void Awake()
    {
        inputReader = GetComponent<InputReader>();
    }
    void Start()
    {
        InitializeGrid();
        inputReader.Fire += OnGemSelected;
    }
    void OnDestroy()
    {
        inputReader.Fire -= OnGemSelected;
    }
    bool IsEmptyPosition(Vector2Int gridPos) => grid.GetValue(gridPos.x, gridPos.y) == null;
    bool IsValidPosition(Vector2Int gridPos) => gridPos.x >= 0 && gridPos.y >= 0 && gridPos.x < width && gridPos.y < height;
    void SelectGem(Vector2Int gridPos) => selectedGem = gridPos;
    void DeselectGem() => selectedGem = Vector2Int.one * -1;
    void OnGemSelected(Vector2 screenPosition)
    {
        var gridPos = grid.GetXY(Camera.main.ScreenToWorldPoint(screenPosition));

        if (!IsValidPosition(gridPos) || IsEmptyPosition(gridPos))
        {
            return;
        }

        if (selectedGem == gridPos)
        {
            DeselectGem();
        }
        else if (selectedGem == Vector2Int.one * -1)
        {
            SelectGem(gridPos);
        }
        else
        {
            StartCoroutine(RunGameLoop(selectedGem, gridPos));
        }

    }
    IEnumerator RunGameLoop(Vector2Int gridPosA, Vector2Int gridPosB)
    {
        yield return StartCoroutine(SwapGems(gridPosA, gridPosB));

        List<Vector2Int> matches = FindMatches();

        yield return StartCoroutine(ExploadeGems(matches));
        yield return StartCoroutine(MakeGemsFall());
        yield return StartCoroutine(FillEmptySpots());


        DeselectGem();
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
                    // play sfx
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
                        .DOLocalMove(endValue: grid.GetWorldPositionCenter(x, y), duration: 0.5f)
                        .SetEase(gemEase);

                    // play sfx

                    yield return new WaitForSeconds(0.1f);
                    break;
                }
            }
        }
    }
    IEnumerator ExploadeGems(List<Vector2Int> matches)
    {
        // TODO: SFX play

        foreach (var match in matches)
        {
            var gem = grid.GetValue(match.x, match.y).GetValue();
            grid.SetValue(match.x, match.y, null);

            // ExplodeVFX(match);

            gem.transform.DOPunchScale(punch: Vector3.one * 0.1f, duration: 0.1f, vibrato: 1, elasticity: 0.5f);
            yield return new WaitForSeconds(0.1f);

            gem.DestroyGem();
        }
    }
    List<Vector2Int> FindMatches()
    {
        HashSet<Vector2Int> matches = new();

        // Horizontal
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                var gridgemA = grid.GetValue(x, y);
                var gridGemB = grid.GetValue(x + 1, y);
                var gridGemC = grid.GetValue(x + 2, y);

                if (gridgemA == null || gridGemB == null || gridGemC == null) continue;

                if (gridgemA.GetValue().GemType == gridGemB.GetValue().GemType && gridGemB.GetValue().GemType == gridGemC.GetValue().GemType)
                {
                    matches.Add(new Vector2Int(x, y));
                    matches.Add(new Vector2Int(x + 1, y));
                    matches.Add(new Vector2Int(x + 2, y));
                }
            }
        }

        // vertical
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                var gridgemA = grid.GetValue(x, y);
                var gridGemB = grid.GetValue(x, y + 1);
                var gridGemC = grid.GetValue(x, y + 2);

                if (gridgemA == null || gridGemB == null || gridGemC == null) continue;

                if (gridgemA.GetValue().GemType == gridGemB.GetValue().GemType && gridGemB.GetValue().GemType == gridGemC.GetValue().GemType)
                {
                    matches.Add(new Vector2Int(x, y));
                    matches.Add(new Vector2Int(x, y + 1));
                    matches.Add(new Vector2Int(x, y + 2));
                }
            }
        }

        return new List<Vector2Int>(matches);
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
