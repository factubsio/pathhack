namespace Pathhack.Game;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public record struct LogEntry(string Tag, string Msg, string? Json = null);

public static class PHMonitor
{
    public static bool Active { get; private set; }

    static UdpClient? _socket;
    static IPEndPoint? _clientEp;

    static readonly List<string> _plines = [];
    static readonly List<LogEntry> _log = [];

    const int DefaultPort = 4777;

    public static void Init(int port = DefaultPort)
    {
        _socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        Active = true;
        Log.Write($"monitor: listening on port {port}");

        // Block until client connects with a hello
        IPEndPoint ep = new(IPAddress.Any, 0);
        _socket.Receive(ref ep);
        _clientEp = ep;
        Log.Write($"monitor: client connected from {ep}");
        SendOk();
    }

    // --- Capture ---

    public static void CapturePline(string msg)
    {
        if (!Active) return;
        _plines.Add(msg);
    }

    public static void CaptureLog(string tag, string msg)
    {
        if (!Active) return;
        _log.Add(new(tag, msg));
    }

    public static void CaptureLog(string tag, string msg, string json)
    {
        if (!Active) return;
        _log.Add(new(tag, msg, json));
    }

    static (string[] Plines, LogEntry[] Log) FlushBuffers()
    {
        string[] plines = [.. _plines];
        LogEntry[] log = [.. _log];
        _plines.Clear();
        _log.Clear();
        return (plines, log);
    }

    // --- Protocol ---

    static JsonObject Recv()
    {
        IPEndPoint ep = new(IPAddress.Any, 0);
        byte[] data = _socket!.Receive(ref ep);
        _clientEp = ep;
        return JsonNode.Parse(Encoding.UTF8.GetString(data))!.AsObject();
    }

    static void Send(JsonObject response)
    {
        byte[] data = Encoding.UTF8.GetBytes(response.ToJsonString());
        _socket!.Send(data, data.Length, _clientEp!);
    }

    static void SendResponse(JsonObject response)
    {
        var (plines, log) = FlushBuffers();
        response["pos"] = new JsonArray(upos.X, upos.Y);
        if (plines.Length > 0)
            response["plines"] = new JsonArray([.. plines.Select(p => JsonValue.Create(p))]);
        if (log.Length > 0)
        {
            JsonArray logArr = [];
            foreach (var entry in log)
            {
                JsonObject obj = new() { ["tag"] = entry.Tag, ["msg"] = entry.Msg };
                if (entry.Json != null)
                    obj["data"] = JsonNode.Parse(entry.Json);
                logArr.Add(obj);
            }
            response["log"] = logArr;
        }
        if (_queryResult != null)
        {
            response["result"] = _queryResult;
            _queryResult = null;
        }
        Send(response);
    }

    // --- Gates ---

    /// <summary>Gate 1: before round start hooks.</summary>
    public static void WaitForStartRound()
    {
        if (!Active) return;
        WaitForGate("start_round", false);
    }

    /// <summary>Gate 2: replaces Input.PlayerTurn().</summary>
    public static void WaitForAction(int energy)
    {
        if (!Active) return;
        WaitForGate("action", true, new JsonObject { ["energy"] = energy });
    }

    /// <summary>Gate 3: after player energy exhausted.</summary>
    public static void WaitForEndPlayerTurn()
    {
        if (!Active) return;
        WaitForGate("end_player_turn", false);
    }

    static void WaitForGate(string gate, bool allowNonSu, JsonObject? extra = null)
    {
        while (true)
        {
            JsonObject waiting = extra ?? [];
            waiting["waiting"] = gate;
            SendResponse(waiting);

            JsonObject cmd = Recv();

            bool su = cmd["su"]?.GetValueKind() == JsonValueKind.True;
            string verb = cmd["cmd"]?.GetValue<string>() ?? "";
            bool isQuery = IsQuery(verb);
            bool gateAck = cmd["gate"]?.GetValue<string>() == gate;

            if (!isQuery && !gateAck && !allowNonSu && !su)
                throw new NotSupportedException($"non-su command '{verb}' received at '{gate}' gate");

            if (verb != "") Dispatch(cmd, verb, su);

            if (gateAck || (!isQuery && !su)) return;
        }
    }

