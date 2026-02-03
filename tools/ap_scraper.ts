/// <reference types="bun-types" />
// bun run tools/ap_scraper.ts download  - fetch HTML to tools/ap_html/
// bun run tools/ap_scraper.ts parse     - parse local HTML to markdown

import { mkdir } from "node:fs/promises";
import * as cheerio from "cheerio";

const APS = [
  // 1E
  "Rise_of_the_Runelords",
  "Curse_of_the_Crimson_Throne",
  "Second_Darkness", 
  "Legacy_of_Fire",
  "Council_of_Thieves_(adventure_path)",
  "Kingmaker_(adventure_path)",
  "Serpent%27s_Skull_(adventure_path)",
  "Carrion_Crown",
  "Jade_Regent_(adventure_path)",
  "Skull_%26_Shackles",
  "Shattered_Star_(adventure_path)",
  "Reign_of_Winter",
  "Wrath_of_the_Righteous",
  "Mummy%27s_Mask",
  "Iron_Gods_(adventure_path)",
  "Giantslayer",
  "Hell%27s_Rebels",
  "Hell%27s_Vengeance",
  "Strange_Aeons",
  "Ironfang_Invasion_(adventure_path)",
  "Ruins_of_Azlant_(adventure_path)",
  "War_for_the_Crown_(adventure_path)",
  "Return_of_the_Runelords",
  "Tyrant%27s_Grasp_(adventure_path)",
  // 2E
  "Age_of_Ashes_(adventure_path)",
  "Extinction_Curse",
  "Agents_of_Edgewatch",
  "Abomination_Vaults_(adventure_path)",
  "Fists_of_the_Ruby_Phoenix",
  "Strength_of_Thousands",
  "Quest_for_the_Frozen_Flame",
  "Outlaws_of_Alkenstar",
  "Blood_Lords_(adventure_path)",
  "Gatewalkers",
  "Stolen_Fate",
  "Sky_King%27s_Tomb",
  "Season_of_Ghosts",
];

interface Chapter {
  title: string;
  summary: string;
}

interface AP {
  name: string;
  url: string;
  summary: string;
  chapters: Chapter[];
}

function parseAPHtml(slug: string, html: string): AP {
  const $ = cheerio.load(html);
  const url = `https://pathfinderwiki.com/wiki/${slug}`;
  
  const name = $("#firstHeading").text().replace(/ \(adventure path\)/, "").trim() || slug;
  
  // Summary: first <p> that contains "<b>Name</b> is the..."
  const summary = $(".mw-parser-output > p").first().text().trim();
  
  // Chapters: entries in the "Pathfinder Adventure Path" publication gallery
  const chapters: Chapter[] = [];
  $(".publication-gallery").each((_, gallery) => {
    const header = $(gallery).find(".header").text();
    if (header !== "Pathfinder Adventure Path") return;
    
    $(gallery).find(".entry").each((_, entry) => {
      const title = $(entry).find(".title a").text().trim();
      const text = $(entry).find(".text p").text().trim();
      if (title) {
        chapters.push({ title, summary: text });
      }
    });
  });
  
  return { name, url, summary, chapters };
}

async function scrapeAP(slug: string): Promise<AP | null> {
  const url = `https://pathfinderwiki.com/wiki/${slug}`;
  try {
    const res = await fetch(url);
    const html = await res.text();
    return parseAPHtml(slug, html);
  } catch (e) {
    console.error(`Failed to fetch ${slug}:`, e);
    return null;
  }
}

async function main() {
  const results: AP[] = [];
  await mkdir("docs/ap", { recursive: true });
  
  for (const slug of APS) {
    console.error(`Parsing ${slug}...`);
    const html = await Bun.file(`tools/ap_html/${slug}.html`).text();
    const ap = parseAPHtml(slug, html);
    results.push(ap);
    
    // Write full detail file
    let detail = `# ${ap.name}\n\n${ap.summary}\n\n`;
    if (ap.chapters.length > 0) {
      detail += "## Chapters\n\n";
      for (const ch of ap.chapters) {
        detail += `### ${ch.title}\n\n${ch.summary}\n\n`;
      }
    }
    const filename = ap.name.toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/_+$/, "");
    await Bun.write(`docs/ap/${filename}.md`, detail);
  }
  
  // Output as markdown
  let md = "# Pathfinder Adventure Paths - Chapters\n\n";
  md += "Auto-generated from pathfinderwiki.com\n\n";
  
  for (const ap of results) {
    md += `## ${ap.name}\n\n`;
    md += `${ap.summary.slice(0, 500)}\n\n`;
    if (ap.chapters.length > 0) {
      md += "### Chapters\n\n";
      for (const ch of ap.chapters) {
        md += `- **${ch.title}**`;
        if (ch.summary) md += `: ${ch.summary.slice(0, 200)}...`;
        md += "\n";
      }
    }
    md += "\n---\n\n";
  }
  
  console.log(md);
  await Bun.write("docs/ap_chapters.md", md);
  console.error("Wrote docs/ap_chapters.md");
}

async function download() {
  const dir = "tools/ap_html";
  await mkdir(dir, { recursive: true });
  
  for (const slug of APS) {
    const url = `https://pathfinderwiki.com/wiki/${slug}`;
    console.error(`Fetching ${slug}...`);
    const res = await fetch(url);
    const html = await res.text();
    await Bun.write(`${dir}/${slug}.html`, html);
    await new Promise(r => setTimeout(r, 200));
  }
  console.error("Done.");
}

async function parseLocal(slug: string) {
  const html = await Bun.file(`tools/ap_html/${slug}.html`).text();
  const ap = parseAPHtml(slug, html);
  console.log(JSON.stringify(ap, null, 2));
}

const cmd = process.argv[2];
const arg = process.argv[3];
if (cmd === "download") download();
else if (cmd === "parse") main();
else if (cmd === "test" && arg) parseLocal(arg);
else console.error("Usage: bun run tools/ap_scraper.ts [download|parse|test <slug>]");
