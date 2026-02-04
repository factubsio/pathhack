namespace Pathhack.UI;

public static class Fireworks
{
    static readonly ConsoleColor[] Colors = [
        ConsoleColor.Red, ConsoleColor.Yellow, ConsoleColor.Green,
        ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.White
    ];

    record struct Particle(double X, double Y, double Vx, double Vy, ConsoleColor Color, int Life);

    public static void Play()
    {
        Console.CursorVisible = false;
        int w = Console.WindowWidth;
        int h = Console.WindowHeight;
        Random rng = new();
        List<Particle> particles = [];

        int frame = 0;
        while (frame < 300)
        {
            if (Console.KeyAvailable)
            {
                Input.NextKey();
                break;
            }

            // spawn new firework
            if (frame % 20 == 0)
            {
                double x = rng.Next(10, w - 10);
                double y = rng.Next(5, h - 5);
                var color = Colors[rng.Next(Colors.Length)];
                int count = rng.Next(15, 30);
                for (int i = 0; i < count; i++)
                {
                    double angle = rng.NextDouble() * Math.PI * 2;
                    double speed = rng.NextDouble() * 2 + 1;
                    particles.Add(new(x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed * 0.5, color, rng.Next(20, 40)));
                }
            }

            // update & draw
            Console.Clear();
            Console.SetCursorPosition(w / 2 - 6, 1);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("YOU WON!");

            List<Particle> next = [];
            foreach (var p in particles)
            {
                int px = (int)p.X;
                int py = (int)p.Y;
                if (px >= 0 && px < w && py >= 0 && py < h && p.Life > 0)
                {
                    Console.SetCursorPosition(px, py);
                    Console.ForegroundColor = p.Color;
                    Console.Write(p.Life > 10 ? '*' : '.');
                    next.Add(p with { X = p.X + p.Vx, Y = p.Y + p.Vy, Vy = p.Vy + 0.05, Life = p.Life - 1 });
                }
            }
            particles = next;

            Console.ResetColor();
            Thread.Sleep(33);
            frame++;
        }

        Console.CursorVisible = true;
        Console.Clear();
        Console.WriteLine("YOU WON!");
        Console.WriteLine();
        Console.WriteLine("Press any key...");
        Input.NextKey();
    }
}
