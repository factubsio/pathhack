# Dumplog System

## Architecture

C# side:
- `UI/BlackBox.cs` — circular buffer of 100 snapshots, recorded at end of each round + on death
- `UI/Dump.cs` — serializes snapshots to JSON, produces self-contained HTML from embedded resources
- Embedded resources: `tools/dump.html` (template), `tools/dump-view.js` (pre-built from TS)

TS/HTML side:
- `tools/dump-view.ts` — viewer with frame scrubber, tooltips, pokedex, FOV
- `tools/dump.html` — HTML shell with inline CSS
- `tools/dump-serve.ts` — Bun dev server for iteration

## Dev workflow

1. Edit `dump-view.ts` or `dump.html`
2. `bun tools/dump-serve.ts` — serves at localhost:3000, compiles TS on the fly
3. Use `#dumplog` in-game or die to produce `dump.json`
4. Refresh browser

When done iterating: `bun build tools/dump-view.ts --outfile tools/dump-view.js` to update the embedded JS.

## Known issues / TODO

### AOT: anonymous types must become records
`System.Text.Json` source generators can't reference anonymous types. All the `new { ... }` in
Dump.cs need to become named records with `[JsonSerializable]` attributes before AOT works.
This also enables the text-mode dump (terminal dumplog) to share the same data structures.

### Compression for full-game recording
Current buffer is 100 frames. For full-game recording (thousands of frames):
- Compress each snapshot to `byte[]` on entry (GZipStream)
- Decompress at dump time
- ~500-800 bytes per frame compressed, so 10K frames ≈ 5-8MB in memory
- Alternatively: chunked compression (groups of N frames) for better cross-frame ratio

### Final HTML gzip
The self-contained HTML compresses extremely well (1.5MB → 26K in testing).
Could emit `.html.gz` and let browsers handle it, or use `DecompressionStream` in JS
with base64-encoded gzip data inline.

### Death frame full reveal
Currently the death frame uses player FOV like all other frames. Could add a
`BlackBox.Record(fullReveal: true)` flag for the death snapshot to show the entire level.

### Phase-in-wall rendering
Wall adjacency flag in vis byte handles entities on wall tiles. Works but untested
with actual phase monsters.

### Wizard mode gating
`dump.json` should only be written in wizard/debug mode. `dump.html` always written.

### Rebuild JS
After editing `dump-view.ts`, must run `bun build tools/dump-view.ts --outfile tools/dump-view.js`
to update the embedded resource. Easy to forget.
