import { createSocket, type Socket } from "dgram";
import { appendFileSync, writeFileSync } from "fs";

const TRAFFIC_LOG = "monitor_traffic.log";
function tlog(msg: string) { appendFileSync(TRAFFIC_LOG, msg + "\n"); }

// --- Directions ---

export type Pos = [number, number];
export type Dir = Pos;
export const N:  Dir = [0, -1];
export const S:  Dir = [0, 1];
export const E:  Dir = [1, 0];
export const W:  Dir = [-1, 0];
export const NE: Dir = [1, -1];
export const NW: Dir = [-1, -1];
export const SE: Dir = [1, 1];
export const SW: Dir = [-1, 1];

// --- Pos utils ---

export function add(a: Pos, b: Pos): Pos { return [a[0] + b[0], a[1] + b[1]]; }
export function sub(a: Pos, b: Pos): Pos { return [a[0] - b[0], a[1] - b[1]]; }
export function dist(a: Pos, b: Pos): number { return Math.max(Math.abs(a[0] - b[0]), Math.abs(a[1] - b[1])); }
export function dirTo(from: Pos, to: Pos): Dir { return [Math.sign(to[0] - from[0]), Math.sign(to[1] - from[1])]; }
export function eq(a: Pos, b: Pos): boolean { return a[0] === b[0] && a[1] === b[1]; }

// --- Types ---

interface LogEntry { tag: string; msg: string; data?: Record<string, unknown> }

export interface Response {
  ok?: boolean;
  waiting?: string;
  energy?: number;
  error?: string;
  plines: string[];
  log: LogEntry[];
  [key: string]: unknown;
}

export interface RoundResult {
  startRound: Response;
  endPlayerTurn: Response;
}

// --- Socket ---

let sock: Socket;
let port = 4777;
let host = "127.0.0.1";
let prompt: Response;

export type RunMode = "step" | "watch" | "auto";
let runMode: RunMode = "auto";

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function parseRunMode(): RunMode {
  const idx = process.argv.indexOf("--run-mode");
  if (idx >= 0 && idx + 1 < process.argv.length) {
    const mode = process.argv[idx + 1];
    if (mode === "step" || mode === "watch" || mode === "auto") return mode;
  }
  return "auto";
}

function recv(): Promise<Response> {
  return new Promise((resolve) => {
    sock.once("message", (msg) => {
      const resp = JSON.parse(msg.toString());
      tlog(`<< ${JSON.stringify(resp)}`);
      resolve(resp);
    });
  });
}

function send(obj: Record<string, unknown>): Promise<void> {
  return new Promise((resolve) => {
    tlog(`>> ${JSON.stringify(obj)}`);
    const buf = Buffer.from(JSON.stringify(obj));
    sock.send(buf, port, host, () => resolve());
  });
}

async function command(obj: Record<string, unknown>): Promise<Response> {
  await send(obj);
  prompt = await recv();
  u._stale = true;
  return prompt;
}

// --- Player state ---

interface InspectData {
  id: number;
  hp: number;
  hp_max: number;
  temp_hp: number;
  facts: string[];
}

export const u = {
  _stale: true,
  _cache: null as InspectData | null,

  get pos(): [number, number] { return prompt.pos as [number, number]; },
  get energy(): number { return prompt.energy ?? 0; },

  async refresh(): Promise<InspectData> {
    const r = await command({ cmd: "inspect_u" });
    const unit = (r.result as any)?.unit;
    u._cache = {
      id: unit?.id ?? 0,
      hp: unit?.hp ?? 0,
      hp_max: unit?.hp_max ?? 0,
      temp_hp: unit?.temp_hp ?? 0,
      facts: unit?.facts ?? [],
    };
    u._stale = false;
    return u._cache;
  },

  async id(): Promise<number> {
    if (u._stale) await u.refresh();
    return u._cache!.id;
  },

  async hp(): Promise<number> {
    if (u._stale) await u.refresh();
    return u._cache!.hp;
  },

  async hpMax(): Promise<number> {
    if (u._stale) await u.refresh();
    return u._cache!.hp_max;
  },

  async tempHp(): Promise<number> {
    if (u._stale) await u.refresh();
    return u._cache!.temp_hp;
  },

  async facts(): Promise<string[]> {
    if (u._stale) await u.refresh();
    return u._cache!.facts;
  },

  dirTo(target: Pos): Dir {
    return dirTo(u.pos, target);
  },
};

