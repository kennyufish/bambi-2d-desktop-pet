import json
import shutil
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


CANVAS = (520, 520)
SHEETS = {
    "idle": ("00-idle-breathing-8f.png", 125, True),
    "walk": ("01-walk-v2-8f.png", 90, True),
    "sit": ("02-sit-down-8f.png", 120, False),
    "lieDown": ("03-lie-down-8f.png", 120, False),
    "sleep": ("04-sleep-8f.png", 140, False),
    "pet": ("05-petting-no-hand-8f.png", 100, False),
    "eat": ("06-eating-8f.png", 100, False),
    "pickupStart": ("07-pickup-start-8f.png", 80, False),
    "pickedUp": ("08-picked-up-scruff-loop-8f.png", 100, True),
    "landing": ("09-release-landing-8f.png", 100, False),
}

GRAB_ANCHOR = (320, 88)
PICKUP_SOURCE_ANCHOR_X = (
    0.68,
    0.66,
    0.58,
    0.54,
    0.52,
    0.50,
    0.50,
    0.50,
)


def split_sheet(sheet):
    frames = []
    for row in range(2):
        for column in range(4):
            left = round(column * sheet.width / 4)
            right = round((column + 1) * sheet.width / 4)
            top = round(row * sheet.height / 2)
            bottom = round((row + 1) * sheet.height / 2)
            frames.append(sheet.crop((left, top, right, bottom)).convert("RGBA"))
    return frames


def split_generated_sheet(sheet):
    pixels = np.array(sheet)
    alpha = pixels[:, :, 3]
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(
        (alpha > 64).astype(np.uint8), connectivity=8
    )
    subjects = sorted(
        (label for label in range(1, count) if stats[label, cv2.CC_STAT_AREA] >= 5000),
        key=lambda label: stats[label, cv2.CC_STAT_AREA],
        reverse=True,
    )[:8]
    if len(subjects) != 8:
        raise ValueError(f"generated sheet contains {len(subjects)} large subjects, expected 8")
    subjects.sort(key=lambda label: (centroids[label][1] >= sheet.height / 2, centroids[label][0]))
    frames = []
    for label in subjects:
        keep = cv2.dilate(
            (labels == label).astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2
        ).astype(bool)
        isolated = pixels.copy()
        isolated[~keep, 3] = 0
        image = Image.fromarray(isolated)
        bbox = image.getchannel("A").getbbox()
        frames.append(image.crop(bbox))
    return frames


def normalize_frame(frame, component_limit):
    alpha = frame.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value > 8 else 0).getbbox()
    if not bbox:
        raise ValueError("transparent sheet contains an empty frame")
    subject = frame.crop(bbox)
    max_width, max_height = CANVAS[0] - 16, CANVAS[1] - 16
    scale = min(1, max_width / subject.width, max_height / subject.height)
    if scale < 1:
        subject = subject.resize(
            (round(subject.width * scale), round(subject.height * scale)),
            Image.Resampling.LANCZOS,
        )
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = (CANVAS[0] - subject.width) // 2
    y = CANVAS[1] - subject.height - 8
    canvas.alpha_composite(subject, (x, y))
    return clean_isolated_pixels(canvas, component_limit)


