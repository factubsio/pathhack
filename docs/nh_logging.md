# nh_logging — structured game logging for roguelikes

## Format

Line-based, one JSON object per event:

```
[R{round}] [{tag}] {json}
```

We went with line-per-event rather than a single JSON document because you can
grep it, tail it, wc -l it, and per-line JSON.parse is what V8 is good at. A
single giant doc means you can't stream or tail, and the parser has to hold the
whole AST in memory.

## Common vocabulary

Tags and field names are shared across games — `attack` means attack, `hit` is
a bool, `ac` is the target number, and so on. The schema is the union of all
games that use this format, and each game fills in what it has. dNethack doesn't
have modifier stacking so it omits `check_mods`, pathhack doesn't have polymorph
so it omits that. The viewer handles missing fields gracefully since everything
is optional — it shows what's there and ignores what isn't.

Unknown tags get skipped by the parser (switch default falls through), so either
game can add new tags and the viewer either renders them or ignores them without
any coordination.

## Current tags

| Tag | Key fields | Notes |
|---|---|---|
| attack | attacker, defender, weapon, roll, base_roll, ac, hit, damage?, hp_before?, hp_after? | check_mods, advantage, disadvantage are pathhack-specific |
| check | key, dc, roll, base_roll, result, tag | saves, skill checks, etc |
| damage | source, target, total, hp_before, hp_after | rolls array has per-die detail |
| spawn | id, name, level, reason | facts/equip arrays are pathhack-specific |
| death | id, name, hits, misses, dmg | hits+misses = TTK in attack rolls |
| levelup | level, xp, hits, misses, dmg_taken | cumulative stats at level-up |
| exp | amount, total, xl, dl, src | dl = effective dungeon level |
| heal | source, target, roll, actual | |
| equip | item | also unequip |
| cast | spell, targeting | |
| action | unit, action, spell | monster AI decisions |

## Tooling

All shared — `bun tools/log-serve.ts game.log` serves the viewer for any game's log.

- `tools/log-common.ts` — interfaces and parser, shared between server and client
- `tools/log-serve.ts` — dev server, parses log on each request, serves viewer
- `tools/log-view.ts` — browser client with uPlot time series chart and panels
- `tools/log-view.html` — layout
- `tools/analyze-log.ts` — CLI tool, imports the same parser

## Viewer

The top of the page is a time series chart (HP, XP, DL, whatever has data) with
independent Y scales. A draggable cursor sets the "current round", and the
window covers [cursor - N, cursor] where N is adjustable (default 200). You
click-drag to scrub — mouse hover doesn't move the cursor.

Bottom panels update as you scrub: the events log scrolls to the cursor position
with most recent at top, while aggregates like attacks, saves, and TTK are
computed over the window.

## dNethack integration (future)

A small C header with an `slog(round, tag, fmt, ...)` macro that does fprintf.
The JSON part is just sprintf for fixed schemas so we don't need an allocator.

It's incrementally useful — instrument `killed()` and you immediately get TTK,
add `thitu`/`hmon` and you get attack stats, one slog call per hook point. The
hook points already exist at the same places you'd grep for `losehp`, `thitu`,
`hmon`, `makemon`, `killed`.

## Perf

It's text parsing, who cares. Re-parse on page refresh.
