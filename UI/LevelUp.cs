namespace Pathhack.UI;

public static partial class Input
{
    public static void DoLevelUp()
    {
        if (!Progression.HasPendingLevelUp(u))
        {
            g.pline("No level up available.");
            return;
        }

        int newLevel = u.CharacterLevel + 1;

        // Build stages
        List<LevelUpStage> stages = [];

        // Class-specific selections
        var classEntry = u.Class?.Progression.ElementAtOrDefault(newLevel - 1);

        if (classEntry != null)
            foreach (var brick in classEntry.Grants)
                u.AddFact(brick);

        if (classEntry?.Selections != null)
        {
            foreach (var sel in classEntry.Selections)
            {
                var options = sel.Options
                    .Where(f => !u.TakenFeats.Contains(f.id))
                    .Where(f => f.WhyNot != "")
                    .OrderBy(f => f.WhyNot)
                    .ToList();
                if (options.Count > 0)
                    stages.Add(new FeatStage(sel.Label, options, sel.Count));
            }
        }

        // Shared schedule feats
        foreach (var featType in Progression.FeatsAtLevel(newLevel))
        {
            var (label, pool) = featType switch
            {
                FeatType.Class => ("Choose a class feat", u.Class?.ClassFeats ?? []),
                FeatType.General => ("Choose a general feat", GeneralFeats.All),
                FeatType.Ancestry => ("Choose an ancestry feat", u.Ancestry?.Feats ?? []),
                _ => (null, null)
            };
            
            if (pool != null && label != null)
            {
                foreach (var f in pool)
                    Log.Write($"DEBUG: feat={f?.Name ?? "NULL"} id={f?.id ?? "NULL"} level={f?.Level}");
                var available = pool
                    .Where(f => f.Level <= newLevel)
                    .Where(f => !u.TakenFeats.Contains(f.id))
                    .Where(f => f.WhyNot != "")
                    .OrderBy(f => f.WhyNot)
                    .ToList();
                if (available.Count > 0)
                    stages.Add(new FeatStage(label, available, 1));
            }
            else if (featType == FeatType.AttributeBoost)
            {
                stages.Add(new AttributeBoostStage());
            }
        }

        // Run stages with back navigation
        int step = 0;
        while (step < stages.Count)
        {
            bool? result = stages[step].Run();
            if (result == null)
            {
                // HOW DO WE CANCEL: because we need pre-reqs
                // Can we UNDO grants? snapshot stuff? seems so hard?
                if (step > 0)
                    step--;
            }
            else
                step++;
        }
        foreach (var stage in stages)
            stage.Apply();

        u.CharacterLevel = newLevel;
        Log.Structured("levelup", $"{newLevel:level}{u.XP:xp}{u.HitsTaken:hits}{u.MissesTaken:misses}{u.DamageTaken:dmg_taken}");

        // Apply hp gains (after level set!)
        int hpGain = u.Class!.HpPerLevel;
        u.HP.BaseMax += hpGain;
        u.HP.Current += hpGain;

        u.RecalculateMaxHp();

        g.pline($"Welcome to level {newLevel}!");
    }

    interface LevelUpStage
    {
        public abstract bool? Run();
        public abstract void Apply();
    }

    class FeatStage(string label, List<FeatDef> options, int count) : LevelUpStage
    {
        List<FeatDef> _picked = [];

        public bool? Run()
        {
            if (count == 1)
            {
                var picked = ListPicker.Pick(options, label);
                if (picked == null) return null;
                _picked = [picked];
            }
            else
            {
                var picked = ListPicker.PickMultiple(options, label, count);
                if (picked == null) return null;
                _picked = picked;
            }
            return true;
        }

        public void Apply()
        {
            foreach (var feat in _picked)
            {
                u.TakenFeats.Add(feat.id);
                foreach (var brick in feat.Components)
                    u.AddFact(brick);
            }
        }
    }

    class AttributeBoostStage : LevelUpStage
    {
        static readonly SimpleSelectable[] StatSelectables = [
            new("Strength", "Physical power and carrying capacity."),
            new("Dexterity", "Agility, reflexes, and balance."),
            new("Constitution", "Health and stamina."),
            new("Intelligence", "Reasoning and memory."),
            new("Wisdom", "Perception and insight."),
            new("Charisma", "Force of personality."),
        ];

        List<SimpleSelectable>? _picked;

        public bool? Run()
        {
            _picked = ListPicker.PickMultiple(StatSelectables, "Choose 4 attribute boosts:", 4);
            return _picked != null ? true : null;
        }

        public void Apply()
        {
            if (_picked == null) return;
            foreach (var stat in _picked)
            {
                AbilityStat ability = stat.Name switch
                {
                    "Strength" => AbilityStat.Str,
                    "Dexterity" => AbilityStat.Dex,
                    "Constitution" => AbilityStat.Con,
                    "Intelligence" => AbilityStat.Int,
                    "Wisdom" => AbilityStat.Wis,
                    "Charisma" => AbilityStat.Cha,
                    _ => throw new Exception(),
                };
                u.BaseAttributes.Modify(ability, x => x + (x >= 18 ? 1 : 2));
            }
        }
    }
}
