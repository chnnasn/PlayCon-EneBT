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

        //  随机生成一条不重复的连续路径（自避免路径）
        int targetPathLen = Random.Range(needed, maxCells + 1);
        var allCells = GetAllCells();
        Shuffle(allCells);

        fullPath = null;
        for (int i = 0; i < allCells.Count; i++)
        {
            var candidatePath = new List<Vector2Int> { allCells[i] };
            var visited = new HashSet<Vector2Int> { allCells[i] };
            if (TryBuildRandomSelfAvoidingPath(candidatePath, visited, targetPathLen))
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

        // 根据路径真实进出方向写入类型
        AssignTypes();
        return CountConstrainedPoints() == targetJumpCount;
    }

    private bool TryBuildRandomSelfAvoidingPath(List<Vector2Int> path, HashSet<Vector2Int> visited, int targetLen)
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
            if (TryBuildRandomSelfAvoidingPath(path, visited, targetLen))
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

    /// <summary>
    /// 在不与 usedCells 冲突的前提下，从 from 到 to 构造一条折线段。
    /// 先随机选横向或纵向优先，若产生冲突则尝试另一方向。
    /// </summary>
    private List<Vector2Int> BuildSegment(Vector2Int from, Vector2Int to, HashSet<Vector2Int> usedCells)
    {
        // 尝试两种折线方向
        bool hFirst = Random.value > 0.5f;
        var seg = TryBuildLShaped(from, to, usedCells, hFirst);
        if (seg != null) return seg;

        seg = TryBuildLShaped(from, to, usedCells, !hFirst);
        return seg;
    }

    private List<Vector2Int> TryBuildLShaped(Vector2Int from, Vector2Int to,
                                              HashSet<Vector2Int> usedCells, bool hFirst)
    {
        var seg = new List<Vector2Int>();
        var cur = from;
        seg.Add(cur);

        Vector2Int corner = hFirst
            ? new Vector2Int(to.x, from.y)
            : new Vector2Int(from.x, to.y);

        // 走第一段（到拐角）
        if (!WalkTo(cur, corner, seg, usedCells)) return null;
        cur = corner;

        // 走第二段（到终点）
        if (!WalkTo(cur, to, seg, usedCells)) return null;

        return seg;
    }

    /// 从 cur 沿直线走到 target，沿途不能踩 usedCells（起点已在列表里可以跳过）
    private bool WalkTo(Vector2Int cur, Vector2Int target,
                        List<Vector2Int> seg, HashSet<Vector2Int> usedCells)
    {
        int dx = (target.x > cur.x) ? 1 : (target.x < cur.x) ? -1 : 0;
        int dy = (target.y > cur.y) ? 1 : (target.y < cur.y) ? -1 : 0;

        while (cur != target)
        {
            cur = new Vector2Int(cur.x + dx, cur.y + dy);
            // 一笔画要求整条路径不重复格子，因此任何一步都不允许踩到已用格子。
            // （包含折线拐角 corner；之前允许 target 被占用会导致自交/重复，进而无解）
            if (usedCells.Contains(cur)) return false;
            seg.Add(cur);
        }
        return true;
    }
    
    private void AssignTypes()
    {
        // 默认将 fullPath 上所有非跳点格子标为通道（0）
        // 注意：你的业务里 0 可能是"普通通道"，按需修改
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
                // 若跳点不在 fullPath 中间（理论上不该发生），为保证可走，退化为普通通道点
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
                // entry/exit 同方向表示直线通过。
                // 若这里随机标成 2/3（必须拐弯），会直接把解变成不可走。
                // 直线跳点仍然算“跳点”，但不施加转向约束 → 设为普通通道(0)。
                grid[point.x, point.y] = 0;
            }
        }
    }
    
    private bool GenerateFallback()
    {
        ClearGrid();
        int w = grid.GetLength(0), h = grid.GetLength(1);

        // Deterministic non-self-intersecting path (snake), guaranteed one-stroke solvable.
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

        // Keep the same strict rule in fallback: only corners can be middle jump points (2/3).
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
        return CountConstrainedPoints() == targetJumpCount;
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

    private int CountConstrainedPoints()
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
