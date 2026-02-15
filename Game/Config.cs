namespace Pathhack.Game;

public static class Config
{
    public static bool AutoPickup { get; private set; } = false;
    public static HashSet<char> AutoPickupClasses { get; } = [];

    public static void Load()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pathhackrc");
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim().ToLowerInvariant();
            var val = line[(eq + 1)..].Trim();

            switch (key)
            {
                case "autopickup":
                    AutoPickup = val is "true" or "1" or "yes";
                    break;
                case "pickup_classes":
                    AutoPickupClasses.Clear();
                    foreach (var c in val) AutoPickupClasses.Add(c);
                    break;
            }
        }
    }
}