def scale_subject_from_baseline(frame, scale):
    bbox = frame.getchannel("A").getbbox()
    if not bbox:
        raise ValueError("cannot scale an empty frame")
    subject = frame.crop(bbox)
    target_width = round(subject.width * scale)
    target_height = round(subject.height * scale)
    max_width, max_height = CANVAS[0] - 16, CANVAS[1] - 16
    fit = min(1, max_width / target_width, max_height / target_height)
    subject = subject.resize(
        (round(target_width * fit), round(target_height * fit)),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = (CANVAS[0] - subject.width) // 2
    y = CANVAS[1] - subject.height - 8
    canvas.alpha_composite(subject, (x, y))
    return canvas


def clean_isolated_pixels(image, component_limit):
    pixels = np.array(image)
    alpha = pixels[:, :, 3]
    count, labels, stats, _ = cv2.connectedComponentsWithStats(
        (alpha > 64).astype(np.uint8), connectivity=8
    )
    keep = np.zeros_like(alpha, dtype=bool)
    ranked = sorted(
        range(1, count),
        key=lambda label: stats[label, cv2.CC_STAT_AREA],
        reverse=True,
    )
    for label in ranked[:component_limit]:
        if stats[label, cv2.CC_STAT_AREA] >= 300:
            keep |= labels == label
    keep = cv2.dilate(keep.astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2).astype(bool)
    pixels[~keep, 3] = 0
    return Image.fromarray(pixels)


def find_upper_anchor(subject, x_fraction):
    alpha = np.array(subject.getchannel("A"))
    center_x = round((subject.width - 1) * x_fraction)
    radius = max(3, round(subject.width * 0.02))
    candidates = []
    for x in range(max(0, center_x - radius), min(subject.width, center_x + radius + 1)):
        ys = np.flatnonzero(alpha[:, x] > 64)
        if ys.size:
            candidates.append((abs(x - center_x), int(ys[0]), x))
    if not candidates:
        raise ValueError("could not locate pickup anchor on cat silhouette")
    _, top_y, anchor_x = min(candidates)
    opaque_below = np.flatnonzero(alpha[top_y:, anchor_x] > 64)
    anchor_y = top_y + int(opaque_below[min(6, len(opaque_below) - 1)])
    return anchor_x, anchor_y


def normalize_grab_frame(frame, source_anchor_x):
    cleaned = clean_isolated_pixels(frame, component_limit=1)
    bbox = cleaned.getchannel("A").getbbox()
    if not bbox:
        raise ValueError("pickup sheet contains an empty frame")
    subject = cleaned.crop(bbox)
    anchor_x, anchor_y = find_upper_anchor(subject, source_anchor_x)
    scale = min(
        1,
        GRAB_ANCHOR[0] / max(anchor_x, 1),
        (CANVAS[0] - GRAB_ANCHOR[0]) / max(subject.width - anchor_x, 1),
        GRAB_ANCHOR[1] / max(anchor_y, 1),
        (CANVAS[1] - GRAB_ANCHOR[1]) / max(subject.height - anchor_y, 1),
    )
    if scale < 1:
        subject = subject.resize(
            (round(subject.width * scale), round(subject.height * scale)),
            Image.Resampling.LANCZOS,
        )
        anchor_x = round(anchor_x * scale)
        anchor_y = round(anchor_y * scale)
    x = GRAB_ANCHOR[0] - anchor_x
    y = GRAB_ANCHOR[1] - anchor_y
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(subject, (x, y))
    return clean_isolated_pixels(canvas, component_limit=1)


def normalize_to_reference_bbox(frame, reference_bbox, y_offset=0):
    cleaned = clean_isolated_pixels(frame, component_limit=1)
    bbox = cleaned.getchannel("A").getbbox()
    if not bbox:
        raise ValueError("landing sheet contains an empty hanging frame")
    subject = cleaned.crop(bbox)
    target_height = reference_bbox[3] - reference_bbox[1]
    scale = target_height / subject.height
    subject = subject.resize(
        (round(subject.width * scale), target_height),
        Image.Resampling.LANCZOS,
    )
    target_center_x = (reference_bbox[0] + reference_bbox[2]) // 2
    x = target_center_x - subject.width // 2
    y = reference_bbox[1] + y_offset
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(subject, (x, y))
    return clean_isolated_pixels(canvas, component_limit=1)


def lab_statistics(images):
    samples = []
    for image in images:
        pixels = np.array(image.convert("RGBA"))
        mask = pixels[:, :, 3] > 64
        rgb = pixels[:, :, :3][mask].reshape(-1, 1, 3)
        samples.append(cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).reshape(-1, 3).astype(np.float32))
    combined = np.concatenate(samples, axis=0)
    return combined.mean(axis=0), combined.std(axis=0)


def match_lab_color(image, target_statistics):
    pixels = np.array(image.convert("RGBA"))
    alpha = pixels[:, :, 3]
    statistics_mask = alpha > 64
    edit_mask = alpha > 0
    lab = cv2.cvtColor(pixels[:, :, :3], cv2.COLOR_RGB2LAB).astype(np.float32)
    source = lab[statistics_mask]
    source_mean = source.mean(axis=0)
    source_std = source.std(axis=0)
    target_mean, target_std = target_statistics
    scale = np.clip(target_std / np.maximum(source_std, 1), 0.65, 1.6)
    adjusted = (lab - source_mean) * scale + target_mean
    lab[edit_mask] = adjusted[edit_mask]
    pixels[:, :, :3] = cv2.cvtColor(np.clip(lab, 0, 255).astype(np.uint8), cv2.COLOR_LAB2RGB)
    return Image.fromarray(pixels)


