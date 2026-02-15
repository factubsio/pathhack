import { readFileSync, watch, watchFile } from "fs";
import { resolve } from "path";
import { parseLog } from "./log-common";

const toolsDir = import.meta.dir;
const rootDir = resolve(toolsDir, "..");
const logFile = process.argv[2] ?? "game.log";

const reloadSockets = new Set<any>();

// watch tool files for changes → full reload
const watchFiles = ["log-view.ts", "log-view.html", "log-common.ts"];
const watchSet = new Set(watchFiles);
watch(toolsDir, (event, filename) => {
  if (!filename || !watchSet.has(filename)) return;
  console.log(`[reload] ${filename} changed, notifying ${reloadSockets.size} client(s)`);
  for (const ws of reloadSockets) ws.send("reload");
});

// watch log file for changes → data refresh (debounced)
let dataTimer: ReturnType<typeof setTimeout> | null = null;
const logPath = `${rootDir}/${logFile}`;
watchFile(logPath, { interval: 500 }, () => {
  if (dataTimer) return;
  dataTimer = setTimeout(() => {
    dataTimer = null;
    for (const ws of reloadSockets) ws.send("data");
  }, 500);
});
console.log(`[data-watch] watching ${logPath}`);

Bun.serve({
  port: 3001,
  async fetch(req, server) {
    const url = new URL(req.url);
    const path = url.pathname;

    if (path === "/ws") {
      if (server.upgrade(req)) return undefined as any;
      return new Response("upgrade failed", { status: 400 });
    }

    if (path === "/" || path === "/log-view.html")
      return new Response(Bun.file(`${toolsDir}/log-view.html`), { headers: { "content-type": "text/html" } });

    if (path === "/app.js") {
      const result = await Bun.build({ entrypoints: [`${toolsDir}/log-view.ts`], minify: false });
      const js = await result.outputs[0].text();
      return new Response(js, { headers: { "content-type": "application/javascript" } });
    }

    if (path === "/data.json") {
      const content = readFileSync(`${rootDir}/${logFile}`, "utf-8");
      const data = parseLog(content);
      return Response.json(data);
    }

    return new Response("not found", { status: 404 });
  },
  websocket: {
    open(ws) { console.log("[ws] client connected"); reloadSockets.add(ws); },
    close(ws) { console.log("[ws] client disconnected"); reloadSockets.delete(ws); },
    message() {},
  },
});

console.log(`http://localhost:3001  (log: ${logFile})`);