    static bool IsQuery(string verb) => verb is "units" or "inspect" or "inspect_u" or "query" or "inv";

    // --- Dispatch ---

    static void Dispatch(JsonObject cmd, string verb, bool su)
    {
        switch (verb)
        {
            case "move":
                var dir = cmd["dir"]?.AsArray();
                if (dir is not { Count: 2 }) { Log.Write("monitor: move requires dir: [x, y]"); break; }
                DoMoveU(new Pos(dir[0]!.GetValue<int>(), dir[1]!.GetValue<int>()));
                break;
            case "shutdown":
                throw new GameOverException();
            case "units":
                DispatchUnits();
                break;
            case "inspect":
                var posArr = cmd["pos"]?.AsArray();
                if (posArr is not { Count: 2 }) { Log.Write("monitor: inspect requires pos: [x, y]"); break; }
                DispatchInspect(new Pos(posArr[0]!.GetValue<int>(), posArr[1]!.GetValue<int>()));
                break;
            case "inspect_u":
                DispatchInspect(upos);
                break;
            case "query":
                var key = cmd["key"]?.GetValue<string>();
                if (key == null) { Log.Write("monitor: query requires key"); break; }
                int? targetId = cmd["target"]?.GetValue<int>();
                DispatchQuery(key, targetId);
                break;
            case "inv":
                DispatchInv();
                break;
            case "spawn":
                DispatchSpawn(cmd);
                break;
            case "cast":
                DispatchCast(cmd);
                break;
            case "grant":
                DispatchGrant(cmd);
                break;
            case "kill":
                var kid = cmd["target"]?.GetValue<int>();
                if (kid == null) { Log.Write("monitor: kill requires target"); break; }
                var victim = lvl.LiveUnits.FirstOrDefault(x => x.Id == (uint)kid);
                if (victim == null) { Log.Write($"monitor: no unit with id {kid}"); break; }
                using (var kctx = PHContext.Create(u, Target.From(victim)))
                {
                    kctx.Damage.Add(new DamageRoll { Formula = victim.HP.Current + 100, Type = DamageTypes.Force });
                    DoDamage(kctx);
                }
                break;
            case "do_dmg":
                DispatchDoDmg(cmd);
                break;
            case "sethp":
                var hp = cmd["hp"]?.GetValue<int>() ?? u.HP.Current;
                var sid = cmd["target"]?.GetValue<int>();
                IUnit hpTarget = sid != null ? lvl.LiveUnits.First(x => x.Id == (uint)sid) : u;
                if (hp > hpTarget.HP.Max) hpTarget.HP.BaseMax = hp;
                hpTarget.HP.Current = hp;
                break;
            default:
                Log.Write($"monitor: unknown command '{verb}'");
                break;
        }
    }

    // --- Query implementations ---

    static void DispatchUnits()
    {
        JsonArray units = [];
        foreach (var unit in lvl.LiveUnits)
        {
            units.Add(new JsonObject
            {
                ["id"] = unit.Id,
                ["name"] = unit.ToString(),
                ["pos"] = new JsonArray(unit.Pos.X, unit.Pos.Y),
                ["hp"] = unit.HP.Current,
                ["hp_max"] = unit.HP.Max,
                ["player"] = unit.IsPlayer,
            });
        }
        _queryResult = new JsonObject { ["units"] = units };
    }

