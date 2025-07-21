using UnityEngine;

public class Match3 : MonoBehaviour {
    [SerializeField] int width = 8;
    [SerializeField] int height = 8;
    [SerializeField] float cellSize = 1f;
    [SerializeField] Vector3 originPosition = Vector3.zero;
    [SerializeField] bool debug = true;

    [SerializeField] Gem gemPrefab;
    [SerializeField] GemType[] gemTypes;

    GridSystem2D<GridObject<Gem>> grid;

    void Start() {
        InitializeGrid();
    }

    void InitializeGrid() {
        grid = GridSystem2D<GridObject<Gem>>.VerticalGrid(width, height, cellSize, originPosition, debug);

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                CreateGem(x, y);
            }
        }
    }

    void CreateGem(int x, int y) {
        Gem gem = Instantiate(gemPrefab, grid.GetWorldPositionCenter(x, y), Quaternion.identity, transform);
        gem.GemType = gemTypes[Random.Range(0, gemTypes.Length)];

        var gridObject = new GridObject<Gem>(grid, x, y);
        gridObject.SetValue(gem);
        grid.SetValue(x, y, gridObject);
    }
}
