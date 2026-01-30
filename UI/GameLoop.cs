namespace Pathhack.UI;

public readonly struct HideCursor : IDisposable
{
    public HideCursor() => Console.CursorVisible = false;
    public void Dispose() => Console.CursorVisible = true;
}

public static class GameLoop
{
    public static void Run()
    {
        try
        {
            while (g.Running)
            {
                g.DoRound();
            }
        }
        catch (GameOverException)
        {
        }
    }
}
