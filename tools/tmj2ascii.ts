const tiles = ['.', '#', '+', '<', '>', '-', '|', '_', ',', '~', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J'];

const file = Bun.file(process.argv[2]);
const data = await file.json();

const layer = data.layers[0];
const { width: w, height: h, data: raw } = layer;

const lines: string[] = [];
for (let y = 0; y < h; y++) {
    let row = '';
    for (let x = 0; x < w; x++) {
        const tid = raw[y * w + x];
        row += tid === 0 ? ' ' : tiles[tid - 1];
    }
    lines.push(row.trimEnd());
}

while (lines.length && !lines[lines.length - 1]) lines.pop();

console.log(lines.join('\n'));