const _fmtBuffer: string[] = [];
let _verbose = process.argv.includes("--verbose");

function _bufLine(line: string) {
  if (_verbose) console.log(line);
  else _fmtBuffer.push(line);
}

export function fmt(label: string, resp: Response) {
  _bufLine(`${label}:`);
  if (resp.result) _bufLine("  result: " + JSON.stringify(resp.result, null, 2).replace(/\n/g, "\n  "));
  if (resp.plines?.length) _bufLine("  plines: " + JSON.stringify(resp.plines));
  if (resp.log?.length) {
    for (const e of resp.log)
      if (e.data) _bufLine(`  [${e.tag}] ${JSON.stringify(e.data)}`);
      else _bufLine(`  log: ${e.msg}`);
  }
  _bufLine(`  prompt: ${resp.waiting}${resp.energy != null ? ` (energy: ${resp.energy})` : ""}`);
  _bufLine("");
}

function _dumpBuffer() {
  if (_verbose || _fmtBuffer.length === 0) return;
  console.log("--- buffered output ---");
  for (const line of _fmtBuffer) console.log(line);
  console.log("--- end buffered output ---");
  _fmtBuffer.length = 0;
}

const waitForEnter = async () => { for await (const _ of console) { break; } };

function expectGate(expected: string) {
  if (prompt.waiting !== expected)
    throw new Error(`expected '${expected}' gate, at '${prompt.waiting}'`);
}

async function expectAction() {
  expectGate("action");
  if (runMode === "step") await waitForEnter();
}

// --- Connect ---

export async function connect(p = 4777, h = "127.0.0.1", mode?: RunMode) {
  port = p;
  host = h;
  runMode = mode ?? parseRunMode();
  _fmtBuffer.length = 0;
  _testFails = 0;
  _didFail = false;
  u._stale = true;
  u._cache = null;
  writeFileSync(TRAFFIC_LOG, "");
  sock = createSocket("udp4");
  const promise = recv();
  await send({ cmd: "hello" });
  await promise; // hello ack
  prompt = await recv(); // first gate prompt
}

export function disconnect() {
  sock?.close();
}

// --- Gates ---

export async function startRound(): Promise<Response> {
  expectGate("start_round");
  return command({ gate: "start_round" });
}

export async function endPlayerTurn(): Promise<Response> {
  expectGate("end_player_turn");
  const r = await command({ gate: "end_player_turn" });
  if (runMode === "watch") await sleep(180);
  return r;
}

function mergeResponses(a: Response, b: Response): Response {
  return {
    ...b,
    plines: [...(a.plines ?? []), ...(b.plines ?? [])],
    log: [...(a.log ?? []), ...(b.log ?? [])],
  };
}

export async function endTurn(prior?: Response): Promise<Response> {
  let merged = prior ?? { plines: [], log: [] } as Response;
  while (u.energy > 1) merged = mergeResponses(merged, await wait());
  return mergeResponses(merged, await endPlayerTurn());
}

export async function round(fn: () => Promise<void>): Promise<RoundResult> {
  const sr = await startRound();
  await fn();
  const ept = await endPlayerTurn();
  if (runMode === "watch") await sleep(500);
  return { startRound: sr, endPlayerTurn: ept };
}

/** Call at end of test script. step/watch wait for enter, auto shuts down. */
export async function end() {
  if (runMode === "auto") {
    await shutdown();
  } else {
    console.log("(press enter to shut down)");
    for await (const _ of console) { break; }
    await shutdown();
  }
}

