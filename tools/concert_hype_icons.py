"""Draws Concert Hype's lineup icons and stage effects into mods/Concert Hype/assets/Images.

Each lineup icon is a coloured circle with a white symbol; each stage effect (how an idol's song went) is a shape in
its outcome's colour with a dark outline, so it reads over the stage. All are drawn at 4x and scaled down to 64x64.
None of them reuse a game icon, since those already mean something in the game.

    python tools/concert_hype_icons.py            # write the icons
    python tools/concert_hype_icons.py --sheet out.png   # also a preview sheet on the concert screen's dark background
"""

import argparse
from pathlib import Path

import math

from PIL import Image, ImageDraw, ImageFilter

SIZE = 64
SCALE = 4
S = SIZE * SCALE
WHITE = (255, 255, 255, 255)
OUT = Path(__file__).resolve().parents[1] / "mods" / "Concert Hype" / "assets" / "Images"

# File name (without .png) -> background colour
COLOURS = {
    "original": (58, 166, 92),
    "substitute": (232, 128, 36),
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
    "hurt": hurt,
    "resting": resting,
}


# The game's colours (mainScript): gold32, green_light32, red_light32, red32
GOLD = (244, 180, 89)
GREEN_LIGHT = (151, 224, 173)
RED_LIGHT = (255, 174, 193)
RED = (236, 120, 147)
OUTLINE = (32, 27, 23, 230)


def star_points(points, outer, inner, cx=128, cy=128, turn=-90):
    # A star with this many points, its tips on the outer radius and its notches on the inner
    corners = []
    for k in range(points * 2):
        radius = outer if k % 2 == 0 else inner
        angle = math.radians(turn + k * 180 / points)
        corners.append((cx + radius * math.cos(angle), cy + radius * math.sin(angle)))
    return corners


def star(draw):
    # Critical success: a fat five-point star with a shine
    draw.polygon(star_points(5, 112, 52, cy=134), fill=GOLD + (255,))
    draw.polygon(star_points(5, 60, 28, cy=134), fill=(255, 222, 150, 255))


def sparkle_shape(draw, colour, outer=112, inner=26):
    # A four-point sparkle, its points long and thin
    draw.polygon(star_points(4, outer, inner), fill=colour + (255,))
    shine = tuple(c + (255 - c) * 2 // 3 for c in colour)
    draw.ellipse((128 - inner, 128 - inner, 128 + inner, 128 + inner), fill=shine + (255,))


def sparkle(draw):
    # Success: a green sparkle
    sparkle_shape(draw, GREEN_LIGHT)


def burst(draw):
    # The small sparkles that fly out of a critical success
    sparkle_shape(draw, GOLD, outer=116, inner=30)


def face(draw, colour):
    # A round face with its eyes
    draw.ellipse((24, 24, 232, 232), fill=colour + (255,))
    for x in (92, 164):
        draw.ellipse((x - 15, 96, x + 15, 130), fill=OUTLINE)


def sweat(draw):
    # Failure: an awkward face, a wavy mouth and a drop of sweat on its brow
    face(draw, RED_LIGHT)
    draw.line([(88, 182), (108, 170), (128, 182), (148, 170), (168, 182)], fill=OUTLINE, width=13, joint="curve")
    draw.polygon([(206, 22), (184, 66), (228, 66)], fill=(150, 205, 245, 255))
    draw.ellipse((182, 50, 230, 98), fill=(150, 205, 245, 255))
    draw.ellipse((196, 64, 208, 80), fill=(235, 248, 255, 255))


def sad(draw):
    # Critical failure: a sad face, its brows raised in the middle and a deep frown
    face(draw, RED)
    draw.line([(66, 84), (108, 68)], fill=OUTLINE, width=12)
    draw.line([(148, 68), (190, 84)], fill=OUTLINE, width=12)
    draw.arc((70, 158, 186, 250), start=205, end=335, fill=OUTLINE, width=15)


EFFECTS = {
    "critical_success": star,
    "burst": burst,
    "success": sparkle,
    "failure": sweat,
    "critical_failure": sad,
}


def effect(name):
    # The shape, then a dark outline around it: the shape's mask grown and filled dark, under the shape
    shape = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    layer = EFFECTS[name](ImageDraw.Draw(shape))
    if layer is not None:
        shape.alpha_composite(layer)
    grown = shape.getchannel("A").filter(ImageFilter.MaxFilter(15))
    image = Image.new("RGBA", (S, S), OUTLINE[:3] + (0,))
    image.putalpha(grown.point(lambda a: OUTLINE[3] if a > 0 else 0))
    image.alpha_composite(shape)
    return image.resize((SIZE, SIZE), Image.LANCZOS)


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
    icons.update({name: effect(name) for name in EFFECTS})
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
