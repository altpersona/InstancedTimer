#!/usr/bin/env python3
"""Generate the 256x256 Thunderstore icon: a countdown-ring glyph on swamp green."""
from PIL import Image, ImageDraw

SIZE = 256
img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# swamp-green background, slightly lighter center
d.ellipse([-60, -60, SIZE + 60, SIZE + 60], fill=(26, 36, 22, 255))
d.ellipse([28, 28, SIZE - 28, SIZE - 28], fill=(34, 46, 28, 255))

cx, cy, r = SIZE // 2, SIZE // 2, 82
width = 14

# dim full ring (workbench gray)
d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(120, 120, 120, 255), width=width)

# bright elapsed arc (countdown) - 75% sweep starting at top
start, end = -90, -90 + int(360 * 0.75)
d.arc([cx - r, cy - r, cx + r, cy + r], start, end, fill=(232, 232, 232, 255), width=width)

# tick marks at 12 / 3 / 6 o'clock (timer feel)
for angle in (-90, 0, 90):
    import math
    a = math.radians(angle)
    x1 = cx + (r - width) * math.cos(a)
    y1 = cy + (r - width) * math.sin(a)
    x2 = cx + (r + width) * math.cos(a)
    y2 = cy + (r + width) * math.sin(a)
    d.line([x1, y1, x2, y2], fill=(26, 36, 22, 255), width=8)

# crypt entrance in the middle: arched doorway, light stone frame, dark interior
door_l, door_r, door_b = cx - 30, cx + 30, cy + 40
arch_top = cy - 28
stone = (168, 172, 148, 255)      # light worn-stone outline
interior = (14, 19, 12, 255)      # near-black doorway
d.rounded_rectangle([door_l - 7, arch_top - 7, door_r + 7, door_b], radius=30,
                    fill=stone)
d.rounded_rectangle([door_l, arch_top, door_r, door_b], radius=24, fill=interior)
# faint green glow line at the base (swamp crypt)
d.rectangle([door_l + 4, door_b - 8, door_r - 4, door_b - 4], fill=(96, 140, 66, 255))

img.save("SunkenCryptTimer/icon.png")
print("wrote SunkenCryptTimer/icon.png", img.size)