// --- Actions ---

export async function move(dir: Dir): Promise<Response> {
  await expectAction();
  return command({ cmd: "move", dir });
}

export async function attack(target: number): Promise<Response> {
  await expectAction();
  return command({ cmd: "attack", target });
}

export async function cast(spell: string, target?: Dir | Pos | number): Promise<Response> {
  await expectAction();
  let t: Record<string, unknown> = {};
  if (Array.isArray(target)) t = { dir: target };
  else if (typeof target === "number") t = { target };
  return command({ cmd: "cast", spell, ...t });
}

export async function castAt(spell: string, pos: Pos): Promise<Response> {
  await expectAction();
  return command({ cmd: "cast", spell, pos });
}

export async function use(ability: string, target: Dir | number): Promise<Response> {
  await expectAction();
  const t = Array.isArray(target) ? { dir: target } : { target };
  return command({ cmd: "use", ability, ...t });
}

export async function wait(): Promise<Response> {
  await expectAction();
  return command({ cmd: "move", dir: [0, 0] });
}

// --- Queries (allowed at any gate) ---

export interface UnitInfo {
  id: number;
  name: string;
  pos: Pos;
  hp: number;
  hp_max: number;
  player: boolean;
}

export async function units(): Promise<Response> {
  return command({ cmd: "units" });
}

export async function findUnit(pred: (u: UnitInfo) => boolean): Promise<UnitInfo | undefined> {
  const r = await units();
  const all = (r.result as any)?.units as UnitInfo[] ?? [];
  return all.find(pred);
}

export async function inspect(pos: Dir): Promise<Response> {
  return command({ cmd: "inspect", pos });
}

export async function inspect_u(): Promise<Response> {
  return command({ cmd: "inspect_u" });
}

export async function query(key: string, target?: number): Promise<Response> {
  return command({ cmd: "query", key, ...(target != null && { target }) });
}

export async function inv(): Promise<Response> {
  return command({ cmd: "inv" });
}

// --- Setup (su, allowed at any gate) ---

export async function spawn(monster: string, p?: Dir): Promise<Response> {
  return command({ cmd: "spawn", monster, ...(p && { pos: p }), su: true });
}

export async function equip(item: string): Promise<Response> {
  return command({ cmd: "equip", item, su: true });
}

export async function grant(brick: string): Promise<Response> {
  return command({ cmd: "grant", brick, su: true });
}

export async function grantFact(id: string, opts?: { duration?: number; stacks?: number }): Promise<Response> {
  return command({ cmd: "grant", fact: id, ...opts, su: true });
}

export async function grantSpell(name: string, withSlots?: number): Promise<Response> {
  return command({ cmd: "grant", spell: name, ...(withSlots != null && { with_slots: withSlots }), su: true });
}

export async function grantPool(name: string, max: number, regen?: number): Promise<Response> {
  return command({ cmd: "grant", pool: name, max, ...(regen != null && { regen }), su: true });
}

export async function levelup(n = 1): Promise<Response> {
  return command({ cmd: "levelup", n, su: true });
}

export async function setHp(hp: number, target?: number): Promise<Response> {
  return command({ cmd: "sethp", hp, ...(target != null && { target }), su: true });
}

export async function doDmg(target: number, amount: number, type?: string): Promise<Response> {
  return command({ cmd: "do_dmg", target, amount, ...(type != null && { type }), su: true });
}

export async function kill(target: number): Promise<Response> {
  return doDmg(target, 9999);
}

export async function reset(): Promise<Response> {
  return command({ cmd: "reset", su: true });
}

export async function shutdown(): Promise<void> {
  await send({ cmd: "shutdown", su: true });
  sock?.close();
}

// --- Structured log types ---

