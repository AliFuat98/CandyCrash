using System;
using TMPro;
using UnityEngine;
public class GridSystem2D<T> {
    public event Action<int, int, T> OnValueChangedEvent;

    int width;
    int height;
    float cellSize;
    Vector3 origin;
    T[,] gridArray;
    CoordinateConverter coordinateConverter;

    public static GridSystem2D<T> VerticalGrid(int width, int height, float cellSize, Vector3 origin, bool debug = false) {
        return new GridSystem2D<T>(width, height, cellSize, origin, new VerticalConverter(), debug);
    }

    public GridSystem2D(int width, int height, float cellSize, Vector3 origin, CoordinateConverter coordinateConverter, bool debug) {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.origin = origin;
        this.coordinateConverter = coordinateConverter ?? new VerticalConverter();

        gridArray = new T[width, height];

        if (debug) {
            DrawDebugLines();
        }
    }

    public void SetValue(Vector3 worldPosition, T value) {
        Vector2Int pos = coordinateConverter.WorldToGrid(worldPosition, cellSize, origin);
        SetValue(pos.x, pos.y, value);
    }

    public void SetValue(int x, int y, T value) {
        if (IsValid(x, y)) {
            gridArray[x, y] = value;
            OnValueChangedEvent?.Invoke(x, y, value);
        }
    }

    public T GetValue(Vector3 worldPosition) {
        Vector2Int pos = GetXY(worldPosition);
        return GetValue(pos.x, pos.y);
    }

    public T GetValue(int x, int y) {
        return IsValid(x, y) ? gridArray[x, y] : default;
    }

    bool IsValid(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
    public Vector2Int GetXY(Vector3 worldPosition) => coordinateConverter.WorldToGrid(worldPosition, cellSize, origin);
    public Vector3 GetWorldPositionCenter(int x, int y) => coordinateConverter.GridToWorldCenter(x, y, cellSize, origin);
    Vector3 GetWorldPosition(int x, int y) => coordinateConverter.GridToWorld(x, y, cellSize, origin);

    void DrawDebugLines() {
        const float duration = 100f;
        var parent = new GameObject("Debugging");

        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                CreateWorldText(parent, text: $"{x},{y}", GetWorldPositionCenter(x, y), dir: coordinateConverter.Forward);
                Debug.DrawLine(start: GetWorldPosition(x, y), end: GetWorldPosition(x, y + 1), color: Color.white, duration: duration);
                Debug.DrawLine(start: GetWorldPosition(x, y), end: GetWorldPosition(x + 1, y), color: Color.white, duration: duration);
            }
        }

        Debug.DrawLine(start: GetWorldPosition(0, height), end: GetWorldPosition(width, height), color: Color.white, duration: duration);
        Debug.DrawLine(start: GetWorldPosition(width, 0), end: GetWorldPosition(width, height), color: Color.white, duration: duration);
    }

    TextMeshPro CreateWorldText(GameObject parent, string text, Vector3 position, Vector3 dir,
        int fontSize = 2, Color color = default, TextAlignmentOptions textAnchor = TextAlignmentOptions.Center, int sortingOrder = 0) {

        GameObject gameObject = new($"DebugText_{text}", typeof(TextMeshPro));
        gameObject.transform.SetParent(parent.transform);
        gameObject.transform.position = position;
        gameObject.transform.forward = dir;

        TextMeshPro textMeshPro = gameObject.GetComponent<TextMeshPro>();
        textMeshPro.text = text;
        textMeshPro.fontSize = fontSize;
        textMeshPro.color = color;
        textMeshPro.alignment = textAnchor;
        textMeshPro.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;

        return textMeshPro;
    }

    public abstract class CoordinateConverter {
        public abstract Vector3 GridToWorld(int x, int y, float cellSize, Vector3 origin);
        public abstract Vector3 GridToWorldCenter(int x, int y, float cellSize, Vector3 origin);
        public abstract Vector2Int WorldToGrid(Vector3 worldPosition, float cellSize, Vector3 origin);
        public abstract Vector3 Forward { get; }
    }

    public class VerticalConverter : CoordinateConverter {
        public override Vector3 GridToWorld(int x, int y, float cellSize, Vector3 origin) {
            return new Vector3(x, y, z: 0) * cellSize + origin;
        }
        public override Vector3 GridToWorldCenter(int x, int y, float cellSize, Vector3 origin) {
            return new Vector3(x: (x * cellSize) + (0.5f * cellSize), y: (y * cellSize) + (0.5f * cellSize), z: 0) + origin;
        }
        public override Vector2Int WorldToGrid(Vector3 worldPosition, float cellSize, Vector3 origin) {
            Vector3 gridPosition = (worldPosition - origin) / cellSize;
            int x = Mathf.FloorToInt(gridPosition.x);
            int y = Mathf.FloorToInt(gridPosition.y);
            return new Vector2Int(x, y);
        }
        public override Vector3 Forward => Vector3.forward;
    }
}
