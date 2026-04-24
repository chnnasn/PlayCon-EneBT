using System.Collections.Generic;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    private Puzzle puzzle;

    private readonly HashSet<Vector2Int> _requiredPoints = new();
    private readonly List<Vector2Int> _drawnPath = new();
    private readonly Dictionary<Vector2Int, Vector2Int> _entryDirForConstrained = new();

    private bool _isDrawing;

    [Header("Puzzle generation params")]
    [SerializeField][Range(3, 4)] private int maxSize = 3;
    [SerializeField][Range(2, 4)] private int nodeNum = 3;

    [Header("Combat one-stroke time limit")]
    [SerializeField][Range(3, 10)] private float timeLimitSeconds = 10f;
    
    
    void OnDisable()
    {
            if (OrderlyScene.TryGetExistingInstance(out var orderly))
                orderly.OneStrokeTimedOut -= OnOneStrokeTimedOut;
    }

    void Start()
    {
        if (puzzle == null)
            puzzle = GetComponent<Puzzle>();
        
        _isDrawing = false;
    }
    
    public bool IsPuzzleSessionActive => _requiredPoints.Count > 0;

    
    public void SetPuzzleVisible(bool visible)
    {
        puzzle?.SetPuzzleVisible(visible);
    }

    public void EnsurePuzzleSession()
    {
        if (!IsPuzzleSessionActive)
            BeginPuzzleSession();
    }

    public void BeginDraw(Vector2 pointerPosition)
    {
        if (puzzle == null || puzzle.IsSolved || !IsPuzzleSessionActive)
            return;
        
        if (puzzle.TryGetCellScreenPosition(puzzle.StartCell, out var startScreenPos))
        {
            PlayerInputSystem.MainInstance.TryWarpPointerPosition(startScreenPos);
            pointerPosition = startScreenPos;
        }

        _isDrawing = true;
        _drawnPath.Clear();
        _entryDirForConstrained.Clear();
        _drawnPath.Add(puzzle.StartCell);
        puzzle.RefreshVisuals(_drawnPath);

        if (timeLimitSeconds > 0f)
        {
            var orderly = OrderlyScene.MainInstance;
            orderly.OneStrokeTimedOut += OnOneStrokeTimedOut;
            orderly.BeginOneStrokeWithTimeLimit(timeLimitSeconds);
        }

        if (puzzle.TryGetCellFromScreen(pointerPosition, out var hoverCell))
            TryExtendOrBacktrack(hoverCell);
    }

    public void ContinueDraw(Vector2 pointerPosition)
    {
        if (!_isDrawing || !IsPuzzleSessionActive || puzzle == null || puzzle.IsSolved)
            return;

        if (puzzle.TryGetCellFromScreen(pointerPosition, out var hoverCell))
            TryExtendOrBacktrack(hoverCell);
    }

    public void EndDraw()
    {
        if (!_isDrawing || !IsPuzzleSessionActive || puzzle == null)
            return;

        OrderlyScene.MainInstance.OneStrokeTimedOut -= OnOneStrokeTimedOut;

        _isDrawing = false;
        if (IsSolvedPath())
        {
            puzzle.SetSolved(true);
            OrderlyScene.MainInstance.CancelOneStroke();
            Debug.Log("战斗一笔画成功");

            EndPuzzleSession();
            return;
        }
        
        OrderlyScene.MainInstance.CancelOneStroke();
        Debug.Log("战斗一笔画中断：重置倒计时");


        _drawnPath.Clear();
        _entryDirForConstrained.Clear();
    }


    private void OnOneStrokeTimedOut()
    {
        Debug.Log("战斗一笔画失败：超时");
        SetPuzzleVisible(false);
        
        EndPuzzleSession();
    }

    public void BeginPuzzleSession()
    {
        _isDrawing = false;
        _requiredPoints.Clear();
        _drawnPath.Clear();
        _entryDirForConstrained.Clear();
        
        if (OrderlyScene.TryGetExistingInstance(out var orderly))
        {
            orderly.OneStrokeTimedOut -= OnOneStrokeTimedOut;
            orderly.CancelOneStroke();
        }

        if (puzzle != null)
        {
            puzzle.SetSolved(false);
            puzzle.Initialize(maxSize, nodeNum);
            BuildRequiredPoints();
            puzzle.RefreshVisuals(_drawnPath);
        }

        Debug.Log("召唤战斗一笔画谜题");
    }

    private void EndPuzzleSession()
    {
        if (OrderlyScene.TryGetExistingInstance(out var orderly))
        {
            orderly.OneStrokeTimedOut -= OnOneStrokeTimedOut;
            orderly.CancelOneStroke();
        }

        _isDrawing = false;
        _requiredPoints.Clear();
        _drawnPath.Clear();
        _entryDirForConstrained.Clear();
        puzzle?.RefreshVisuals(_drawnPath);
    }

    void BuildRequiredPoints()
    {
        if (puzzle == null || !puzzle.IsGenerated)
            return;

        _requiredPoints.Clear();
        _drawnPath.Clear();
        _entryDirForConstrained.Clear();
        _isDrawing = false;

        _requiredPoints.Add(puzzle.StartCell);
        _requiredPoints.Add(puzzle.EndCell);

        for (int x = 0; x < puzzle.Size; x++)
        {
            for (int y = 0; y < puzzle.Size; y++)
            {
                var cell = new Vector2Int(x, y);
                var type = puzzle.GetCellType(cell);
                if (type == TileType.MiddleType2 || type == TileType.MiddleType3)
                    _requiredPoints.Add(cell);
            }
        }
    }

    void TryExtendOrBacktrack(Vector2Int cell)
    {
        if (_drawnPath.Count == 0)
            return;

        var current = _drawnPath[_drawnPath.Count - 1];
        if (cell == current)
            return;

        int visitedIndex = _drawnPath.IndexOf(cell);
        if (visitedIndex >= 0)
        {
            // 鼠标回到已走路径上的任意节点：直接回退到该节点。
            for (int i = _drawnPath.Count - 1; i > visitedIndex; i--)
            {
                var removed = _drawnPath[i];
                _drawnPath.RemoveAt(i);
                _entryDirForConstrained.Remove(removed);
            }
            puzzle.RefreshVisuals(_drawnPath);
            return;
        }

        if (!IsAdjacent(current, cell))
            return;

        var moveDir = cell - current;

        if (puzzle.IsConstrainedMiddle(current, out var currentType))
        {
            if (!TryGetEntryDir(current, out var entryDir))
                return;
            if (!IsValidConstrainedTurn(currentType, entryDir, moveDir))
                return;
        }

        if (puzzle.IsConstrainedMiddle(cell, out var nextType))
        {
            if (!IsValidConstrainedEntry(nextType, moveDir))
                return;
            _entryDirForConstrained[cell] = moveDir;
        }

        _drawnPath.Add(cell);
        puzzle.RefreshVisuals(_drawnPath);
    }

    bool TryGetEntryDir(Vector2Int cell, out Vector2Int entryDir)
    {
        if (_entryDirForConstrained.TryGetValue(cell, out entryDir))
            return true;

        int idx = _drawnPath.LastIndexOf(cell);
        if (idx > 0)
        {
            entryDir = _drawnPath[idx] - _drawnPath[idx - 1];
            return true;
        }

        entryDir = default;
        return false;
    }

    bool IsSolvedPath()
    {
        if (_drawnPath.Count == 0)
            return false;
        if (_drawnPath[0] != puzzle.StartCell)
            return false;
        if (_drawnPath[_drawnPath.Count - 1] != puzzle.EndCell)
            return false;

        var visitedRequired = new HashSet<Vector2Int>();
        for (int i = 0; i < _drawnPath.Count; i++)
        {
            var p = _drawnPath[i];
            if (_requiredPoints.Contains(p))
                visitedRequired.Add(p);
        }

        return visitedRequired.Count == _requiredPoints.Count;
    }

    static bool IsAdjacent(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

    static bool IsHorizontal(Vector2Int dir) => dir.x != 0;
    static bool IsVertical(Vector2Int dir) => dir.y != 0;

    static bool IsValidConstrainedEntry(TileType type, Vector2Int entryDir) => type switch
    {
        TileType.MiddleType2 => IsVertical(entryDir),
        TileType.MiddleType3 => IsHorizontal(entryDir),
        _ => true
    };

    static bool IsValidConstrainedTurn(TileType type, Vector2Int entryDir, Vector2Int exitDir) => type switch
    {
        TileType.MiddleType2 => IsVertical(entryDir) && IsHorizontal(exitDir),
        TileType.MiddleType3 => IsHorizontal(entryDir) && IsVertical(exitDir),
        _ => true
    };
}
