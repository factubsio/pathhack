import { readFileSync, writeFileSync, mkdtempSync, rmSync } from "fs";
import { tmpdir } from "os";
import { join } from "path";
import { createSocket } from "dgram";

// --- Data ---

interface Bleh {
  id: number;
  text: string;
  status: "open" | "meat" | "test" | "closed";
  notes: string;
}

const DATA_FILE = join(import.meta.dir, "..", "bleh.json");
const UDP_PORT = 7331;

function load(): Bleh[] {
  try {
    return JSON.parse(readFileSync(DATA_FILE, "utf-8"));
  } catch {
    return [];
  }
}

function save(blehs: Bleh[]) {
  writeFileSync(DATA_FILE, JSON.stringify(blehs, null, 2) + "\n");
}

function nextId(blehs: Bleh[]): number {
  return blehs.reduce((max, b) => Math.max(max, b.id), 0) + 1;
}

// --- Terminal ---

const CSI = "\x1b[";
const write = (s: string) => process.stdout.write(s);
const clear = () => write(`${CSI}2J${CSI}H`);
const moveTo = (x: number, y: number) => write(`${CSI}${y + 1};${x + 1}H`);
const fg = (c: number) => `${CSI}${c}m`;
const RESET = `${CSI}0m`;
const REVERSE = `${CSI}7m`;
const BOLD = `${CSI}1m`;
const DIM = `${CSI}2m`;

const statusColors: Record<string, number> = {
  open: 37,    // white
  meat: 35,    // magenta
  test: 33,    // yellow
  closed: 90,  // dark gray
};

const statusLabels: Record<string, string> = {
  open: "OPEN",
  meat: "MEAT",
  test: "TEST",
  closed: "DONE",
};

const STATUSES: Bleh["status"][] = ["open", "meat", "test", "closed"];

// --- State ---

let blehs = load();
let cursor = 0;
let cols = process.stdout.columns || 80;
let rows = process.stdout.rows || 24;
const LIST_WIDTH = 40;

let noteScroll = 0;

function noteMaxScroll(): number {
  if (blehs.length === 0 || cursor >= blehs.length) return 0;
  const b = blehs[cursor];
  if (!b.notes) return 0;
  const detailW = cols - (LIST_WIDTH + 3) - 1;
  const textLines = wordWrap(b.text, detailW);
  const notesStart = 4 + textLines.length + 1;
  const noteMaxVisible = rows - 2 - notesStart - 1;
  const noteLines = wordWrap(b.notes.replace(/\\n/g, "\n"), detailW);
  const overflow = noteLines.length - noteMaxVisible;
  if (overflow <= 0) return 0;
  return overflow + Math.floor(noteMaxVisible / 2);
}

function noteBottomScroll(): number {
  return Math.max(0, noteMaxScroll() - Math.floor((rows - 8) / 2));
}

process.stdout.on("resize", () => {
  cols = process.stdout.columns || 80;
  rows = process.stdout.rows || 24;
  render();
});

// --- Render ---

