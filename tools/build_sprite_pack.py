import argparse
import io
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


SHEETS = {
    "walk": ("walk.png", 4),
    "sit": ("sit.png", 4),
    "lieDown": ("lie-down.png", 4),
    "sleep": ("sleep.png", 4),
    "pet": ("pet.png", 4),
    "eat": ("eat.png", 4),
}
CANVAS = (520, 390)
BOWL_BOXES = (
    (455, 380, 585, 470),
    (435, 380, 575, 480),
    (450, 395, 585, 490),
    (430, 395, 565, 480),
)


def parse_args():
    parser = argparse.ArgumentParser(description="Build an alpha-matted desktop cat sprite pack")
    parser.add_argument("source_dir", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--model", default="birefnet-general", help="rembg model name")
    parser.add_argument("--only", nargs="+", choices=["idle", *SHEETS.keys()])
    return parser.parse_args()


def quadrants(sheet):
    width, height = sheet.size
    half_x, half_y = width // 2, height // 2
    margin, gap = 4, 4
    boxes = (
        (margin, margin, half_x - gap, half_y - gap),
        (half_x + gap, margin, width - margin, half_y - gap),
        (margin, half_y + gap, half_x - gap, height - margin),
        (half_x + gap, half_y + gap, width - margin, height - margin),
    )
    return [sheet.crop(box).convert("RGB") for box in boxes]


def model_cutout(image, session):
    from rembg import remove

    output = remove(
        image,
        session=session,
        alpha_matting=False,
        post_process_mask=True,
    )
    if isinstance(output, bytes):
        output = Image.open(io.BytesIO(output))
    return output.convert("RGBA")


def remove_background(image, session, bowl_box=None):
    output = model_cutout(image, session)
    if bowl_box is not None:
        left, top, right, bottom = bowl_box
        bowl_alpha = extract_bowl_alpha(image.crop(bowl_box))
        output_pixels = np.array(output)
        source_pixels = np.array(image.convert("RGBA"))
        bowl_pixels = bowl_alpha > 0
        output_region = output_pixels[top:bottom, left:right]
        source_region = source_pixels[top:bottom, left:right]
        output_region[bowl_pixels, :3] = source_region[bowl_pixels, :3]
        base_alpha = output_pixels[:, :, 3]
        base_alpha[top:bottom, left:right] = np.maximum(
            base_alpha[top:bottom, left:right], bowl_alpha
        )
        output = Image.fromarray(output_pixels)
    return clean_isolated_noise(output)


def extract_bowl_alpha(crop):
    bgr = cv2.cvtColor(np.array(crop), cv2.COLOR_RGB2BGR)
    height, width = bgr.shape[:2]
    mask = np.full((height, width), cv2.GC_PR_BGD, dtype=np.uint8)
    mask[:4, :] = cv2.GC_BGD
    mask[-4:, :] = cv2.GC_BGD
    mask[:, :4] = cv2.GC_BGD
    mask[:, -4:] = cv2.GC_BGD
    cv2.ellipse(
        mask,
        (width // 2, round(height * 0.55)),
        (round(width * 0.43), round(height * 0.38)),
        0,
        0,
        360,
        cv2.GC_PR_FGD,
        -1,
    )
    cv2.ellipse(
        mask,
        (width // 2, round(height * 0.55)),
        (round(width * 0.28), round(height * 0.20)),
        0,
        0,
        360,
        cv2.GC_FGD,
        -1,
    )
    cv2.grabCut(bgr, mask, None, np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64), 6, cv2.GC_INIT_WITH_MASK)
    foreground = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    return cv2.GaussianBlur(foreground, (0, 0), 0.8)


def clean_isolated_noise(image):
    alpha = np.array(image.getchannel("A"))
    binary = (alpha > 12).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(binary, connectivity=8)
    keep = np.zeros_like(binary)
    for label in range(1, count):
        if stats[label, cv2.CC_STAT_AREA] >= 180:
            keep[labels == label] = 1
    alpha[keep == 0] = 0
    image.putalpha(Image.fromarray(alpha))
    return image


def normalize_frame(image):
    alpha = image.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value > 10 else 0).getbbox()
    if not bbox:
        raise ValueError("background removal returned an empty frame")
    subject = image.crop(bbox)
    max_width, max_height = 492, 364
    scale = min(max_width / subject.width, max_height / subject.height)
    size = (max(1, round(subject.width * scale)), max(1, round(subject.height * scale)))
    subject = subject.resize(size, Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = (CANVAS[0] - size[0]) // 2
    y = CANVAS[1] - size[1] - 8
    canvas.alpha_composite(subject, (x, y))
    return canvas


def save_action(action, frames, output_dir, session):
    paths = []
    for index, frame in enumerate(frames):
        result = normalize_frame(
            remove_background(frame, session, bowl_box=BOWL_BOXES[index] if action == "eat" else None)
        )
        name = f"{action}-{index}.png"
        result.save(output_dir / name, optimize=True)
        paths.append(f"frames/{name}")
    return paths


def main():
    args = parse_args()
    from rembg import new_session

    frames_dir = args.output_dir / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    session = new_session(args.model)

    if args.only:
        for action in args.only:
            if action == "idle":
                frame = quadrants(Image.open(args.source_dir / "normal.png"))[1]
                save_action("idle", [frame], frames_dir, session)
                continue
            filename, _ = SHEETS[action]
            save_action(action, quadrants(Image.open(args.source_dir / filename)), frames_dir, session)
        return

    normal = Image.open(args.source_dir / "normal.png")
    idle_frame = quadrants(normal)[1]
    action_frames = {"idle": save_action("idle", [idle_frame], frames_dir, session)}
    for action, (filename, expected_frames) in SHEETS.items():
        sheet = Image.open(args.source_dir / filename)
        frames = quadrants(sheet)
        if len(frames) != expected_frames:
            raise ValueError(f"{filename}: expected {expected_frames} frames")
        action_frames[action] = save_action(action, frames, frames_dir, session)

    manifest = {
        "schemaVersion": 1,
        "id": "tabby-reference-v1",
        "displayName": "Reference Tabby",
        "canvas": {"width": CANVAS[0], "height": CANVAS[1], "anchorX": 0.5, "anchorY": 1.0},
        "actions": {
            "idle": {"frames": action_frames["idle"], "frameMs": 500, "loop": True},
            "walk": {"frames": action_frames["walk"], "frameMs": 145, "loop": True},
            "sit": {"frames": action_frames["sit"], "frameMs": 220, "loop": False},
            "lieDown": {"frames": action_frames["lieDown"], "frameMs": 260, "loop": False},
            "sleep": {"frames": action_frames["sleep"], "frameMs": 320, "loop": False},
            "pet": {"frames": action_frames["pet"], "frameMs": 190, "loop": False},
            "eat": {"frames": action_frames["eat"], "frameMs": 220, "loop": False, "props": ["bowl"]},
        },
    }
    (args.output_dir / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
