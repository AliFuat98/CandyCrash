using System.Collections;
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

    bool IsEmptyPosition(Vector2Int gridPos) => grid.GetValue(gridPos.x, gridPos.y) == null;
    bool IsValidPosition(Vector2Int gridPos) => gridPos.x >= 0 && gridPos.y >= 0 && gridPos.x < width && gridPos.y < height;
    void SelectGem(Vector2Int gridPos) => selectedGem = gridPos;
    void DeselectGem() => selectedGem = Vector2Int.one * -1;
    IEnumerator RunGameLoop(Vector2Int gridPosA, Vector2Int gridPosB)
    {
        yield return SwapGems(gridPosA, gridPosB);

        DeselectGem();
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