    static void DispatchInspect(Pos pos)
    {
        JsonObject result = new() { ["pos"] = new JsonArray(pos.X, pos.Y) };

        // Tile
        result["tile"] = lvl[pos].Type.ToString();

        // Unit
        var unit = lvl.UnitAt(pos);
        if (unit != null)
        {
            JsonArray facts = [.. unit.ActiveBuffNames.Select(n => JsonValue.Create(n))];
            result["unit"] = new JsonObject
            {
                ["id"] = unit.Id,
                ["name"] = unit.ToString(),
                ["hp"] = unit.HP.Current,
                ["hp_max"] = unit.HP.Max,
                ["temp_hp"] = unit.TempHp,
                ["facts"] = facts,
            };
        }

        // Items
        var items = lvl.ItemsAt(pos);
        if (items.Count > 0)
        {
            JsonArray itemArr = [.. items.Select(i => JsonValue.Create(i.ToString()))];
            result["items"] = itemArr;
        }

        // Trap
        if (lvl.Traps.TryGetValue(pos, out var trap))
            result["trap"] = trap.GetType().Name;

        _queryResult = result;
    }

    static void DispatchQuery(string key, int? targetId)
    {
        IUnit target = u;
        if (targetId != null)
        {
            var found = lvl.LiveUnits.FirstOrDefault(x => x.Id == (uint)targetId);
            if (found == null) { Log.Write($"monitor: no unit with id {targetId}"); return; }
            target = found;
        }

        var mods = target.QueryModifiers(key);
        int total = mods.Calculate();
        _queryResult = new JsonObject
        {
            ["key"] = key,
            ["value"] = total,
            ["breakdown"] = mods.ToString(),
        };
    }

    static void DispatchInv()
    {
        JsonArray items = [];
        foreach (var item in u.Inventory)
        {
            JsonObject entry = new()
            {
                ["letter"] = item.InvLet.ToString(),
                ["name"] = item.ToString(),
            };
            if (u.Equipped.ContainsValue(item)) entry["equipped"] = true;
            items.Add(entry);
        }
        _queryResult = new JsonObject { ["inventory"] = items };
    }

    static JsonObject? _queryResult;

    // --- Action implementations ---

    static void DispatchDoDmg(JsonObject cmd)
    {
        var tid = cmd["target"]?.GetValue<int>();
        if (tid == null) { Log.Write("monitor: do_dmg requires target"); return; }
        var target = lvl.LiveUnits.FirstOrDefault(x => x.Id == (uint)tid);
        if (target == null) { Log.Write($"monitor: no unit with id {tid}"); return; }
        int amount = cmd["amount"]?.GetValue<int>() ?? 1;
        string typeName = cmd["type"]?.GetValue<string>() ?? "force";
        DamageType type = typeName switch
        {
            "fire" => DamageTypes.Fire,
            "cold" => DamageTypes.Cold,
            "shock" => DamageTypes.Shock,
            "acid" => DamageTypes.Acid,
            "sonic" => DamageTypes.Sonic,
            "slashing" => DamageTypes.Slashing,
            "piercing" => DamageTypes.Piercing,
            "blunt" => DamageTypes.Blunt,
            _ => DamageTypes.Force,
        };
        using var ctx = PHContext.Create(u, Target.From(target));
        ctx.Damage.Add(new DamageRoll { Formula = amount, Type = type });
        DoDamage(ctx);
    }

    static void DispatchSpawn(JsonObject cmd)
    {
        var name = cmd["monster"]?.GetValue<string>();
        if (name == null) { Log.Write("monitor: spawn requires monster"); return; }
        var def = name == "dummy" ? DummyThings.Dummy : Wish.WishParser.ParseMonster(name);
        if (def == null) { Log.Write($"monitor: unknown monster '{name}'"); return; }
        Pos? pos = null;
        if (cmd["pos"]?.AsArray() is { Count: 2 } posArr)
            pos = new Pos(posArr[0]!.GetValue<int>(), posArr[1]!.GetValue<int>());
        if (!MonsterSpawner.SpawnAndPlace(lvl, "monitor", def, false, pos: pos))
            Log.Write($"monitor: couldn't place {name}");
    }

