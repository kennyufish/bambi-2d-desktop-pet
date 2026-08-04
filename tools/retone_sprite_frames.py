import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


def load_rgba(path):
    with Image.open(path) as image:
        if image.mode != "RGBA":
            raise AssertionError(f"{path}: expected RGBA, got {image.mode}")
        return np.array(image, dtype=np.uint8)


def alpha_sha256(rgba):
    return hashlib.sha256(rgba[:, :, 3].tobytes()).hexdigest()


def lab_image(rgba):
    return cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2LAB).astype(np.float32)


def rgb_pixels(images, opaque=False):
    values = []
    for image in images:
        mask = image[:, :, 3] == 255 if opaque else image[:, :, 3] > 0
        values.append(image[:, :, :3][mask])
    return np.concatenate(values, axis=0)


def lab_pixels(images, opaque=False):
    values = []
    for image in images:
        mask = image[:, :, 3] == 255 if opaque else image[:, :, 3] > 0
        values.append(lab_image(image)[mask])
    return np.concatenate(values, axis=0)


def summarize_images(images, opaque=False):
    rgb = rgb_pixels(images, opaque=opaque)
    lab = lab_pixels(images, opaque=opaque)
    return {
        "pixels": int(len(rgb)),
        "rgbMean": np.round(rgb.mean(axis=0), 6).tolist(),
        "rgbStd": np.round(rgb.std(axis=0), 6).tolist(),
        "labMean": np.round(lab.mean(axis=0), 6).tolist(),
        "labStd": np.round(lab.std(axis=0), 6).tolist(),
    }


def apply_lab_mean_shift(rgba, delta):
    mask = rgba[:, :, 3] > 0
    adjusted_lab = np.rint(np.clip(lab_image(rgba) + delta, 0, 255)).astype(np.uint8)
    adjusted_rgb = cv2.cvtColor(adjusted_lab, cv2.COLOR_LAB2RGB)
    result = rgba.copy()
    result[:, :, :3][mask] = adjusted_rgb[mask]
    return result


def frame_paths(pack_dir, manifest, action):
    return [pack_dir / relative for relative in manifest["actions"][action]["frames"]]


def load_action_images(pack_dir, manifest, action):
    paths = frame_paths(pack_dir, manifest, action)
    return paths, [load_rgba(path) for path in paths]


def rounded(values):
    return [round(float(value), 6) for value in values]


def main():
    parser = argparse.ArgumentParser(
        description="Apply one deterministic Lab mean shift to grooming frames."
    )
    parser.add_argument("pack_dir", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()

    pack_dir = args.pack_dir
    manifest = json.loads((pack_dir / "manifest.json").read_text(encoding="utf-8"))
    actions = {action: load_action_images(pack_dir, manifest, action) for action in (
        "walk", "groom", "groomLoop"
    )}
    walk_images = actions["walk"][1]
    source_images = actions["groom"][1] + actions["groomLoop"][1]
    target_mean = lab_pixels(walk_images).mean(axis=0)
    source_mean = lab_pixels(source_images).mean(axis=0)
    delta = target_mean - source_mean

    output_images = {}
    for action, (paths, images) in actions.items():
        if action == "walk":
            output_images[action] = images
        else:
            output_images[action] = [apply_lab_mean_shift(image, delta) for image in images]

    alpha_checks = []
    for action in ("groom", "groomLoop"):
        for path, before, after in zip(
            actions[action][0], actions[action][1], output_images[action]
        ):
            if before.shape != after.shape:
                raise AssertionError(f"{path}: color correction changed image shape")
            if not np.array_equal(before[:, :, 3], after[:, :, 3]):
                raise AssertionError(f"{path}: color correction changed alpha")
            transparent = before[:, :, 3] == 0
            if not np.array_equal(before[:, :, :3][transparent], after[:, :, :3][transparent]):
                raise AssertionError(f"{path}: color correction changed hidden RGB")
            alpha_checks.append({
                "file": str(path),
                "before": alpha_sha256(before),
                "after": alpha_sha256(after),
                "unchanged": True,
            })

    if not args.dry_run:
        for action in ("groom", "groomLoop"):
            for path, before, image in zip(
                actions[action][0], actions[action][1], output_images[action]
            ):
                Image.fromarray(image).save(path, optimize=True)
                saved = load_rgba(path)
                transparent = before[:, :, 3] == 0
                if (
                    saved.shape != image.shape
                    or not np.array_equal(saved[:, :, 3], before[:, :, 3])
                    or not np.array_equal(saved[:, :, :3][transparent], before[:, :, :3][transparent])
                ):
                    raise AssertionError(f"{path}: saved image changed size or alpha")

    report = {
        "method": "uniform Lab mean shift",
        "labEncoding": "OpenCV 8-bit Lab",
        "mask": "alpha > 0",
        "referenceAction": "walk",
        "sourceActions": ["groom", "groomLoop"],
        "dryRun": args.dry_run,
        "delta": rounded(delta),
        "before": {
            action: {
                "visible": summarize_images(images),
                "opaque": summarize_images(images, opaque=True),
            }
            for action, (_, images) in actions.items()
        },
        "after": {
            action: {
                "visible": summarize_images(images),
                "opaque": summarize_images(images, opaque=True),
            }
            for action, images in output_images.items()
        },
        "alphaChecks": alpha_checks,
    }
    for action in ("groom", "groomLoop"):
        report["after"][action]["visible"]["labMeanDeltaToWalk"] = rounded(
            np.array(report["after"][action]["visible"]["labMean"])
            - np.array(report["after"]["walk"]["visible"]["labMean"])
        )
        report["after"][action]["opaque"]["labMeanDeltaToWalk"] = rounded(
            np.array(report["after"][action]["opaque"]["labMean"])
            - np.array(report["after"]["walk"]["opaque"]["labMean"])
        )

    rendered = json.dumps(report, ensure_ascii=False, indent=2)
    if args.report:
        args.report.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)


if __name__ == "__main__":
    main()
