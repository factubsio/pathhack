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
        // ListPicker creates its own fullscreen transient window, so we just
        // need a persistent one for the progress bar between picks.
        using var handle = WM.CreateTransient(Draw.ScreenWidth, 1, z: 4, opaque: false);
        var progressWin = handle.Window;

        int step = 0;
        while (step < 3)
        {
            DrawProgress(progressWin);
            
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

    void DrawProgress(Window win)
    {
        win.Clear();
        int x = 2;
        
        if (Class != null)
        {
            win.At(x, 0).Write(Class.Name, ConsoleColor.Yellow);
            x += Class.Name.Length;
            win.At(x, 0).Write(" > ", ConsoleColor.DarkGray);
            x += 3;
        }

        if (Deity != null)
        {
            win.At(x, 0).Write(Deity.Name, ConsoleColor.Cyan);
            x += Deity.Name.Length;
            win.At(x, 0).Write(" > ", ConsoleColor.DarkGray);
            x += 3;
        }

        if (Ancestry != null)
        {
            win.At(x, 0).Write(Ancestry.Name, ConsoleColor.Green);
            x += Ancestry.Name.Length;
            win.At(x, 0).Write(" > ", ConsoleColor.DarkGray);
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
