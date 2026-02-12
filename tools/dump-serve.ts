const toolsDir = import.meta.dir;
const rootDir = toolsDir + "/..";

Bun.serve({
  port: 3000,
  async fetch(req) {
    const url = new URL(req.url);
    const path = url.pathname;

    if (path === "/" || path === "/dump.html")
      return new Response(Bun.file(`${toolsDir}/dump.html`), { headers: { "content-type": "text/html" } });

    if (path === "/dump-view.js") {
      const result = await Bun.build({ entrypoints: [`${toolsDir}/dump-view.ts`], minify: false });
      const js = await result.outputs[0].text();
      return new Response(js, { headers: { "content-type": "application/javascript" } });
    }

    if (path === "/dump.json")
      return new Response(Bun.file(`${rootDir}/dump.json`));

    return new Response("not found", { status: 404 });
  },
});

console.log("http://localhost:3000");