function render() {
  clear();

  // Header
  moveTo(0, 0);
  write(`${BOLD}BLEH${RESET} ${DIM}(${blehs.length} items)${RESET}`);
  moveTo(0, 1);
  write("─".repeat(cols));

  // List (left pane)
  const maxVisible = rows - 4;
  let scroll = 0;
  if (blehs.length > maxVisible) {
    scroll = Math.max(0, Math.min(cursor - Math.floor(maxVisible / 2), blehs.length - maxVisible));
  }

  for (let i = 0; i < maxVisible && scroll + i < blehs.length; i++) {
    const idx = scroll + i;
    const b = blehs[idx];
    const isCursor = idx === cursor;
    const label = statusLabels[b.status];
    const color = statusColors[b.status];

    moveTo(0, 2 + i);

    const prefix = `${fg(color)}[${label}]${RESET} `;
    const maxText = LIST_WIDTH - label.length - 3;
    const rawText = b.text.replace(/\n/g, " ");
    const text = rawText.length > maxText ? rawText.slice(0, maxText - 1) + "…" : rawText;

    if (isCursor) {
      write(`${REVERSE}${prefix}${text}${RESET}`);
      // pad to LIST_WIDTH
      const pad = LIST_WIDTH - label.length - 3 - text.length;
      if (pad > 0) write(`${REVERSE}${" ".repeat(pad)}${RESET}`);
    } else {
      write(`${prefix}${text}`);
    }
  }

  // Divider
  for (let y = 2; y < rows - 1; y++) {
    moveTo(LIST_WIDTH + 1, y);
    write(`${DIM}│${RESET}`);
  }

  // Detail (right pane)
  const detailX = LIST_WIDTH + 3;
  const detailW = cols - detailX - 1;

  if (blehs.length > 0 && cursor < blehs.length) {
    const b = blehs[cursor];
    const color = statusColors[b.status];

    moveTo(detailX, 2);
    write(`${BOLD}#${b.id}${RESET} ${fg(color)}[${statusLabels[b.status]}]${RESET}`);

    moveTo(detailX, 4);
    // Word-wrap the text
    const textLines = wordWrap(b.text, detailW);
    for (let i = 0; i < textLines.length; i++) {
      moveTo(detailX, 4 + i);
      write(`${BOLD}${textLines[i]}${RESET}`);
    }

    const notesStart = 4 + textLines.length + 1;
    if (b.notes) {
      moveTo(detailX, notesStart);
      write(`${DIM}── notes ──${RESET}`);
      const noteLines = wordWrap(b.notes.replace(/\\n/g, "\n"), detailW);
      const noteMaxVisible = rows - 2 - notesStart - 1;
      const noteEnd = Math.min(noteScroll + noteMaxVisible, noteLines.length);
      for (let i = noteScroll; i < noteEnd; i++) {
        moveTo(detailX, notesStart + 1 + i - noteScroll);
        write(noteLines[i]);
      }
    } else {
      moveTo(detailX, notesStart);
      write(`${DIM}(no notes)${RESET}`);
    }
  }

  // Footer
  moveTo(0, rows - 1);
  write(`${DIM}[j/k] navigate  [o] done  [u] reopen  [e] edit  [n] new  [d] delete  [q] quit${RESET}`);
}

function wordWrap(text: string, width: number): string[] {
  const lines: string[] = [];
  for (const raw of text.split("\n")) {
    if (raw.length <= width) {
      lines.push(raw);
      continue;
    }
    let line = "";
    for (const word of raw.split(" ")) {
      if (line.length + word.length + 1 > width) {
        lines.push(line);
        line = word;
      } else {
        line = line ? line + " " + word : word;
      }
    }
    if (line) lines.push(line);
  }
  return lines;
}

// --- Actions ---

function setStatus(status: Bleh["status"]) {
  if (blehs.length === 0) return;
  blehs[cursor].status = status;
  save(blehs);
}

function editNotes() {
  if (blehs.length === 0) return;
  const b = blehs[cursor];

  const dir = mkdtempSync(join(tmpdir(), "bleh-"));
  const file = join(dir, `bleh-${b.id}.md`);

  const content = [
    `# Bleh #${b.id}: ${b.text}`,
    `# Status: ${b.status}`,
    `#`,
    ...(b.notes ? b.notes.split("\n").map((l) => `# ${l}`) : ["# (no notes yet)"]),
    `#`,
    `### RESPONSE BELOW ###`,
    ``,
  ].join("\n");

  writeFileSync(file, content);

  // Restore terminal before spawning editor
  process.stdin.setRawMode(false);
  process.stdin.pause();
  write(`${CSI}?25h`); // show cursor
  clear();

  const editor = process.env.EDITOR || "vi";
  Bun.spawnSync([editor, file], { stdin: "inherit", stdout: "inherit", stderr: "inherit" });

  // Re-enter raw mode
  process.stdin.resume();
  process.stdin.setRawMode(true);
  write(`${CSI}?25l`); // hide cursor

  const result = readFileSync(file, "utf-8");
  const marker = "### RESPONSE BELOW ###";
  const markerIdx = result.indexOf(marker);
  if (markerIdx >= 0) {
    const response = result.slice(markerIdx + marker.length).trim();
    if (response) {
      b.notes = b.notes ? b.notes + "\n" + response : response;
      b.status = "open";
      save(blehs);
    }
  }

  rmSync(dir, { recursive: true });
}

