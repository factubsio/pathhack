import { watch } from "fs";

const toolsDir = import.meta.dir;
const watchFiles = new Set(["ascii-art.ts", "ascii-art.html"]);
const reloadSockets = new Set<any>();

watch(toolsDir, (_, filename) => {
  if (!filename || !watchFiles.has(filename)) return;
  console.log(`[reload] ${filename} changed`);
  for (const ws of reloadSockets) ws.send("reload");
});

Bun.serve({
  port: 3005,
  async fetch(req, server) {
    const url = new URL(req.url);
    if (url.pathname === "/ws") {
      if (server.upgrade(req)) return undefined as any;
      return new Response("upgrade failed", { status: 400 });
    }
    if (url.pathname === "/" || url.pathname === "/ascii-art.html")
      return new Response(Bun.file(`${toolsDir}/ascii-art.html`), { headers: { "content-type": "text/html" } });
    if (url.pathname === "/app.js") {
      const result = await Bun.build({ entrypoints: [`${toolsDir}/ascii-art.ts`], minify: false });
      return new Response(await result.outputs[0].text(), { headers: { "content-type": "application/javascript" } });
    }
    return new Response("not found", { status: 404 });
  },
  websocket: {
    open(ws) { reloadSockets.add(ws); },
    close(ws) { reloadSockets.delete(ws); },
    message() {},
  },
});

console.log("http://localhost:3005");
