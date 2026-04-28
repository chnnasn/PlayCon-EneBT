using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RandomLine
{
    private int[,] grid;
    private int targetJumpCount;

    private List<Vector2Int> jumpPoints;
    private List<Vector2Int> fullPath;
    private Vector2Int start, end;

    private static readonly Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    public List<Vector2Int> FullPath  => fullPath;
    public List<Vector2Int> JumpPoints => jumpPoints;
    public Vector2Int Start => start;
    public Vector2Int End   => end;

    public bool Generate(ref int[,] array, int middlePointCount)
    {
        grid = array;
        targetJumpCount = middlePointCount;
        int maxCells = grid.GetLength(0) * grid.GetLength(1);

        if (targetJumpCount + 2 > maxCells)
            targetJumpCount = Mathf.Max(0, maxCells - 2);

        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (TryGenerate())
            {
                array = grid;
                return true;
            }
        }
        
        if (GenerateFallback())
        {
            array = grid;
            return true;
        }
        
        return false;
    }
    
    private bool TryGenerate()
    {
        ClearGrid();
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);
        int maxCells = w * h;
        int needed = targetJumpCount + 2;
        if (needed > maxCells) return false;
        
        int targetPathLen = Random.Range(needed, maxCells + 1);
        var allCells = GetAllCells();
        Shuffle(allCells);

        fullPath = null;
        for (int i = 0; i < allCells.Count; i++)
        {
            var candidatePath = new List<Vector2Int> { allCells[i] };
            var visited = new HashSet<Vector2Int> { allCells[i] };
            if (TryBuildRandomPath(candidatePath, visited, targetPathLen))
            {
                fullPath = candidatePath;
                break;
            }
        }

        if (fullPath == null || fullPath.Count < needed)
            return false;

        // 只从拐点中挑选中间点，确保 2/3 数量严格等于 targetJumpCount
        start = fullPath[0];
        end = fullPath[fullPath.Count - 1];

        var cornerIndices = GetCornerIndices(fullPath);
        if (cornerIndices.Count < targetJumpCount)
            return false;
        Shuffle(cornerIndices);

        jumpPoints = new List<Vector2Int> { start };
        for (int i = 0; i < targetJumpCount; i++)
        {
            int idx = cornerIndices[i];
            jumpPoints.Add(fullPath[idx]);
        }
        jumpPoints = jumpPoints.OrderBy(p => fullPath.IndexOf(p)).ToList();
        jumpPoints.Add(end);
        
        AssignTypes();
        return ConstrainedPointsCount() == targetJumpCount;
    }

    private bool TryBuildRandomPath(List<Vector2Int> path, HashSet<Vector2Int> visited, int targetLen)
    {
        if (path.Count >= targetLen)
            return true;

        var cur = path[path.Count - 1];
        var neighbors = GetShuffledNeighbors(cur);
        for (int i = 0; i < neighbors.Count; i++)
        {
            var next = neighbors[i];
            if (!IsInBounds(next) || visited.Contains(next))
                continue;

            visited.Add(next);
            path.Add(next);
            if (TryBuildRandomPath(path, visited, targetLen))
                return true;
            path.RemoveAt(path.Count - 1);
            visited.Remove(next);
        }
        return false;
    }

    private List<Vector2Int> GetShuffledNeighbors(Vector2Int p)
    {
        var result = new List<Vector2Int>(4)
        {
            p + Vector2Int.up,
            p + Vector2Int.down,
            p + Vector2Int.left,
            p + Vector2Int.right
        };
        Shuffle(result);
        return result;
    }
    
    
    private void AssignTypes()
    {

        foreach (var p in fullPath)
            if (grid[p.x, p.y] == -1)
                grid[p.x, p.y] = 0;

        // 起点 / 终点
        grid[start.x, start.y] = 1;
        grid[end.x,   end.y]   = 4;

        // 中间跳点：根据进出方向决定类型
        for (int i = 1; i < jumpPoints.Count - 1; i++)
        {
            var point = jumpPoints[i];
            int idx = fullPath.IndexOf(point);

            if (idx <= 0 || idx >= fullPath.Count - 1)
            {
                // 若跳点不在 fullPath 中间，化为普通通道点
                grid[point.x, point.y] = 0;
                continue;
            }

            var entry  = point - fullPath[idx - 1];
            var exit   = fullPath[idx + 1] - point;
            bool entryH = entry.x != 0;
            bool exitH  = exit.x  != 0;

            // 2: 竖进横出，3: 横进竖出
            if      (!entryH &&  exitH) grid[point.x, point.y] = 2;
            else if ( entryH && !exitH) grid[point.x, point.y] = 3;
            else
            {
                grid[point.x, point.y] = 0;
            }
        }
    }
    
    private bool GenerateFallback()
    {
        ClearGrid();
        int w = grid.GetLength(0), h = grid.GetLength(1);
        
        fullPath = new List<Vector2Int>(w * h);
        for (int y = 0; y < h; y++)
        {
            if (y % 2 == 0)
            {
                for (int x = 0; x < w; x++)
                    fullPath.Add(new Vector2Int(x, y));
            }
            else
            {
                for (int x = w - 1; x >= 0; x--)
                    fullPath.Add(new Vector2Int(x, y));
            }
        }

        start = fullPath[0];
        end = fullPath[fullPath.Count - 1];
        
        var cornerIndices = GetCornerIndices(fullPath);
        if (cornerIndices.Count < targetJumpCount)
            return false;
        Shuffle(cornerIndices);

        jumpPoints = new List<Vector2Int> { start };
        for (int i = 0; i < targetJumpCount; i++)
            jumpPoints.Add(fullPath[cornerIndices[i]]);
        jumpPoints = jumpPoints.OrderBy(p => fullPath.IndexOf(p)).ToList();
        jumpPoints.Add(end);

        AssignTypes();
        return ConstrainedPointsCount() == targetJumpCount;
    }
    
    private void ClearGrid()
    {
        for (int i = 0; i < grid.GetLength(0); i++)
            for (int j = 0; j < grid.GetLength(1); j++)
                grid[i, j] = -1;
    }

    private List<Vector2Int> GetAllCells()
    {
        var cells = new List<Vector2Int>();
        for (int i = 0; i < grid.GetLength(0); i++)
            for (int j = 0; j < grid.GetLength(1); j++)
                cells.Add(new Vector2Int(i, j));
        return cells;
    }

    private bool IsInBounds(Vector2Int p) =>
        p.x >= 0 && p.x < grid.GetLength(0) &&
        p.y >= 0 && p.y < grid.GetLength(1);

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    private static List<int> GetCornerIndices(List<Vector2Int> path)
    {
        var corners = new List<int>();
        for (int i = 1; i < path.Count - 1; i++)
        {
            var entry = path[i] - path[i - 1];
            var exit = path[i + 1] - path[i];
            bool entryH = entry.x != 0;
            bool exitH = exit.x != 0;
            if (entryH != exitH)
                corners.Add(i);
        }
        return corners;
    }

    private int ConstrainedPointsCount()
    {
        int count = 0;
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                int v = grid[x, y];
                if (v == 2 || v == 3)
                    count++;
            }
        }
        return count;
    }
}