    static void DispatchGrant(JsonObject cmd) // TODO: ALLOW TARGET (entity id)
    {
        if (cmd["fact"]?.GetValue<string>() is { } brickId)
        {
            if (!MasonryYard.TryResolve(brickId, out var brick)) { Log.Write($"monitor: unknown brick '{brickId}'"); return; }
            int? duration = cmd["duration"]?.GetValue<int>();
            int stacks = cmd["stacks"]?.GetValue<int>() ?? 1;
            u.AddFact(brick!, duration, stacks);
        }
        else if (cmd["spell"]?.GetValue<string>() is { } spellName)
        {
            var spell = MasonryYard.FindSpell(spellName);
            if (spell == null) { Log.Write($"monitor: unknown spell '{spellName}'"); return; }
            u.AddSpell(spell);
            if (cmd["with_slots"]?.GetValue<int>() is { } slots)
                u.AddPool(spell.Pool, slots, 20);
        }
        else if (cmd["action"]?.GetValue<string>() is { } actionId)
        {
            if (!MasonryYard.TryResolveAction(actionId, out var action)) { Log.Write($"monitor: unknown action '{actionId}'"); return; }
            u.AddAction(action!);
        }
        else if (cmd["pool"]?.GetValue<string>() is { } poolName)
        {
            int max = cmd["max"]?.GetValue<int>() ?? 1;
            int regen = cmd["regen"]?.GetValue<int>() ?? 20;
            u.AddPool(poolName, max, regen);
        }
        else
        {
            Log.Write("monitor: grant requires fact, spell, action, or pool");
        }
    }

    static void DispatchCast(JsonObject cmd)
    {
        var name = cmd["spell"]?.GetValue<string>();
        if (name == null) { Log.Write("monitor: cast requires spell"); return; }

        var spell = u.Spells.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (spell == null) { Log.Write($"monitor: unknown spell '{name}'"); return; }

        var data = u.ActionData.GetValueOrDefault(spell);
        Target target = BuildTarget(cmd, spell.Targeting);

        var plan = spell.CanExecute(u, data, target);
        if (!plan) { Log.Write($"monitor: can't cast '{name}': {plan.WhyNot}"); return; }

        spell.Execute(u, data, target, plan.Plan);
        u.Energy -= spell.GetCost(u, data, target).Value;
    }

    static Target BuildTarget(JsonObject cmd, TargetingType type)
    {
        return type switch
        {
            TargetingType.Direction => cmd["dir"]?.AsArray() is { Count: 2 } d
                ? new Target(null, new Pos(d[0]!.GetValue<int>(), d[1]!.GetValue<int>()))
                : Target.None,
            TargetingType.Unit => ResolveUnitTarget(cmd),
            TargetingType.Pos => cmd["pos"]?.AsArray() is { Count: 2 } p
                ? Target.From(new Pos(p[0]!.GetValue<int>(), p[1]!.GetValue<int>()))
                : Target.None,
            _ => Target.None,
        };
    }


    static Target ResolveUnitTarget(JsonObject cmd)
    {
        var raw = cmd["target"];
        Log.Verbose("monitor_dbg", $"unit target raw={raw} type={raw?.GetValueKind()}");
        if (raw?.GetValue<int>() is not { } id) return Target.None;
        Log.Verbose("monitor_dbg", $"looking for id={id}, live=[{string.Join(",", lvl.LiveUnits.Select(x => $"{x}:{x.Id}"))}]");
        var unit = lvl.LiveUnits.FirstOrDefault(x => x.Id == (uint)id);
        if (unit == null) { Log.Verbose("monitor_dbg", $"unit id={id} not found"); return Target.None; }
        return Target.From(unit);
    }
    // --- Response helpers ---

    public static void SendOk(JsonObject? extra = null)
    {
        JsonObject resp = extra ?? [];
        resp["ok"] = true;
        SendResponse(resp);
    }

    public static void SendError(string reason)
    {
        SendResponse(new JsonObject { ["ok"] = false, ["error"] = reason });
    }

    public static void Shutdown()
    {
        _socket?.Close();
        _socket = null;
        Active = false;
    }
}
