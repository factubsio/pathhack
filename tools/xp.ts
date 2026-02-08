#!/usr/bin/env bun

const WIDTH = 60;
const HEIGHT = 20;

function drawGraph(fn: (dl: number) => number, maxDL: number, maxXL: number) {
    const grid: string[][] = Array.from({ length: HEIGHT }, () => 
        Array(WIDTH).fill(' ')
    );

    for (let y = 0; y < HEIGHT; y++) grid[y][0] = '│';
    for (let x = 0; x < WIDTH; x++) grid[HEIGHT - 1][x] = '─';
    grid[HEIGHT - 1][0] = '└';

    for (let x = 1; x < WIDTH; x++) {
        const dl = (x / WIDTH) * maxDL;
        const xl = fn(dl);
        const y = HEIGHT - 2 - Math.round((xl / maxXL) * (HEIGHT - 2));
        if (y >= 0 && y < HEIGHT - 1) grid[y][x] = '•';
    }

    // compute DL needed for each XL
    const dlForXL: number[] = [];
    for (let xl = 1; xl <= maxXL; xl++) {
        for (let dl = 0; dl <= maxDL; dl += 0.1) {
            if (fn(dl) >= xl) { dlForXL[xl] = dl; break; }
        }
    }

    // map Y position to XL
    const yToXL: Map<number, number> = new Map();
    for (let xl = 1; xl <= maxXL; xl++) {
        const y = HEIGHT - 2 - Math.round((xl / maxXL) * (HEIGHT - 2));
        yToXL.set(y, xl);
    }

    console.log(`XL ΔDL  DL`);
    for (let y = 0; y < HEIGHT; y++) {
        const xl = yToXL.get(y);
        let label = '          ';
        if (xl !== undefined) {
            const delta = xl === 1 ? dlForXL[1] : dlForXL[xl] - dlForXL[xl - 1];
            const cum = dlForXL[xl] ?? NaN;
            label = `${xl.toString().padStart(2)} ${delta.toFixed(1).padStart(3)} ${cum.toFixed(0).padStart(3)}`;
        }
        console.log(`${label} ${grid[y].join('')}`);
    }
    console.log(`${''.padStart(WIDTH / 2 + 11)}DL (max ${maxDL})`);
}

const identity = (dl: number) => Math.min(dl, 20);

// dNetHack-style: fast early, flattens hard after ~10
// XL roughly tracks DL until ~8, then slows dramatically
const dnh = (dl: number) => {
    if (dl <= 8) return dl;
    if (dl <= 20) return 8 + (dl - 8) * 0.4;   // ~XL 13 by DL 20
    if (dl <= 35) return 13 + (dl - 20) * 0.3; // ~XL 17.5 by DL 35
    return Math.min(17.5 + (dl - 35) * 0.15, 20);
};

// Smooth ramp: 1:1 for XL 1-2, then gradual increase
// ΔDL starts at 1, ramps up after XL 2
// tuned so XL 20 ~= DL 41
const smooth = (dl: number) => {
    let cumDL = 0;
    for (let xl = 1; xl <= 20; xl++) {
        const delta = xl <= 2 ? 1 : 1 + (xl - 2) * 0.125;
        cumDL += delta;
        if (dl < cumDL) return xl - 1 + (dl - (cumDL - delta)) / delta;
    }
    return 20;
};

drawGraph(smooth, 50, 20);
