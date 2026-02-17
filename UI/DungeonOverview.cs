namespace Pathhack.UI;

readonly record struct BranchKey(sbyte A, sbyte B = -1, sbyte C = -1, sbyte D = -1, sbyte E = -1) : IComparable<BranchKey>
{
    public int Length => E >= 0 ? 5 : D >= 0 ? 4 : C >= 0 ? 3 : B >= 0 ? 2 : 1;

    public sbyte this[int i] => i switch { 0 => A, 1 => B, 2 => C, 3 => D, 4 => E, _ => -1 };

    public BranchKey Append(sbyte v) => Length switch
    {
        1 => this with { B = v },
        2 => this with { C = v },
        3 => this with { D = v },
        4 => this with { E = v },
        _ => throw new InvalidOperationException("BranchKey full"),
    };

    public int CompareTo(BranchKey other)
    {
        for (int i = 0; i < 5; i++)
        {
            int cmp = this[i].CompareTo(other[i]);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
}

record struct TreeEntry(BranchKey Key, Branch Branch);

static class DungeonOverview
{
    static List<TreeEntry> BuildTree()
    {
        List<TreeEntry> entries = [];
        HashSet<string> seen = [];
        Walk(g.Branches["dungeon"], new BranchKey(1), entries, seen);
        entries.Sort((a, b) => a.Key.CompareTo(b.Key));
        return entries;
    }

    static void Walk(Branch branch, BranchKey key, List<TreeEntry> entries, HashSet<string> seen)
    {
        if (!branch.Discovered || !seen.Add(branch.Id)) return;
        entries.Add(new(key, branch));

        for (int i = 0; i < branch.ResolvedLevels.Count; i++)
        {
            var resolved = branch.ResolvedLevels[i];
            sbyte depth = (sbyte)(i + 1);

            if (resolved.BranchDown is { } downId && g.Branches.TryGetValue(downId, out var down))
                Walk(down, key.Append(depth), entries, seen);
            if (resolved.BranchUp is { } upId && g.Branches.TryGetValue(upId, out var up))
                Walk(up, key.Append(depth), entries, seen);
        }
    }

    static bool HasSibling(List<TreeEntry> entries, int i)
    {
        var curr = entries[i];
        int len = curr.Key.Length;
        for (int j = i + 1; j < entries.Count; j++)
        {
            var other = entries[j].Key;
            if (other.Length < len) break;
            if (other.Length != len) continue;
            bool match = true;
            for (int k = 0; k < len - 1; k++)
                if (curr.Key[k] != other[k]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    static bool HasContinuation(List<TreeEntry> entries, int i, int depth)
    {
        var curr = entries[i];
        for (int j = i + 1; j < entries.Count; j++)
        {
            var other = entries[j].Key;
            if (other.Length <= depth) continue;
            bool match = true;
            for (int k = 0; k < depth; k++)
                if (curr.Key[k] != other[k]) { match = false; break; }
            if (match) return true;
        }
        return false;
    }

    static void RenderTree(Window win, List<TreeEntry> entries, int cursor)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            int len = e.Key.Length;
            int x = 0;
            int y = i + 1;

            for (int d = 1; d < len - 1; d++)
            {
                if (HasContinuation(entries, i, d))
                    win.At(x, y).Write("│   ", ConsoleColor.DarkGray);
                else
                    win.At(x, y).Write("    ", ConsoleColor.DarkGray);
                x += 4;
            }

            if (i > 0 && len > 1)
            {
                string connector = HasSibling(entries, i) ? "├─ " : "└─ ";
                win.At(x, y).Write(connector, ConsoleColor.DarkGray);
                x += 3;
            }

            bool isHere = e.Branch == u.Level.Branch;
            bool selected = i == cursor;
            ConsoleColor fg = isHere ? ConsoleColor.Blue : ConsoleColor.Gray;
            CellStyle style = selected ? CellStyle.Reverse : CellStyle.None;
            win.At(x, y).Write(e.Branch.Name, fg, style: style);
        }
    }

    static string? FindParentName(Branch branch)
    {
        if (branch.EntranceDepthInParent == null) return null;
        foreach (var (_, b) in g.Branches)
            foreach (var rl in b.ResolvedLevels)
            {
                if (branch.Dir == BranchDir.Down && rl.BranchDown == branch.Id) return b.Name;
                if (branch.Dir == BranchDir.Up && rl.BranchUp == branch.Id) return b.Name;
            }
        return null;
    }

    record struct SliceRow(string Label, bool Dim, bool IsHere = false, string? LeftAnnot = null, string? RightAnnot = null);

    static List<SliceRow> BuildSlice(Branch branch)
    {
        List<SliceRow> rows = [];
        string? parentName = FindParentName(branch);

        int firstVisited = -1, lastVisited = -1;
        for (int d = 1; d <= branch.MaxDepth; d++)
        {
            if (g.Levels.ContainsKey(new LevelId(branch, d)))
            {
                if (firstVisited < 0) firstVisited = d;
                lastVisited = d;
            }
        }

        int entryDepth = branch.EntranceDepthInParent is { } edp
            ? (branch.Dir == BranchDir.Down ? 1 : branch.MaxDepth)
            : 1;

        if (firstVisited < 0)
        {
            rows.Add(new($"{entryDepth}", true, LeftAnnot: parentName != null ? $"{parentName} ←" : null));
            return rows;
        }

        if (firstVisited > 1)
            rows.Add(new($"{firstVisited - 1} ?", true));

        for (int d = firstVisited; d <= lastVisited; d++)
        {
            bool visited = g.Levels.ContainsKey(new LevelId(branch, d));
            bool isEntry = d == entryDepth;
            bool isHere = branch == u.Level.Branch && d == u.Level.Depth;
            var resolved = branch.ResolvedLevels[d - 1];

            LevelId lid = new(branch, d);
            string label = $"{d}";
            if (g.Levels.TryGetValue(lid, out var level) && level.Described is { } desc)
                label = $"{d} {desc}";
            string? rightAnnot = null;

            if (resolved.BranchDown is { } downId && g.Branches.TryGetValue(downId, out var down) && down.Discovered && down.Name != parentName)
                rightAnnot = $"→ {down.Name}";
            else if (resolved.BranchUp is { } upId && g.Branches.TryGetValue(upId, out var up) && up.Discovered && up.Name != parentName)
                rightAnnot = $"→ {up.Name}";

            rows.Add(new(label, !visited, isHere,
                isEntry && parentName != null ? $"{parentName} ←" : null,
                visited ? rightAnnot : null));
        }

        if (lastVisited < branch.MaxDepth)
            rows.Add(new($"{lastVisited + 1} ?", true));

        return rows;
    }

    const int SliceWidth = 20;
    const int SliceLeft = 36;

    static void RenderSlice(Window win, Branch branch, int? scrollOverride = null)
    {
        var rows = BuildSlice(branch);

        win.At(SliceLeft, 0).Write(branch.Name, branch.Color, style: CellStyle.Bold);

        int maxVisible = (Draw.ScreenHeight - 4) / 2;

        int focus = 0;
        if (scrollOverride == null)
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].IsHere) { focus = i; break; }

        int startRow;
        if (scrollOverride != null)
            startRow = Math.Clamp(scrollOverride.Value, 0, Math.Max(0, rows.Count - maxVisible));
        else
            startRow = Math.Clamp(focus - maxVisible / 2, 0, Math.Max(0, rows.Count - maxVisible));
        int endRow = Math.Min(startRow + maxVisible, rows.Count);

        int y = 2;
        for (int i = startRow; i < endRow; i++)
        {
            var row = rows[i];
            ConsoleColor fg = row.IsHere ? ConsoleColor.Blue : row.Dim ? ConsoleColor.DarkGray : ConsoleColor.Gray;

            if (i == startRow)
            {
                if (startRow > 0)
                    win.At(SliceLeft + 1, y).Write("▲", ConsoleColor.DarkGray);
                win.At(SliceLeft + 2, y).Write("╱", fg);
                for (int x = 1; x < SliceWidth - 1; x++)
                    win.At(SliceLeft + 2 + x, y).Write("─", fg);
                win.At(SliceLeft + 2 + SliceWidth - 1, y).Write("╱", fg);
                y++;
            }

            win.At(SliceLeft + 1, y).Write("╱", fg);
            win.At(SliceLeft + 2, y).Write(" ", fg);
            win.At(SliceLeft + 3, y).Write(row.Label.PadRight(SliceWidth - 3), fg);
            win.At(SliceLeft + SliceWidth, y).Write("╱", fg);

            if (row.LeftAnnot is { } la)
                win.At(SliceLeft - la.Length, y).Write(la, ConsoleColor.DarkYellow);
            if (row.RightAnnot is { } ra)
                win.At(SliceLeft + SliceWidth + 2, y).Write(ra, ConsoleColor.DarkCyan);
            y++;

            win.At(SliceLeft, y).Write("╱", fg);
            for (int x = 1; x < SliceWidth - 1; x++)
                win.At(SliceLeft + x, y).Write("─", fg);
            win.At(SliceLeft + SliceWidth - 1, y).Write("╱", fg);

            if (i == endRow - 1 && endRow < rows.Count)
                win.At(SliceLeft - 1, y).Write("▼", ConsoleColor.DarkGray);

            y++;
        }
    }

    static int AutoFocus(Branch branch)
    {
        var rows = BuildSlice(branch);
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].IsHere) return i;
        return 0;
    }

    public static void Show()
    {
        var entries = BuildTree();
        int cursor = 0;
        int? sliceScroll = null;

        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Branch == u.Level.Branch) { cursor = i; break; }

        using var handle = WM.CreateTransient(Draw.ScreenWidth, Draw.ScreenHeight, z: 5, opaque: true);
        var win = handle.Window;

        while (true)
        {
            win.Clear();

            RenderTree(win, entries, cursor);
            RenderSlice(win, entries[cursor].Branch, sliceScroll);
            win.At(0, 0).Write("(j/k) navigate  (J/K) scroll slice  (esc) close", ConsoleColor.DarkGray);
            Draw.Blit();

            var key = Input.NextKey();
            if (key.Key == ConsoleKey.Escape) break;
            bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift) || char.IsUpper(key.KeyChar);
            var dir = Input.GetDirection(key.Key);
            var branch = entries[cursor].Branch;
            int maxVisible = (Draw.ScreenHeight - 4) / 2;
            int sliceMax = Math.Max(0, BuildSlice(branch).Count - maxVisible);
            if (shift && dir == Pos.S) { sliceScroll = Math.Min((sliceScroll ?? AutoFocus(branch)) + 1, sliceMax); Log.Write($"slice scroll J: {sliceScroll}"); }
            else if (shift && dir == Pos.N) { sliceScroll = Math.Max(0, (sliceScroll ?? AutoFocus(branch)) - 1); Log.Write($"slice scroll K: {sliceScroll}"); }
            else if (dir == Pos.S) { cursor = Math.Min(cursor + 1, entries.Count - 1); sliceScroll = null; }
            else if (dir == Pos.N) { cursor = Math.Max(cursor - 1, 0); sliceScroll = null; }
        }
    }
}
