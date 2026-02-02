namespace Pathhack.Test;

using Pathhack.Game;
using Pathhack.UI;
using static Pathhack.Game.GameState;

public static class TestRunner
{
    static readonly List<(string Name, Action Test)> _tests = [];
    static readonly List<string> _failures = [];

    public static void Register(string name, Action test) => _tests.Add((name, test));

    public static void Run()
    {
        Draw.Enabled = false;
        foreach (var (name, test) in _tests)
        {
            _failures.Clear();
            try
            {
                test();
                FeedKeys(ConsoleKey.OemPeriod);
                g.DoRound();
            }
            catch (Exception e)
            {
                _failures.Add($"Exception: {e.Message}");
            }

            if (_failures.Count > 0)
            {
                Console.WriteLine($"FAIL: {name}");
                foreach (var f in _failures)
                    Console.WriteLine($"  {f}");
            }
            else
            {
                Console.WriteLine($"PASS: {name}");
            }
        }
        Draw.Enabled = true;
    }

    public static void Assert(bool condition, string msg)
    {
        if (!condition) _failures.Add(msg);
    }

    public static void AssertEq<T>(T expected, T actual, string msg)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            _failures.Add($"{msg}: expected {expected}, got {actual}");
    }

    public static void FeedKeys(params ConsoleKey[] keys)
    {
        Input.InjectedKeys = new Queue<ConsoleKey>(keys);
    }
}
