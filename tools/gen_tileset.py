#!/usr/bin/env python3
from PIL import Image, ImageDraw, ImageFont

W, H = 16, 32
TILES = [
    ('.', (128, 128, 128)),   # floor
    ('#', (139, 90, 43)),     # corridor
    ('+', (255, 255, 0)),     # door
    ('<', (0, 255, 255)),     # stairs up
    ('>', (0, 200, 200)),     # stairs down
    ('-', (255, 255, 255)),   # h wall
    ('|', (255, 255, 255)),   # v wall
    ('_', (255, 0, 255)),     # marker
    (',', (0, 200, 0)),       # grass
    ('~', (0, 100, 255)),     # water
    ('1', (255, 100, 100)),   # room walls - red
    ('2', (100, 255, 100)),   # green
    ('3', (100, 100, 255)),   # blue
    ('4', (255, 255, 100)),   # yellow
    ('5', (255, 100, 255)),   # magenta
    ('6', (100, 255, 255)),   # cyan
    ('7', (255, 180, 100)),   # orange
    ('8', (180, 100, 255)),   # purple
    ('9', (100, 255, 180)),   # teal
    ('A', (255, 80, 80)),     # markers
    ('B', (255, 120, 80)),
    ('C', (255, 160, 80)),
    ('D', (255, 200, 80)),
    ('E', (255, 80, 120)),
    ('F', (255, 80, 160)),
    ('G', (255, 80, 200)),
    ('H', (200, 80, 255)),
    ('I', (160, 80, 255)),
    ('J', (120, 80, 255)),
]

img = Image.new('RGBA', (W * len(TILES), H), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

try:
    font = ImageFont.truetype('/System/Library/Fonts/Menlo.ttc', 24)
except:
    font = ImageFont.load_default()

for i, (ch, color) in enumerate(TILES):
    x = i * W
    bbox = draw.textbbox((0, 0), ch, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    draw.text((x + (W - tw) // 2, (H - th) // 2 - bbox[1]), ch, fill=color, font=font)

img.save('tools/tileset.png')
print(f'Generated tileset.png with {len(TILES)} tiles')
print('Tile order:', ' '.join(t[0] for t in TILES))
