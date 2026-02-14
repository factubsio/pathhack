import { connect, end, startRound, endPlayerTurn, move, units, inspect_u, inv, fmt, E, S, W, N } from "./monitor";

await connect();

fmt("units", await units());
fmt("inspect_u", await inspect_u());
fmt("inv", await inv());

const side = 5;
for (const [dir, count] of [[E, side], [S, side], [W, side], [N, side]] as const) {
  for (let i = 0; i < count; i++) {
    fmt("startRound", await startRound());
    fmt("move", await move(dir));
    fmt("endPlayerTurn", await endPlayerTurn());
  }
}

fmt("inspect_u", await inspect_u());

await end();
