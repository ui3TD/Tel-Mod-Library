"""Draws Concert Hype's lineup icons into mods/Concert Hype/assets/Images.

Each icon is a coloured circle with a white symbol, drawn at 4x and scaled down to 64x64. None of them reuse a
game icon, since those already mean something in the game.

    python tools/concert_hype_icons.py            # write the icons
    python tools/concert_hype_icons.py --sheet out.png   # also a preview sheet on the concert screen's dark background
"""

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 64
SCALE = 4
S = SIZE * SCALE
WHITE = (255, 255, 255, 255)
OUT = Path(__file__).resolve().parents[1] / "mods" / "Concert Hype" / "assets" / "Images"

# File name (without .png) -> background colour
COLOURS = {
    "original": (58, 166, 92),
    "substitute": (232, 128, 36),
    "empty": (128, 134, 146),
    "hosting": (142, 91, 214),
    "hurt": (214, 69, 69),
    "resting": (66, 100, 190),
}


def base(colour):
    image = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((4, 4, S - 5, S - 5), fill=colour + (255,))
    return image, draw


def formation(draw, colour):
    # Dots in rows of 1, 2 and 3 like the senbatsu triangle, the center bigger: her own place in the single
    draw.ellipse((104, 46, 152, 94), fill=WHITE)
    for x in (98, 158):
        draw.ellipse((x - 19, 112, x + 19, 150), fill=WHITE)
    for x in (68, 128, 188):
        draw.ellipse((x - 19, 166, x + 19, 204), fill=WHITE)


def arrow(draw, y, left_to_right):
    # A shaft and a head, pointing right or left
    if left_to_right:
        draw.rectangle((56, y - 11, 160, y + 11), fill=WHITE)
        draw.polygon([(152, y - 34), (206, y), (152, y + 34)], fill=WHITE)
    else:
        draw.rectangle((96, y - 11, 200, y + 11), fill=WHITE)
        draw.polygon([(104, y - 34), (50, y), (104, y + 34)], fill=WHITE)


def swap(draw, colour):
    # One in, one out: she performs in another idol's place
    arrow(draw, 96, True)
    arrow(draw, 160, False)



def empty(draw, colour):
    # A dashed ring: nobody there
    centre, radius, width, dashes = 128, 70, 18, 10
    for i in range(dashes):
        start = i * 360 / dashes
        draw.arc((centre - radius, centre - radius, centre + radius, centre + radius), start, start + 360 / dashes * 0.55,
                 fill=WHITE, width=width)


def hosting(draw, colour):
    # A speech bubble with three dots: she talks
    draw.rounded_rectangle((52, 62, 204, 162), radius=34, fill=WHITE)
    draw.polygon([(88, 150), (128, 150), (80, 200)], fill=WHITE)
    for x in (92, 128, 164):
        draw.ellipse((x - 13, 99, x + 13, 125), fill=colour + (255,))


def hurt(draw, colour):
    # A bandage across the circle, with its pad and holes
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((36, 96, 220, 160), radius=32, fill=WHITE)
    d.rectangle((104, 96, 152, 160), fill=(255, 214, 214, 255))
    for x, y in ((116, 114), (140, 114), (116, 142), (140, 142)):
        d.ellipse((x - 5, y - 5, x + 5, y + 5), fill=colour + (255,))
    return layer.rotate(-45, resample=Image.BICUBIC, center=(128, 128))


def resting(draw, colour):
    # A crescent moon: she sleeps it off
    draw.ellipse((58, 50, 198, 190), fill=WHITE)
    draw.ellipse((96, 30, 226, 160), fill=colour + (255,))


SYMBOLS = {
    "original": formation,
    "substitute": swap,
    "empty": empty,
    "hosting": hosting,
    "hurt": hurt,
    "resting": resting,
}


def icon(name):
    colour = COLOURS[name]
    image, draw = base(colour)
    layer = SYMBOLS[name](draw, colour)
    if layer is not None:
        image.alpha_composite(layer)
    return image.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--sheet", help="also write a preview sheet here")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    icons = {name: icon(name) for name in SYMBOLS}
    for name, image in icons.items():
        image.save(OUT / (name + ".png"))
    if args.sheet:
        # Each icon at its full size and at about its size on screen, on the concert screen's dark background
        sheet = Image.new("RGBA", (len(icons) * 96 + 16, 140), (36, 30, 28, 255))
        for i, image in enumerate(icons.values()):
            sheet.alpha_composite(image, (16 + i * 96, 12))
            small = image.resize((30, 30), Image.LANCZOS)
            sheet.alpha_composite(small, (33 + i * 96, 96))
        sheet.save(args.sheet)
    print("wrote", ", ".join(name + ".png" for name in icons), "to", OUT)


if __name__ == "__main__":
    main()
