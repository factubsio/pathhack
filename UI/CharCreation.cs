using Pathhack.Game.Classes;

namespace Pathhack.UI;

public class CharCreation
{
    public ClassDef? Class;
    public DeityDef? Deity;
    public AncestryDef? Ancestry;

    int _classIndex;
    int _deityIndex;
    int _ancestryIndex;

    public bool Run()
    {
        var layer = Draw.Layers[1];
        using var _ = layer.Activate(fullScreen: true);

        int step = 0;
        while (step < 3)
        {
            DrawProgress(layer);
            
            bool? result = step switch
            {
                0 => PickClass(),
                1 => PickDeity(),
                2 => PickAncestry(),
                _ => true,
            };

            if (result == null)
            {
                if (step == 0) return false;
                step--;
            }
            else
            {
                step++;
            }
        }

        return true;
    }

    void DrawProgress(ScreenBuffer layer)
    {
        layer.Clear();
        int x = 2;
        
        if (Class != null)
        {
            layer.Write(x, 0, Class.Name, ConsoleColor.Yellow);
            x += Class.Name.Length;
            layer.Write(x, 0, " > ", ConsoleColor.DarkGray);
            x += 3;
        }

        if (Deity != null)
        {
            layer.Write(x, 0, Deity.Name, ConsoleColor.Cyan);
            x += Deity.Name.Length;
            layer.Write(x, 0, " > ", ConsoleColor.DarkGray);
            x += 3;
        }

        if (Ancestry != null)
        {
            layer.Write(x, 0, Ancestry.Name, ConsoleColor.Green);
            x += Ancestry.Name.Length;
            layer.Write(x, 0, " > ", ConsoleColor.DarkGray);
        }
    }

    bool? PickClass()
    {
        var picked = ListPicker.Pick(ClassDefs.All, "Choose your class:", _classIndex);
        if (picked == null) return null;
        _classIndex = Array.IndexOf(ClassDefs.All, picked);
        Class = picked;
        return true;
    }

    bool? PickDeity()
    {
        var picked = ListPicker.Pick(Pantheon.All, "Choose your deity:", _deityIndex);
        if (picked == null) return null;
        _deityIndex = Array.IndexOf(Pantheon.All, picked);
        Deity = picked;
        return true;
    }

    bool? PickAncestry()
    {
        var picked = ListPicker.Pick(Ancestries.All, "Choose your ancestry:", _ancestryIndex);
        if (picked == null) return null;
        _ancestryIndex = Array.IndexOf(Ancestries.All, picked);
        Ancestry = picked;
        return true;
    }
}
