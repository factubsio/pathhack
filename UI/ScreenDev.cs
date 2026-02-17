namespace Pathhack.UI;

public static class ScreenDev
{
    public static void Run()
    {
        Console.Clear();
        Console.CursorVisible = false;

        Window win = new(40, 10, x: 5, y: 3, z: 0);
        var content = win.WithBorder(ConsoleColor.Yellow);
        content.Write("Hello from Window!", ConsoleColor.Green);
        content.NewLine(); content.NewLine();
        content.Write("Local coords, no MapRow!", ConsoleColor.Cyan);
        content.NewLine(); content.NewLine();
        content.Write("Press any key...", ConsoleColor.DarkGray);

        WM.Register(win);
        WM.Blit();
        Console.ReadKey(true);

        // Transient window on top — auto-unregisters
        using (var handle = WM.CreateTransient(20, 5, x: 15, y: 6, z: 1))
        {
            var content2 = handle.Window.WithBorder(ConsoleColor.Red);
            content2.Write("Overlapping!", ConsoleColor.Red);
            content2.NewLine();
            content2.Write("Z-order test!", ConsoleColor.Magenta);

            WM.Blit();
            Console.ReadKey(true);
        }

        // Transient disposed — first window reappears
        Compositor.Invalidate();
        WM.Blit();
        Console.ReadKey(true);

        WM.Unregister(win);
        Console.CursorVisible = true;
        Console.Clear();
    }
}