function addNew() {
  const dir = mkdtempSync(join(tmpdir(), "bleh-"));
  const file = join(dir, "bleh-new.md");
  writeFileSync(file, "### TYPE BLEH BELOW ###\n\n");

  process.stdin.setRawMode(false);
  process.stdin.pause();
  write(`${CSI}?25h`);
  clear();

  const editor = process.env.EDITOR || "vi";
  Bun.spawnSync([editor, file], { stdin: "inherit", stdout: "inherit", stderr: "inherit" });

  process.stdin.resume();
  process.stdin.setRawMode(true);
  write(`${CSI}?25l`);

  const result = readFileSync(file, "utf-8");
  const marker = "### TYPE BLEH BELOW ###";
  const markerIdx = result.indexOf(marker);
  if (markerIdx >= 0) {
    const text = result.slice(markerIdx + marker.length).trim();
    if (text) {
      blehs.push({ id: nextId(blehs), text, status: "open", notes: "" });
      save(blehs);
      cursor = blehs.length - 1;
    }
  }

  rmSync(dir, { recursive: true });
}

function deleteCurrent() {
  if (blehs.length === 0) return;
  // y/n confirm
  moveTo(0, rows - 1);
  write(" ".repeat(cols));
  moveTo(0, rows - 1);
  write(`${BOLD}Delete #${blehs[cursor].id}? [y/n]${RESET}`);
  process.stdout.write("");
  // wait handled by returning a flag
}

let pendingDelete = false;

// --- UDP Server ---

const udp = createSocket("udp4");

udp.on("message", (msg) => {
  try {
    const cmd = JSON.parse(msg.toString());

    if (cmd.cmd === "add" && cmd.text) {
      blehs.push({ id: nextId(blehs), text: cmd.text, status: "open", notes: "" });
      save(blehs);
      render();
    } else if (cmd.cmd === "status" && cmd.id && cmd.status) {
      const b = blehs.find((b) => b.id === cmd.id);
      if (b && STATUSES.includes(cmd.status)) {
        b.status = cmd.status;
        if (cmd.notes !== undefined) {
          b.notes = b.notes ? b.notes + "\n" + cmd.notes : cmd.notes;
        }
        save(blehs);
        render();
      }
    } else if (cmd.cmd === "notes" && cmd.id && cmd.notes) {
      const b = blehs.find((b) => b.id === cmd.id);
      if (b) {
        b.notes = b.notes ? b.notes + "\n" + cmd.notes : cmd.notes;
        save(blehs);
        render();
      }
    }
  } catch {
    // ignore malformed
  }
});

udp.bind(UDP_PORT, "127.0.0.1");

// --- Input Loop ---

process.stdin.setRawMode(true);
process.stdin.resume();
process.stdin.setEncoding("utf-8");
write(`${CSI}?25l`); // hide cursor

render();

process.stdin.on("data", (key: string) => {
  const code = key.charCodeAt(0);

  // Arrow key sequences
  if (key === "\x1b[A") key = "k";
  if (key === "\x1b[B") key = "j";
  if (key === "\x1b[C") key = "l";
  if (key === "\x1b[D") key = "h";

  if (pendingDelete) {
    if (key === "y") {
      blehs.splice(cursor, 1);
      if (cursor >= blehs.length) cursor = Math.max(0, blehs.length - 1);
      save(blehs);
    }
    pendingDelete = false;
    render();
    return;
  }

  if (key === "q") {
    write(`${CSI}?25h`);
    clear();
    process.exit(0);
  }

  if (key === "j") {
    cursor = Math.min(cursor + 1, blehs.length - 1);
    noteScroll = noteBottomScroll();
  } else if (key === "k") {
    cursor = Math.max(cursor - 1, 0);
    noteScroll = noteBottomScroll();
  } else if (code === 4) { // ctrl-d
    const half = Math.floor((rows - 8) / 2);
    const maxScroll = noteMaxScroll();
    noteScroll = Math.min(noteScroll + half, maxScroll);
  } else if (code === 21) { // ctrl-u
    const half = Math.floor((rows - 8) / 2);
    noteScroll = Math.max(0, noteScroll - half);
  } else if (key === "o") {
    setStatus("closed");
  } else if (key === "u") {
    setStatus("open");
  } else if (key === "e") {
    editNotes();
  } else if (key === "n") {
    addNew();
    return;
  } else if (key === "x") {
    if (blehs.length === 0) return;
    const b = blehs[cursor];
    b.notes = b.notes ? b.notes + "\nexecute" : "execute";
    b.status = "open";
    save(blehs);
  } else if (key === "d") {
    deleteCurrent();
    pendingDelete = true;
    return;
  }

  render();
});

// Cleanup on exit
process.on("exit", () => {
  write(`${CSI}?25h`);
  clear();
  udp.close();
});

process.on("SIGINT", () => process.exit(0));
process.on("SIGTERM", () => process.exit(0));
