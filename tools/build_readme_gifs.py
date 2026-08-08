import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / "desktop-2d" / "sprite-packs" / "orange-tabby"
ACTIONS = json.loads((PACK / "manifest.json").read_text(encoding="utf-8"))["actions"]
OUTPUT = ROOT / "docs" / "media"
CANVAS = (720, 400)
SPRITE_SIZE = 338


def background():
    image = Image.new("RGB", CANVAS)
    pixels = image.load()
    for y in range(CANVAS[1]):
        progress = y / (CANVAS[1] - 1)
        color = (
            round(248 - 13 * progress),
            round(244 - 15 * progress),
            round(238 - 17 * progress),
        )
        for x in range(CANVAS[0]):
            pixels[x, y] = color
    return image


def sprite(action, index, mirrored=False):
    image = Image.open(PACK / ACTIONS[action]["frames"][index]).convert("RGBA")
    image = image.resize((SPRITE_SIZE, SPRITE_SIZE), Image.Resampling.LANCZOS)
    return image.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if mirrored else image


def render(action, index, center_x=360, bottom=386, mirrored=False):
    frame = background()
    cat = sprite(action, index, mirrored=mirrored)
    frame.paste(cat, (round(center_x - SPRITE_SIZE / 2), bottom - SPRITE_SIZE), cat)
    return frame


def save_gif(path, frames, durations):
    palette = frames[0].convert("P", palette=Image.Palette.ADAPTIVE, colors=160)
    encoded = [
        frame.quantize(palette=palette, dither=Image.Dither.FLOYDSTEINBERG)
        for frame in frames
    ]
    encoded[0].save(
        path,
        save_all=True,
        append_images=encoded[1:],
        duration=durations,
        loop=0,
        optimize=True,
        disposal=2,
    )


def build_walk():
    frames = []
    steps = 16
    for step in range(steps):
        center_x = 120 + (480 * step / (steps - 1))
        frames.append(render("walk", step % 8, center_x=center_x))
    for step in range(steps):
        center_x = 600 - (480 * step / (steps - 1))
        frames.append(render("walk", step % 8, center_x=center_x, mirrored=True))
    save_gif(OUTPUT / "bambi-walking.gif", frames, [95] * len(frames))


def build_groom():
    sequence = (
        [("groom", index) for index in range(8)]
        + [("groomLoop", index) for _ in range(2) for index in range(8)]
        + [("groomReturn", index) for index in range(8)]
    )
    frames = [render(action, index) for action, index in sequence]
    durations = [120] * 8 + [140] * 16 + [120] * 8
    save_gif(OUTPUT / "bambi-grooming.gif", frames, durations)


def build_pickup():
    sequence = (
        [("pickupStart", index) for index in range(8)]
        + [("pickedUp", index) for _ in range(2) for index in range(8)]
        + [("landing", index) for index in range(8)]
        + [("walk", index) for index in range(8)]
    )
    frames = [render(action, index) for action, index in sequence]
    durations = [110] * 8 + [120] * 16 + [100] * 8 + [95] * 8
    save_gif(OUTPUT / "bambi-pickup-and-landing.gif", frames, durations)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    build_walk()
    build_groom()
    build_pickup()
    for path in sorted(OUTPUT.glob("*.gif")):
        with Image.open(path) as image:
            print(f"{path.relative_to(ROOT)}: {image.n_frames} frames, {path.stat().st_size} bytes")


if __name__ == "__main__":
    main()
