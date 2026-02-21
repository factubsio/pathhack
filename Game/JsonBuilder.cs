namespace Pathhack.Game;

using System.Runtime.CompilerServices;
using System.Text;

[InterpolatedStringHandler]
public ref struct JsonBuilder
{
    readonly StringBuilder _sb = new();
    int _count;

    public JsonBuilder(int literalLength, int formattedCount)
    {
        _sb.Append('{');
    }

    public void AppendLiteral(string value) { } // ignored — keys come from format specifiers

    // --- Primitives ---
    public void AppendFormatted(int value, string? format = null) => Append(format, value.ToString());
    public void AppendFormatted(double value, string? format = null) => Append(format, value.ToString());
    public void AppendFormatted(bool value, string? format = null) => Append(format, value ? "true" : "false");
    public void AppendFormatted(string? value, string? format = null) => Append(format, $"\"{Escape(value)}\"");
    public void AppendFormatted(IEnumerable<string> values, string? format = null) =>
        Append(format, $"[{string.Join(",", values.Select(v => $"\"{Escape(v)}\""))}]");
    public void AppendFormatted(string[] values, string? format = null) =>
        Append(format, $"[{string.Join(",", values.Select(v => $"\"{Escape(v)}\""))}]");

    // --- Domain types ---
    public void AppendFormatted(Modifiers mods, string? format = null)
    {
        StringBuilder arr = new();
        AppendMods(arr, mods);
        Append(format, arr.ToString());
    }

    public void AppendFormatted(List<DamageRoll> rolls, string? format = null)
    {
        StringBuilder arr = new("[");
        int i = 0;
        foreach (var r in rolls)
        {
            if (r.Negated) continue;
            if (i++ > 0) arr.Append(',');
            arr.Append($"{{\"formula\":\"{Escape(r.Formula.ToString())}\",\"type\":\"{Escape(r.Type.SubCat)}\"");
            arr.Append($",\"rolled\":{r.Rolled},\"total\":{r.Total},\"dr\":{r.DR},\"prot\":{r.ProtectionUsed}");
            if (r.ExtraDice > 0) arr.Append($",\"extra_dice\":{r.ExtraDice}");
            if (r.Halved) arr.Append(",\"halved\":true");
            if (r.Doubled) arr.Append(",\"doubled\":true");
            if (r.Tags.Count > 0) arr.Append($",\"tags\":\"{Escape(string.Join(",", r.Tags))}\"");
            if (r.Modifiers.Stackable.Count > 0 || r.Modifiers.Unstackable.Count > 0) { arr.Append(",\"mods\":"); AppendMods(arr, r.Modifiers); }
            arr.Append('}');
        }
        arr.Append(']');
        Append(format, arr.ToString());
    }

    static void AppendMods(StringBuilder sb, Modifiers mods)
    {
        sb.Append('[');
        int i = 0;
        foreach (var m in mods.Stackable.Concat(mods.Unstackable.Values))
        {
            if (i++ > 0) sb.Append(',');
            sb.Append($"{{\"cat\":\"{Escape(m.Category.ToString())}\",\"value\":{m.Value},\"why\":\"{Escape(m.Why)}\"}}");
        }
        sb.Append(']');
    }

    public void AppendFormatted(DiceFormula value, string? format = null) => Append(format, $"\"{Escape(value.ToString())}\"");

    // --- Fallback ---
    public void AppendFormatted<T>(T value, string? format = null) => Append(format, $"\"{Escape(value?.ToString())}\"");

    void Append(string? key, string jsonValue)
    {
        if (key == null) return;
        if (_count++ > 0) _sb.Append(',');
        _sb.Append('"').Append(key).Append("\":").Append(jsonValue);
    }

    static string Escape(string? s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

    public string ToJson()
    {
        _sb.Append('}');
        return _sb.ToString();
    }

    public override string ToString()
    {
        // human-readable: key=value key2=value2
        ReadOnlySpan<char> json = _sb.ToString().AsSpan(1); // skip leading {
        StringBuilder sb = new();
        foreach (var pair in json.ToString().Split(','))
        {
            if (sb.Length > 0) sb.Append(' ');
            var kv = pair.Split(':', 2);
            if (kv.Length == 2)
                sb.Append(kv[0].Trim('"')).Append('=').Append(kv[1]);
        }
        return sb.ToString();
    }
}