export interface CheckData {
  key: string; dc: number; roll: number; base_roll: number;
  mods: { cat: string; value: number; why: string }[];
  result: boolean; advantage: number; disadvantage: number;
  tag?: string;
}

export interface RollData {
  formula: string; type: string; rolled: number; total: number;
  dr: number; prot: number; halved?: boolean; doubled?: boolean; tags?: string;
}

export interface DamageData {
  source: string; target: string; total: number;
  rolls: RollData[]; hp_before: number; hp_after: number; saved: boolean;
}

export interface AttackData {
  attacker: string; defender: string; weapon: string;
  roll: number; base_roll: number; ac: number;
  check_mods: { cat: string; value: number; why: string }[];
  advantage: number; disadvantage: number;
  hit: boolean; damage?: number; rolls?: RollData[];
  hp_before?: number; hp_after?: number;
}

// --- Assertions ---

let _testFails = 0;
let _testName = "";

function _findLog<T>(r: Response, tag: string): T | undefined {
  return r.log?.find(e => e.tag === tag)?.data as T | undefined;
}

function _findAllLog<T>(r: Response, tag: string): T[] {
  return (r.log?.filter(e => e.tag === tag).map(e => e.data) ?? []) as T[];
}

function _fail(msg: string, data?: unknown) {
  if (_testFails === 0) _dumpBuffer();
  _testFails++;
  console.log(`FAIL: ${msg}`, data ?? "");
}

export function assertCheck(r: Response, pred: (d: CheckData) => boolean, msg = "assertCheck failed") {
  const d = _findLog<CheckData>(r, "check");
  if (!d || !pred(d)) _fail(msg, d ?? "no check entry");
}

export function assertDamage(r: Response, pred: (d: DamageData) => boolean, msg = "assertDamage failed") {
  const d = _findLog<DamageData>(r, "damage");
  if (!d || !pred(d)) _fail(msg, d ?? "no damage entry");
}

export function assertAttack(r: Response, pred: (d: AttackData) => boolean, msg = "assertAttack failed") {
  const d = _findLog<AttackData>(r, "attack");
  if (!d || !pred(d)) _fail(msg, d ?? "no attack entry");
}

export function assertPline(r: Response, text: string, msg?: string) {
  if (!r.plines?.some(p => p.includes(text)))
    _fail(msg ?? `expected pline containing "${text}"`, r.plines);
}

export function assertNoPline(r: Response, text: string, msg?: string) {
  if (r.plines?.some(p => p.includes(text)))
    _fail(msg ?? `unexpected pline containing "${text}"`, r.plines);
}

export function assert(cond: boolean, msg = "assertion failed") {
  if (!cond) _fail(msg);
}

export function testSummary() {
  if (_testFails > 0) {
    console.log(`✗ ${_testName}: ${_testFails} assertion(s) FAILED`);
    _didFail = true;
    return;
  }
  console.log(`✓ ${_testName}`);
}

export let _didFail = false;

export function setTestName(name: string) {
  _testName = name;
  _testFails = 0;
  _fmtBuffer.length = 0;
  u._stale = true;
  u._cache = null;
}

// --- High-level helpers ---

export interface SpawnAndCastOpts {
  spell: string;
  monster?: string;
  distance?: number;
  dir?: Dir;
  pos?: Pos;
  slots?: number;
  count?: number;
}

export async function spawnAndCastAt(opts: SpawnAndCastOpts): Promise<Response> {
  const { spell, monster = "goblin", distance = 3, dir = E, slots = 3, count = 1 } = opts;

  await grantSpell(spell, slots);
  for (let i = 0; i < count; i++) {
    const offset: Pos = [distance, i === 0 ? 0 : (i % 2 === 0 ? -Math.ceil(i/2) : Math.ceil(i/2))];
    await spawn(monster, add(u.pos, offset));
  }

  await startRound();
  if (opts.pos) return castAt(spell, opts.pos).then(endTurn);
  return cast(spell, dir).then(endTurn);
}