def breathing_frames(source):
    bbox = source.getchannel("A").getbbox()
    subject = source.crop(bbox)
    factors = (1.0, 1.004, 1.008, 1.012, 1.012, 1.008, 1.004, 1.0)
    frames = []
    for factor in factors:
        resized = subject.resize(
            (subject.width, round(subject.height * factor)),
            Image.Resampling.LANCZOS,
        )
        frame = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        frame.alpha_composite(resized, ((CANVAS[0] - resized.width) // 2, bbox[3] - resized.height))
        frames.append(frame)
    return frames


def tail_wag_frames(source):
    pixels = np.array(source)
    height, width = pixels.shape[:2]
    yy, xx = np.mgrid[0:height, 0:width].astype(np.float32)
    weight = np.clip((310 - xx) / 135, 0, 1) * np.clip((yy - 450) / 45, 0, 1)
    phases = (0.0, 0.7, 1.0, 0.7, 0.0, -0.7, -1.0, -0.7)
    frames = []
    for phase in phases:
        map_x = xx - 11 * phase * weight
        map_y = yy + 5 * abs(phase) * weight
        warped = cv2.remap(
            pixels,
            map_x,
            map_y,
            interpolation=cv2.INTER_LANCZOS4,
            borderMode=cv2.BORDER_CONSTANT,
            borderValue=(0, 0, 0, 0),
        )
        blend = np.clip(weight[:, :, None], 0, 1)
        result = (pixels * (1 - blend) + warped * blend).astype(np.uint8)
        frames.append(Image.fromarray(result))
    return frames


def add_derived_action(actions, frames_dir, name, frames, frame_ms, loop):
    paths = []
    for index, frame in enumerate(frames):
        filename = f"{name}-{index}.png"
        clean_isolated_pixels(frame, component_limit=1).save(frames_dir / filename, optimize=True)
        paths.append(f"frames/{filename}")
    actions[name] = {"frames": paths, "frameMs": frame_ms, "loop": loop}


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: import_transparent_8frame_pack.py SOURCE_DIR OUTPUT_DIR")
    source_dir = Path(sys.argv[1])
    output_dir = Path(sys.argv[2])
    frames_dir = output_dir / "frames"
    copied_source_dir = output_dir / "source"
    frames_dir.mkdir(parents=True, exist_ok=True)
    copied_source_dir.mkdir(parents=True, exist_ok=True)

    actions = {}
    landing_color_statistics = None
    for action, (filename, frame_ms, loop) in SHEETS.items():
        source_path = source_dir / filename
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        sheet = Image.open(source_path).convert("RGBA")
        frames = split_generated_sheet(sheet) if action in {"walk", "pickupStart", "pickedUp", "landing"} else split_sheet(sheet)
        if action == "landing":
            frames = [frames[index] for index in (0, 1, 5, 2, 4, 3, 6, 7)]
            references = [
                Image.open(frames_dir / f"{reference_action}-{index}.png").convert("RGBA")
                for reference_action in ("idle", "walk", "pickedUp")
                for index in range(8)
            ]
            landing_color_statistics = lab_statistics(references)
        if action == "sleep":
            frames[-1] = frames[-2].copy()
        if len(frames) != 8:
            raise ValueError(f"{filename}: expected 8 frames")
        paths = []
        component_limit = 2 if action in {"pet", "eat", "pickedUp"} else 1
        for index, frame in enumerate(frames):
            name = f"{action}-{index}.png"
            if action == "pickupStart":
                result = normalize_grab_frame(frame, PICKUP_SOURCE_ANCHOR_X[index])
            elif action == "pickedUp":
                result = normalize_grab_frame(frame, 0.50)
            elif action == "landing" and index < 2:
                reference = Image.open(frames_dir / "pickedUp-7.png").convert("RGBA")
                result = normalize_to_reference_bbox(
                    frame,
                    reference.getchannel("A").getbbox(),
                    y_offset=index * 30,
                )
            elif action == "landing":
                result = normalize_frame(frame, component_limit=1)
                bbox = result.getchannel("A").getbbox()
                result = scale_subject_from_baseline(result, 409 / (bbox[2] - bbox[0]))
            elif action == "pet" and frame.size == CANVAS:
                result = clean_isolated_pixels(frame, component_limit=1)
            else:
                result = normalize_frame(frame, component_limit)
                if action == "walk":
                    result = scale_subject_from_baseline(result, 1.10)
            if action == "landing":
                result = match_lab_color(result, landing_color_statistics)
            result.save(frames_dir / name, optimize=True)
            paths.append(f"frames/{name}")
        copied_source = copied_source_dir / filename
        if source_path.resolve() != copied_source.resolve():
            shutil.copy2(source_path, copied_source)
        actions[action] = {"frames": paths, "frameMs": frame_ms, "loop": loop}

    add_derived_action(
        actions,
        frames_dir,
        "sleepBreathing",
        breathing_frames(Image.open(frames_dir / "sleep-7.png").convert("RGBA")),
        180,
        True,
    )
    add_derived_action(
        actions,
        frames_dir,
        "sitTail",
        tail_wag_frames(Image.open(frames_dir / "sit-7.png").convert("RGBA")),
        140,
        True,
    )
    actions["sitReturn"] = {
        "frames": list(reversed(actions["sit"]["frames"])),
        "frameMs": actions["sit"]["frameMs"],
        "loop": False,
    }
    actions["sleepReturn"] = {
        "frames": list(reversed(actions["sleep"]["frames"])),
        "frameMs": actions["sleep"]["frameMs"],
        "loop": False,
    }
    actions["edgeReturn"] = {
        "frames": actions["landing"]["frames"],
        "frameMs": actions["landing"]["frameMs"],
        "loop": False,
    }

    actions["eat"]["props"] = ["bowl"]
    for action in ("pickupStart", "pickedUp"):
        actions[action]["interaction"] = "drag"
        actions[action]["dragAnchor"] = {"x": GRAB_ANCHOR[0], "y": GRAB_ANCHOR[1]}
    manifest = {
        "schemaVersion": 1,
        "id": "orange-tabby-v1",
        "displayName": "Orange Tabby",
        "canvas": {
            "width": CANVAS[0],
            "height": CANVAS[1],
            "anchorX": 0.5,
            "anchorY": 1.0,
            "displayScale": 0.5,
        },
        "actions": actions,
    }
    (output_dir / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
