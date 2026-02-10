using System.Runtime.InteropServices;

namespace Pathhack.Core;

public static class RngExtensions
{
    public static int Rn2(this Random r, int n) => n <= 0 ? 0 : r.Next(n);
    public static int Rn1(this Random r, int x, int y) => y + r.Rn2(x);
    public static int RnRange(this Random r, int min, int max) => min + r.Rn2(max - min + 1);
    public static int Rne(this Random r, int x)
    {
        int result = 1;
        while (result < 5 && r.Rn2(x) == 0) result++;
        return result;
    }
    public static Pos RandomInterior(this Random r, Rect rect) => new(r.RnRange(rect.X + 1, rect.X + rect.W - 2), r.RnRange(rect.Y + 1, rect.Y + rect.H - 2));
    public static Pos RandomBorder(this Random r, Rect rect)
    {
        int perim = 2 * (rect.W + rect.H) - 4;
        int i = r.Rn2(perim);
        if (i < rect.W) return new(rect.X + i, rect.Y);
        i -= rect.W;
        if (i < rect.W) return new(rect.X + i, rect.Y + rect.H - 1);
        i -= rect.W;
        if (i < rect.H - 2) return new(rect.X, rect.Y + 1 + i);
        i -= rect.H - 2;
        return new(rect.X + rect.W - 1, rect.Y + 1 + i);
    }
    public static List<Pos> RandomInteriorN(this Random r, Rect rect, int n)
    {
        HashSet<Pos> picked = [];
        while (picked.Count < n)
            picked.Add(r.RandomInterior(rect));
        return [.. picked];
    }

    public static T Pick<T>(this List<T> list) => list[g.Rn2(list.Count)];
    public static T Pick<T>(this T[] array) => array[g.Rn2(array.Length)];

    public static T[] Shuffled<T>(this T[] array)
    {
        T[] copy = [..array];
        g.Shuffle(copy.AsSpan());
        return copy;
    }

    public static List<T> Shuffled<T>(this List<T> list)
    {
        List<T> copy = [..list];
        g.Shuffle(CollectionsMarshal.AsSpan(copy));
        return copy;
    }
    
    public static T PickWeighted<T>(this IEnumerable<T> items, Func<T, int> weight)
    {
        var list = items.ToList();
        int total = list.Sum(weight);
        int roll = g.Rn2(total);
        foreach (var item in list)
        {
            roll -= weight(item);
            if (roll < 0) return item;
        }
        return list[^1];
    }
}

public record struct Dice(int D, int F, int Flat = 0)
{
    public static Dice d(int f) => new(1, f);
    public static Dice d(int d, int f) => new(d, f);
    public static Dice d(int d, int f, int flat) => new(d, f, flat);

    public readonly int Roll()
    {
        int sum = Flat;
        for (int i = 0; i < D; i++)
            sum += g.Rn1(F, 1);
        return sum;
    }

    public static Dice operator+(Dice d, int bonus) => new(d.D, d.F, d.Flat + bonus);

    internal readonly Dice WithExtra(int extraDice) => new(D * (1 + extraDice), F, Flat);
}

public record struct DiceFormula(Dice[] Dice)
{
    public static implicit operator DiceFormula(Dice d) => new([d]);
    public static implicit operator DiceFormula(int flat) => new([new(0, 0, flat)]);

    public readonly int Roll(int extraDice = 0)
    {
        int sum = 0;
        foreach (var die in Dice)
            sum += d(die.D + extraDice, die.F, die.Flat).Roll();
        return sum;
    }

    public readonly double Average()
    {
        double sum = 0;
        foreach (var d in Dice)
            sum += d.D * (d.F + 1) / 2.0 + d.Flat;
        return sum;
    }

    public override readonly string ToString()
    {
        if (Dice.Length == 0) return "0";
        return string.Join("+", Dice.Select(d =>
        {
            string s = d.D == 1 ? $"d{d.F}" : $"{d.D}d{d.F}";
            if (d.Flat > 0) s += $"+{d.Flat}";
            else if (d.Flat < 0) s += $"{d.Flat}";
            return s;
        }));
    }

    static readonly int[] DieSteps = [4, 6, 8, 10, 12];

    public readonly DiceFormula StepUp()
    {
        if (Dice.Length != 1 || Dice[0].D != 1) return this;
        var die = Dice[0];
        int idx = Array.IndexOf(DieSteps, die.F);
        if (idx < 0) return this;
        if (idx < DieSteps.Length - 1)
            return new([new(1, DieSteps[idx + 1], die.Flat)]);
        // d12 -> 2d6
        return new([new(2, 6, die.Flat)]);
    }

    internal readonly DiceFormula WithExtra(int extraDice)
    {
        if (Dice.Length == 1)
            return Dice[0].WithExtra(extraDice);
        else
            return new([Dice[0].WithExtra(extraDice), ..Dice[1..]]);
        
    }
}