using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TileType
{
    Empty         = -1,
    Start         =  1,
    MiddleType2   =  2,
    MiddleType3   =  3,
    End           =  4
}

public class Puzzle : MonoBehaviour
{
    private int maxSize = 3;
    private int nodeNum = 3;
    public TileSO tileSprites;

    private RectTransform _tileContainer;
    private GraphicRaycaster _raycaster;
    private Canvas _canvas;
    private Camera _uiCamera;
    private Image[,] _cellIcons;
    private Image[,] _cellBackgrounds;
    private int[,] _board;
    private Vector2Int _start;
    private Vector2Int _end;
    private bool _isSolved;

    private float CellSize => tileSprites != null ? tileSprites.cellSize : 100f;
    private float CellSpacing => tileSprites != null ? tileSprites.cellSpacing : 8f;
    private Color ContainerColor => tileSprites != null ? tileSprites.containerColor : new Color(0f, 0f, 0f, 0f);
    private Color TileColor => tileSprites != null ? tileSprites.tileColor : new Color(0.15f, 0.15f, 0.15f, 0.9f);
    private Color ActiveColor => tileSprites != null ? tileSprites.activeColor : new Color(1f, 0.78f, 0.2f, 1f);
    private Color InactiveIconColor => tileSprites != null ? tileSprites.inactiveIconColor : Color.white;
    private Color ActiveIconColor => tileSprites != null ? tileSprites.activeIconColor : Color.white;
    private float TotalSize => maxSize * CellSize;

