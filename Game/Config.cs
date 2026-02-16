namespace Pathhack.Game;

public static class Config
{
    public static bool AutoPickup { get; private set; } = false;
    public static HashSet<char> AutoPickupClasses { get; } = [];
    public static bool AutoDig { get; private set; } = false;

    public static void Load()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pathhackrc");
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            // Require OPTIONS= prefix (NH compatible)
            // Future directives (skip for now, don't warn):
            //   BIND=key:command          — rebind keys
            //   MSGTYPE=type "pattern"    — hide/stop/norep messages
            //   AUTOPICKUP_EXCEPTION=>pat — override autopickup per item glob
            if (!line.StartsWith("OPTIONS=", StringComparison.OrdinalIgnoreCase)) continue;
            line = line[8..];

            // Split comma-separated options
            foreach (var part in line.Split(','))
            {
                var opt = part.Trim();
                if (opt.Length == 0) continue;

                // compound option: key:value
                int colon = opt.IndexOf(':');
                if (colon >= 0)
                {
                    var key = opt[..colon].Trim().ToLowerInvariant();
                    var val = opt[(colon + 1)..].Trim();
                    SetCompound(key, val);
                    continue;
                }

                // boolean: !flag or noflag to disable, bare flag to enable
                bool enable = true;
                if (opt[0] == '!')
                {
                    enable = false;
                    opt = opt[1..];
                }
                else if (opt.StartsWith("no", StringComparison.OrdinalIgnoreCase))
                {
                    enable = false;
                    opt = opt[2..];
                }

                SetBool(opt.ToLowerInvariant(), enable);
            }
        }
    }

    static void SetBool(string key, bool val)
    {
        switch (key)
        {
            case "autopickup": AutoPickup = val; break;
            case "autodig": AutoDig = val; break;
        }
    }

    static void SetCompound(string key, string val)
    {
        switch (key)
        {
            case "pickup_classes":
                AutoPickupClasses.Clear();
                foreach (var c in val) AutoPickupClasses.Add(c);
                break;
        }
    }
}
