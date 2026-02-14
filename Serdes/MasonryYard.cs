namespace Pathhack.Serdes;

public static partial class MasonryYard
{
    static readonly Dictionary<string, Func<string?, LogicBrick>> _factories = [];
    static readonly Dictionary<string, LogicBrick> _cache = [];

    static readonly Dictionary<string, Func<string?, ActionBrick>> _actionFactories = [];
    static readonly Dictionary<string, ActionBrick> _actionCache = [];

    // --- LogicBrick ---

    public static IEnumerable<KeyValuePair<string, LogicBrick>> AllBricks => _cache;

    public static void Register(string key, LogicBrick instance)
    {
        if (key == "") throw new ArgumentException($"Brick {instance.GetType().Name} has no Id");
        if (_cache.TryGetValue(key, out LogicBrick? value)) throw new ArgumentException($"Duplicate brick id '{key}' ({instance.GetType().Name} vs {value.GetType().Name})");
        _cache[key] = instance;
    }

    public static void Register(string key, Func<string?, LogicBrick> factory)
    {
        if (key == "") throw new ArgumentException($"Brick factory has no Id");
        if (_factories.TryGetValue(key, out var _)) throw new ArgumentException($"Duplicate factory id '{key}'");
        _factories[key] = factory;
    }

    public static LogicBrick Resolve(string id) =>
        ResolveFrom(_cache, _factories, id);

    public static bool TryResolve(string id, out LogicBrick? brick) =>
        TryResolveFrom(_cache, _factories, id, out brick);

    // --- ActionBrick ---

    public static void RegisterAction(string key, ActionBrick instance) =>
        _actionCache[key] = instance;

    public static void RegisterAction(string key, Func<string?, ActionBrick> factory) =>
        _actionFactories[key] = factory;

    public static ActionBrick ResolveAction(string id) =>
        ResolveFrom(_actionCache, _actionFactories, id);

    public static bool TryResolveAction(string id, out ActionBrick? brick) =>
        TryResolveFrom(_actionCache, _actionFactories, id, out brick);

    // --- Spells ---

    static readonly Dictionary<string, SpellBrickBase> _spells = [];

    public static IEnumerable<SpellBrickBase> AllSpells => _spells.Values;

    public static void RegisterSpell(string name, SpellBrickBase spell) =>
        _spells[name] = spell;

    public static SpellBrickBase? FindSpell(string name) =>
        _spells.GetValueOrDefault(name);

    // --- Shared resolution ---

    static T ResolveFrom<T>(Dictionary<string, T> cache, Dictionary<string, Func<string?, T>> factories, string id)
    {
        if (cache.TryGetValue(id, out var cached))
            return cached;

        int split = id.IndexOf('+');
        string key = split < 0 ? id : id[..split];
        string? cdr = split < 0 ? null : id[(split + 1)..];

        if (!factories.TryGetValue(key, out var factory))
            throw new KeyNotFoundException($"MasonryYard: no factory for '{key}' (full id: '{id}')");

        T brick = factory(cdr);
        cache[id] = brick;
        return brick;
    }

    static bool TryResolveFrom<T>(Dictionary<string, T> cache, Dictionary<string, Func<string?, T>> factories, string id, out T? brick)
    {
        if (cache.TryGetValue(id, out brick))
            return true;

        int split = id.IndexOf('+');
        string key = split < 0 ? id : id[..split];
        string? cdr = split < 0 ? null : id[(split + 1)..];

        if (!factories.TryGetValue(key, out var factory))
        {
            brick = default;
            return false;
        }

        brick = factory(cdr);
        cache[id] = brick;
        return true;
    }
}