    void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
        _raycaster = GetComponentInParent<GraphicRaycaster>();
        _uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
        _tileContainer = CreateTileContainer("TileContainer");
    }

    RectTransform CreateTileContainer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = ContainerColor;

        // new GameObject 自带 RectTransform，不能 AddComponent，要 GetComponent
        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = Vector3.zero;
        rt.localScale    = Vector3.one;

        // 轴心在中心
        rt.anchorMin     = new Vector2(0.5f, 0.5f);
        rt.anchorMax     = new Vector2(0.5f, 0.5f);
        rt.pivot         = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // 固定尺寸 = maxSize * cellSize
        rt.sizeDelta = new Vector2(TotalSize, TotalSize);

        return rt;
    }

    void Generate()
    {
        if (_tileContainer == null)
            return;

        ClearCells();
        _board = new int[maxSize, maxSize];
        _isSolved = false;

        var line = new RandomLine();
        bool success = line.Generate(ref _board, nodeNum);
        if (!success)
        {
            return;
        }

        _start = line.Start;
        _end = line.End;
        _cellIcons = new Image[maxSize, maxSize];
        _cellBackgrounds = new Image[maxSize, maxSize];

        for (int x = 0; x < maxSize; x++)
        {
            for (int y = 0; y < maxSize; y++)
            {
                CreateCell(x, y, (TileType)_board[x, y]);
            }
        }

        RefreshVisuals(null);
    }

    Vector2 GridToLocal(int x, int y)
    {
        float half = (maxSize - 1) * 0.5f;
        return new Vector2(
            (x - half) * CellSize,
            (half - y) * CellSize
        );
    }

    void ClearCells()
    {
        for (int i = _tileContainer.childCount - 1; i >= 0; i--)
            Destroy(_tileContainer.GetChild(i).gameObject);
    }

    public void Regenerate() => Generate();

    void CreateCell(int x, int y, TileType type)
    {
        var go = new GameObject($"Cell_{x}_{y}");
        go.transform.SetParent(_tileContainer, false);

        var bg = go.AddComponent<Image>();
        bg.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = GridToLocal(x, y);
        rt.sizeDelta = new Vector2(CellSize - CellSpacing, CellSize - CellSpacing);

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(go.transform, false);

        var icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.sprite = GetCellSprite(type);

        var iconRt = icon.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        _cellBackgrounds[x, y] = bg;
        _cellIcons[x, y] = icon;
    }

    Sprite GetCellSprite(TileType type)
    {
        if (tileSprites == null)
            return null;

        return type switch
        {
            TileType.Start => tileSprites.start,
            TileType.End => tileSprites.end,
            TileType.MiddleType2 => tileSprites.middleType2,
            TileType.MiddleType3 => tileSprites.middleType3,
            _ => tileSprites.normal
        };
    }

    public bool TryGetCellFromScreen(Vector2 screenPosition, out Vector2Int cell)
    {
        cell = default;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_tileContainer, screenPosition, _uiCamera, out var localPoint))
            return false;

        float half = TotalSize * 0.5f;
        float x = localPoint.x + half;
        float y = half - localPoint.y;
        if (x < 0f || y < 0f)
            return false;

        int gridX = Mathf.FloorToInt(x / CellSize);
        int gridY = Mathf.FloorToInt(y / CellSize);
        if (gridX < 0 || gridX >= maxSize || gridY < 0 || gridY >= maxSize)
            return false;

        cell = new Vector2Int(gridX, gridY);
        return true;
    }

    public bool TryGetCellScreenPosition(Vector2Int cell, out Vector2 screenPosition)
    {
        screenPosition = default;
        if (_tileContainer == null)
            return false;

        if (cell.x < 0 || cell.x >= maxSize || cell.y < 0 || cell.y >= maxSize)
            return false;

        var local = GridToLocal(cell.x, cell.y);
        var world = _tileContainer.TransformPoint(local);
        screenPosition = RectTransformUtility.WorldToScreenPoint(_uiCamera, world);
        return true;
    }

    public bool IsPointerBlocked(Vector2 screenPosition)
    {
        if (_raycaster == null || EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        var results = new List<RaycastResult>();
        _raycaster.Raycast(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            var hit = results[i].gameObject;
            if (hit.transform == _tileContainer || hit.transform.IsChildOf(_tileContainer))
                return false;
        }

        return results.Count > 0;
    }

    public int Size => maxSize;
    public bool IsGenerated => _board != null;

    public Vector2Int StartCell => _start;

    public Vector2Int EndCell => _end;

    public TileType GetCellType(Vector2Int cell)
    {
        if (_board == null)
            return TileType.Empty;

        if (cell.x < 0 || cell.x >= maxSize || cell.y < 0 || cell.y >= maxSize)
            return TileType.Empty;

        return (TileType)_board[cell.x, cell.y];
    }

    public bool IsConstrainedMiddle(Vector2Int cell, out TileType type)
    {
        type = GetCellType(cell);
        return type == TileType.MiddleType2 || type == TileType.MiddleType3;
    }

    static bool IsHorizontal(Vector2Int dir) => dir.x != 0;
    static bool IsVertical(Vector2Int dir) => dir.y != 0;

    static bool IsValidConstrainedEntry(TileType type, Vector2Int entryDir) => type switch
    {
        // 2: 竖进横出
        TileType.MiddleType2 => IsVertical(entryDir),
        // 3: 横进竖出
        TileType.MiddleType3 => IsHorizontal(entryDir),
        _ => true
    };

    static bool IsValidConstrainedTurn(TileType type, Vector2Int entryDir, Vector2Int exitDir) => type switch
    {
        TileType.MiddleType2 => IsVertical(entryDir) && IsHorizontal(exitDir),
        TileType.MiddleType3 => IsHorizontal(entryDir) && IsVertical(exitDir),
        _ => true
    };

    public void RefreshVisuals(IReadOnlyCollection<Vector2Int> activePath)
    {
        if (_cellIcons == null || _cellBackgrounds == null)
            return;

        var activeSet = activePath != null ? new HashSet<Vector2Int>(activePath) : new HashSet<Vector2Int>();
        for (int x = 0; x < maxSize; x++)
        {
            for (int y = 0; y < maxSize; y++)
            {
                var pos = new Vector2Int(x, y);
                bool isActive = activeSet.Contains(pos);
                _cellBackgrounds[x, y].color = isActive ? ActiveColor : TileColor;
                _cellIcons[x, y].color = isActive ? ActiveIconColor : InactiveIconColor;
            }
        }
    }

    public void Initialize(int newMaxSize, int newNodeNum)
    {
        maxSize = Mathf.Clamp(newMaxSize, 3, 4);
        nodeNum = Mathf.Clamp(newNodeNum, 2, 4);

        if (_tileContainer != null)
        {
            _tileContainer.sizeDelta = new Vector2(TotalSize, TotalSize);
            var containerImage = _tileContainer.GetComponent<Image>();
            if (containerImage != null)
                containerImage.color = ContainerColor;
        }

        Generate();
        SetPuzzleVisible(true);
    }

    public void SetPuzzleVisible(bool visible)
    {
        if (_tileContainer != null)
            _tileContainer.gameObject.SetActive(visible);
    }

    public void SetSolved(bool solved)
    {
        _isSolved = solved;
    }

    public bool IsSolved => _isSolved;

}












