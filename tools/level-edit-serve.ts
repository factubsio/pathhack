import { readFileSync, readdirSync, writeFileSync, watch } from "fs";
import { resolve } from "path";

const toolsDir = import.meta.dir;
const rootDir = resolve(toolsDir, "..");
const datDir = resolve(rootDir, "Dat");

// --- .cs parsing ---

interface LevelEntry {
  file: string;       // relative path from root
  varName: string;    // C# variable name
  id: string;         // SpecialLevel id
  map: string;        // raw map string (no leading/trailing blank lines)
  startLine: number;  // line number of """ open
  endLine: number;    // line number of """ close
}

function scanFile(relPath: string): LevelEntry[] {
  const abs = resolve(rootDir, relPath);
  const content = readFileSync(abs, "utf-8");
  const lines = content.split("\n");
  const entries: LevelEntry[] = [];

  // Find patterns like: SpecialLevel("id", """
  // Capture the variable name from preceding "= new"
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Match: ... varName = new SpecialLevel("id", """
    // or continuation where """ is on same line as SpecialLevel
    const m = line.match(/(\w+)\s*=\s*new(?:\s+SpecialLevel)?\(\s*"([^"]+)"\s*,\s*"""/);
    if (!m) continue;

    const varName = m[1];
    const id = m[2];
    const startLine = i;

    // Find closing """
    let endLine = -1;
    for (let j = i + 1; j < lines.length; j++) {
      if (lines[j].includes('"""')) {
        endLine = j;
        break;
      }
    }
    if (endLine < 0) continue;

    // Extract map lines between the """ delimiters
    const mapLines = lines.slice(startLine + 1, endLine);

    // Dedent: strip common leading whitespace
    const nonEmpty = mapLines.filter(l => l.trim().length > 0);
    const indent = nonEmpty.length > 0
      ? Math.min(...nonEmpty.map(l => l.match(/^(\s*)/)![1].length))
      : 0;
    const dedented = mapLines.map(l => l.slice(indent));
    const map = dedented.join("\n");

    entries.push({ file: relPath, varName, id, map, startLine, endLine });
  }

  return entries;
}

function scanAll(): LevelEntry[] {
  const entries: LevelEntry[] = [];
  const files = readdirSync(datDir).filter(f => f.endsWith(".cs"));
  for (const f of files) {
    entries.push(...scanFile(`Dat/${f}`));
  }
  return entries;
}

function saveMap(entry: LevelEntry, newMap: string): void {
  const abs = resolve(rootDir, entry.file);
  const content = readFileSync(abs, "utf-8");
  const lines = content.split("\n");

  // Replace lines between startLine+1 and endLine (exclusive)
  const before = lines.slice(0, entry.startLine + 1);
  const after = lines.slice(entry.endLine);
  const mapLines = newMap.split("\n");

  const result = [...before, ...mapLines, ...after].join("\n");
  writeFileSync(abs, result);
}

// --- Server ---

const reloadSockets = new Set<any>();

const watchFiles = new Set(["level-edit.ts", "level-edit.html", "level-edit-serve.ts"]);
watch(toolsDir, (_, filename) => {
  if (!filename || !watchFiles.has(filename)) return;
  console.log(`[reload] ${filename} changed`);
  for (const ws of reloadSockets) ws.send("reload");
});

Bun.serve({
  port: 3002,
  async fetch(req, server) {
    const url = new URL(req.url);
    const path = url.pathname;

    if (path === "/ws") {
      if (server.upgrade(req)) return undefined as any;
      return new Response("upgrade failed", { status: 400 });
    }

    if (path === "/" || path === "/level-edit.html")
      return new Response(Bun.file(`${toolsDir}/level-edit.html`), { headers: { "content-type": "text/html" } });

    if (path === "/app.js") {
      const result = await Bun.build({ entrypoints: [`${toolsDir}/level-edit.ts`], minify: false });
      const js = await result.outputs[0].text();
      return new Response(js, { headers: { "content-type": "application/javascript" } });
    }

    if (path === "/api/levels") {
      const entries = scanAll();
      return Response.json(entries.map(e => ({ file: e.file, varName: e.varName, id: e.id, map: e.map })));
    }

    if (path === "/api/save" && req.method === "POST") {
      const body = await req.json() as { id: string; map: string };
      const entries = scanAll();
      const entry = entries.find(e => e.id === body.id);
      if (!entry) return new Response("not found", { status: 404 });
      saveMap(entry, body.map);
      return Response.json({ ok: true });
    }

    if (path === "/api/new" && req.method === "POST") {
      const entries = scanAll();
      // find next free custom_N id
      let n = 1;
      while (entries.some(e => e.id === `custom_${n}`)) n++;
      const id = `custom_${n}`;
      const varName = `Custom${n}`;

      const customPath = resolve(datDir, "Custom.cs");
      let content: string;
      try {
        content = readFileSync(customPath, "utf-8");
      } catch {
        // create the file
        content = `namespace Pathhack.Dat;\n\npublic static class CustomLevels\n{\n}\n`;
      }

      // build the blank map (80 spaces × 21 rows)
      const blankRow = " ".repeat(80);
      const mapLines = Array(21).fill(blankRow).join("\n");

      // insert before closing brace
      const insertion = `\n    public static readonly SpecialLevel ${varName} = new("${id}", """\n${mapLines}\n""");\n`;
      const lastBrace = content.lastIndexOf("}");
      content = content.slice(0, lastBrace) + insertion + content.slice(lastBrace);
      writeFileSync(customPath, content);

      return Response.json({ ok: true, id });
    }

    return new Response("not found", { status: 404 });
  },
  websocket: {
    open(ws) { reloadSockets.add(ws); },
    close(ws) { reloadSockets.delete(ws); },
    message() {},
  },
});

console.log("http://localhost:3002");
