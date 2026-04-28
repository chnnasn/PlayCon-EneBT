using System;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    private Puzzle puzzle;
    public event Action<bool> OneDrawingChanged;
    public event Action OneStrokeTimedOut;

    private readonly HashSet<Vector2Int> requiredPoints = new();
    private readonly List<Vector2Int> Path = new();
    private readonly Dictionary<Vector2Int, Vector2Int> Constrained = new();

    private bool isDrawing;

    [Header("Puzzle generation params")]
    [SerializeField][Range(3, 4)] private int maxSize = 3;
    [SerializeField][Range(2, 4)] private int nodeNum = 3;

    [Header("Combat one-stroke time limit")]
    [SerializeField][Range(3, 10)] private float TimeSeconds = 10f;

    [Header("Puzzle cooldown")]
    [SerializeField][Min(0f)] private float CooldownSeconds = 1f;
    private float nextAllowedTime;
    
    
    void OnDisable()
    {
            if (OrderlyScene.TryGetExistingInstance(out var orderly))
                orderly.OneStrokeTimedOut -= OnStrokeTimedOut;
    }

    void Start()
    {
        if (puzzle == null)
            puzzle = GetComponent<Puzzle>();
        
        isDrawing = false;
    }
    
    public bool IsPuzzleSession => requiredPoints.Count > 0;

    
    public void SetPuzzleVisible(bool visible)
    {
        puzzle?.SetPuzzleVisible(visible);
    }

    public void EnsurePuzzleSession()
    {
        if (!IsPuzzleSession)
            BeginPuzzle();
    }

    public void CancelPuzzleSession(bool hideUi = true)
    {
        if (hideUi)
        {
            SetPuzzleVisible(false);
        }

        EndPuzzleSession();
    }

    public void BeginDraw(Vector2 pointerPosition)
    {
        if (puzzle == null || puzzle.IsSolved || !IsPuzzleSession)
            return;
        
        if (puzzle.TryGetCellScreenPosition(puzzle.StartCell, out var startScreenPos))
        {
            PlayerInputSystem.MainInstance.TryWarpPointerPosition(startScreenPos);
            pointerPosition = startScreenPos;
        }

        isDrawing = true;
        Path.Clear();
        Constrained.Clear();
        Path.Add(puzzle.StartCell);
        puzzle.RefreshVisuals(Path);
        OneDrawingChanged?.Invoke(true);

        if (TimeSeconds > 0f)
        {
            var orderly = OrderlyScene.MainInstance;
            orderly.OneStrokeTimedOut += OnStrokeTimedOut;
            orderly.BeginOneStrokeWithTimeLimit(TimeSeconds);
        }

        if (puzzle.TryGetCellFromScreen(pointerPosition, out var hoverCell))
            TryExtendOrBacktrack(hoverCell);
    }

    public void ContinueDraw(Vector2 pointerPosition)
    {
        if (!isDrawing || !IsPuzzleSession || puzzle == null || puzzle.IsSolved)
            return;

        if (puzzle.TryGetCellFromScreen(pointerPosition, out var hoverCell))
            TryExtendOrBacktrack(hoverCell);
    }

    public bool EndDraw()
    {
        if (!isDrawing || !IsPuzzleSession || puzzle == null)
            return false;

        OrderlyScene.MainInstance.OneStrokeTimedOut -= OnStrokeTimedOut;

        isDrawing = false;
        OneDrawingChanged?.Invoke(false);
        if (IsSolvedPath())
        {
            puzzle.SetSolved(true);
            OrderlyScene.MainInstance.CancelOneStroke();
            EndPuzzleSession();
            return true;
        }
        
        OrderlyScene.MainInstance.CancelOneStroke();
        Debug.Log("战斗一笔画中断：重置倒计时");


        Path.Clear();
        Constrained.Clear();
        return false;
    }


    private void OnStrokeTimedOut()
    {
        Debug.Log("战斗一笔画失败：超时");
        SetPuzzleVisible(false);
        
        if (isDrawing)
        {
            isDrawing = false;
            OneDrawingChanged?.Invoke(false);
        }

        OneStrokeTimedOut?.Invoke();
        
        EndPuzzleSession();
    }

    public void BeginPuzzle()
    {
        if (Time.time < nextAllowedTime)
        {
            return;
        }

        isDrawing = false;
        requiredPoints.Clear();
        Path.Clear();
        Constrained.Clear();
        
        if (OrderlyScene.TryGetExistingInstance(out var orderly))
        {
            orderly.OneStrokeTimedOut -= OnStrokeTimedOut;
            orderly.CancelOneStroke();
        }

        if (puzzle != null)
        {
            puzzle.SetSolved(false);
            puzzle.Initialize(maxSize, nodeNum);
            BuildRequiredPoints();
            puzzle.RefreshVisuals(Path);
        }

        Debug.Log("召唤战斗一笔画谜题");
    }

    private void EndPuzzleSession()
    {
        nextAllowedTime = Time.time + Mathf.Max(0f, CooldownSeconds);

        if (OrderlyScene.TryGetExistingInstance(out var orderly))
        {
            orderly.OneStrokeTimedOut -= OnStrokeTimedOut;
            orderly.CancelOneStroke();
        }

        isDrawing = false;
        requiredPoints.Clear();
        Path.Clear();
        Constrained.Clear();
        puzzle?.RefreshVisuals(Path);
        OneDrawingChanged?.Invoke(false);
    }

    void BuildRequiredPoints()
    {
        if (puzzle == null || !puzzle.IsGenerated)
            return;

        requiredPoints.Clear();
        Path.Clear();
        Constrained.Clear();
        isDrawing = false;

        requiredPoints.Add(puzzle.StartCell);
        requiredPoints.Add(puzzle.EndCell);

        for (int x = 0; x < puzzle.Size; x++)
        {
            for (int y = 0; y < puzzle.Size; y++)
            {
                var cell = new Vector2Int(x, y);
                var type = puzzle.GetCellType(cell);
                if (type == TileType.MiddleType2 || type == TileType.MiddleType3)
                    requiredPoints.Add(cell);
            }
        }
    }

    void TryExtendOrBacktrack(Vector2Int cell)
    {
        if (Path.Count == 0)
            return;

        var current = Path[Path.Count - 1];
        if (cell == current)
            return;

        int visitedIndex = Path.IndexOf(cell);
        if (visitedIndex >= 0)
        {
            // 鼠标回到已走路径上的任意节点：直接回退到该节点。
            for (int i = Path.Count - 1; i > visitedIndex; i--)
            {
                var removed = Path[i];
                Path.RemoveAt(i);
                Constrained.Remove(removed);
            }
            puzzle.RefreshVisuals(Path);
            return;
        }

        if (!IsAdjacent(current, cell))
            return;

        var moveDir = cell - current;

        if (puzzle.IsConstrainedMiddle(current, out var currentType))
        {
            if (!TryGetEntryDir(current, out var entryDir))
                return;
            if (!IsConstrainedTurn(currentType, entryDir, moveDir))
                return;
        }

        if (puzzle.IsConstrainedMiddle(cell, out var nextType))
        {
            if (!IsConstrainedEntry(nextType, moveDir))
                return;
            Constrained[cell] = moveDir;
        }

        Path.Add(cell);
        puzzle.RefreshVisuals(Path);
    }

    bool TryGetEntryDir(Vector2Int cell, out Vector2Int entryDir)
    {
        if (Constrained.TryGetValue(cell, out entryDir))
            return true;

        int idx = Path.LastIndexOf(cell);
        if (idx > 0)
        {
            entryDir = Path[idx] - Path[idx - 1];
            return true;
        }

        entryDir = default;
        return false;
    }

    bool IsSolvedPath()
    {
        if (Path.Count == 0)
            return false;
        if (Path[0] != puzzle.StartCell)
            return false;
        if (Path[Path.Count - 1] != puzzle.EndCell)
            return false;

        var visitedRequired = new HashSet<Vector2Int>();
        for (int i = 0; i < Path.Count; i++)
        {
            var p = Path[i];
            if (requiredPoints.Contains(p))
                visitedRequired.Add(p);
        }

        return visitedRequired.Count == requiredPoints.Count;
    }

    static bool IsAdjacent(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

    static bool IsHorizontal(Vector2Int dir) => dir.x != 0;
    static bool IsVertical(Vector2Int dir) => dir.y != 0;

    static bool IsConstrainedEntry(TileType type, Vector2Int entryDir) => type switch
    {
        TileType.MiddleType2 => IsVertical(entryDir),
        TileType.MiddleType3 => IsHorizontal(entryDir),
        _ => true
    };

    static bool IsConstrainedTurn(TileType type, Vector2Int entryDir, Vector2Int exitDir) => type switch
    {
        TileType.MiddleType2 => IsVertical(entryDir) && IsHorizontal(exitDir),
        TileType.MiddleType3 => IsHorizontal(entryDir) && IsVertical(exitDir),
        _ => true
    };
}
