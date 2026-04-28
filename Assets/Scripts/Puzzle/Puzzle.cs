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

    private RectTransform tileContainer;
    private GraphicRaycaster raycaster;
    private Canvas canvas;
    private Camera uiCamera;
    private Image[,] cellIcons;
    private Image[,] cellBackgrounds;
    private int[,] board;
    private Vector2Int start;
    private Vector2Int end;
    private bool isSolved;

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
        canvas = GetComponentInParent<Canvas>();
        raycaster = GetComponentInParent<GraphicRaycaster>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        tileContainer = CreateTileC("TileContainer");
    }

    RectTransform CreateTileC(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = ContainerColor;
        
        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = Vector3.zero;
        rt.localScale    = Vector3.one;
        
        rt.anchorMin     = new Vector2(0.5f, 0.5f);
        rt.anchorMax     = new Vector2(0.5f, 0.5f);
        rt.pivot         = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        
        rt.sizeDelta = new Vector2(TotalSize, TotalSize);

        return rt;
    }

    void Generate()
    {
        if (tileContainer == null)
            return;

        ClearCells();
        board = new int[maxSize, maxSize];
        isSolved = false;

        var line = new RandomLine();
        bool success = line.Generate(ref board, nodeNum);
        if (!success)
        {
            return;
        }

        start = line.Start;
        end = line.End;
        cellIcons = new Image[maxSize, maxSize];
        cellBackgrounds = new Image[maxSize, maxSize];

        for (int x = 0; x < maxSize; x++)
        {
            for (int y = 0; y < maxSize; y++)
            {
                CreateCell(x, y, (TileType)board[x, y]);
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
        for (int i = tileContainer.childCount - 1; i >= 0; i--)
            Destroy(tileContainer.GetChild(i).gameObject);
    }
    

    void CreateCell(int x, int y, TileType type)
    {
        var go = new GameObject($"Cell_{x}_{y}");
        go.transform.SetParent(tileContainer, false);

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

        cellBackgrounds[x, y] = bg;
        cellIcons[x, y] = icon;
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
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(tileContainer, screenPosition, uiCamera, out var localPoint))
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
        if (tileContainer == null)
            return false;

        if (cell.x < 0 || cell.x >= maxSize || cell.y < 0 || cell.y >= maxSize)
            return false;

        var local = GridToLocal(cell.x, cell.y);
        var world = tileContainer.TransformPoint(local);
        screenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, world);
        return true;
    }
    
    public int Size => maxSize;
    public bool IsGenerated => board != null;

    public Vector2Int StartCell => start;

    public Vector2Int EndCell => end;

    public TileType GetCellType(Vector2Int cell)
    {
        if (board == null)
            return TileType.Empty;

        if (cell.x < 0 || cell.x >= maxSize || cell.y < 0 || cell.y >= maxSize)
            return TileType.Empty;

        return (TileType)board[cell.x, cell.y];
    }

    public bool IsConstrainedMiddle(Vector2Int cell, out TileType type)
    {
        type = GetCellType(cell);
        return type == TileType.MiddleType2 || type == TileType.MiddleType3;
    }
    
    public void RefreshVisuals(IReadOnlyCollection<Vector2Int> activePath)
    {
        if (cellIcons == null || cellBackgrounds == null)
            return;

        var activeSet = activePath != null ? new HashSet<Vector2Int>(activePath) : new HashSet<Vector2Int>();
        for (int x = 0; x < maxSize; x++)
        {
            for (int y = 0; y < maxSize; y++)
            {
                var pos = new Vector2Int(x, y);
                bool isActive = activeSet.Contains(pos);
                cellBackgrounds[x, y].color = isActive ? ActiveColor : TileColor;
                cellIcons[x, y].color = isActive ? ActiveIconColor : InactiveIconColor;
            }
        }
    }

    public void Initialize(int newMaxSize, int newNodeNum)
    {
        maxSize = Mathf.Clamp(newMaxSize, 3, 4);
        nodeNum = Mathf.Clamp(newNodeNum, 2, 4);

        if (tileContainer != null)
        {
            tileContainer.sizeDelta = new Vector2(TotalSize, TotalSize);
            var containerImage = tileContainer.GetComponent<Image>();
            if (containerImage != null)
                containerImage.color = ContainerColor;
        }

        Generate();
        SetPuzzleVisible(true);
    }

    public void SetPuzzleVisible(bool visible)
    {
        if (tileContainer != null)
            tileContainer.gameObject.SetActive(visible);
    }

    public void SetSolved(bool solved)
    {
        isSolved = solved;
    }

    public bool IsSolved => isSolved;

}












