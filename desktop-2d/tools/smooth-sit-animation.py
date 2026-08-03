"""Create a smoother sitting animation from the existing transparent key frames.

This uses classic OpenCV optical-flow warping only. It does not call an image
model and keeps the first and final key frames byte-for-byte unchanged.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

ROOT_TOOLS = Path(__file__).resolve().parents[2] / "tools"
sys.path.insert(0, str(ROOT_TOOLS))
from import_transparent_8frame_pack import normalize_frame, split_sheet  # noqa: E402

FRAME_PLAN = (
    (0, 0, 0.0),
    (0, 1, 0.5),
    (1, 1, 0.0),
    (1, 2, 1 / 3),
    (1, 2, 2 / 3),
    (2, 2, 0.0),
    (2, 3, 0.5),
    (3, 3, 0.0),
    (3, 4, 0.5),
    (4, 4, 0.0),
    (4, 5, 0.5),
    (5, 5, 0.0),
    (5, 6, 0.5),
    (6, 6, 0.0),
    (6, 7, 0.5),
    (7, 7, 0.0),
)


def load_frame(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32)


def load_key_frames(source_sheet: Path, terminal_frame: Path) -> list[np.ndarray]:
    sheet = Image.open(source_sheet).convert("RGBA")
    frames = [np.asarray(normalize_frame(frame, component_limit=1), dtype=np.float32)
              for frame in split_sheet(sheet)]
    frames[-1] = load_frame(terminal_frame)
    return frames


def grayscale(frame: np.ndarray) -> np.ndarray:
    rgb = frame[..., :3].astype(np.uint8)
    alpha = frame[..., 3] / 255.0
    return (cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY) * alpha).astype(np.uint8)


def flow_between(source: np.ndarray, target: np.ndarray) -> np.ndarray:
    return cv2.calcOpticalFlowFarneback(
        grayscale(source), grayscale(target), None,
        pyr_scale=0.5, levels=5, winsize=51, iterations=7,
        poly_n=7, poly_sigma=1.5, flags=0,
    )


def warp(frame: np.ndarray, flow: np.ndarray, amount: float) -> np.ndarray:
    height, width = frame.shape[:2]
    grid_x, grid_y = np.meshgrid(np.arange(width), np.arange(height))
    return cv2.remap(
        frame,
        (grid_x - flow[..., 0] * amount).astype(np.float32),
        (grid_y - flow[..., 1] * amount).astype(np.float32),
        interpolation=cv2.INTER_LINEAR,
        borderMode=cv2.BORDER_CONSTANT,
        borderValue=0,
    )


def morph(source: np.ndarray, target: np.ndarray, amount: float) -> np.ndarray:
    if amount <= 0:
        return source.copy()
    if amount >= 1:
        return target.copy()
    source_flow = flow_between(source, target)
    target_flow = flow_between(target, source)
    source_premultiplied = premultiply(source)
    target_premultiplied = premultiply(target)
    warped = (
        warp(source_premultiplied, source_flow, amount)
        if amount < 0.5
        else warp(target_premultiplied, target_flow, 1 - amount)
    )
    return unpremultiply(warped)


def premultiply(frame: np.ndarray) -> np.ndarray:
    result = frame.copy()
    result[..., :3] *= result[..., 3:4] / 255.0
    return result


def unpremultiply(frame: np.ndarray) -> np.ndarray:
    alpha = frame[..., 3:4]
    rgb = np.divide(
        frame[..., :3] * 255.0,
        np.maximum(alpha, 1e-4),
        out=np.zeros_like(frame[..., :3]),
        where=alpha > 1e-4,
    )
    return np.concatenate((rgb, alpha), axis=2)


def save_frame(path: Path, frame: np.ndarray) -> None:
    Image.fromarray(np.clip(np.rint(frame), 0, 255).astype(np.uint8)).save(path)


def make_contact_sheet(frames: list[np.ndarray], output: Path) -> None:
    width, height = frames[0].shape[1], frames[0].shape[0]
    sheet = Image.new("RGBA", (width * 4, height * 4), (53, 61, 70, 255))
    for index, frame in enumerate(frames):
        image = Image.fromarray(np.clip(np.rint(frame), 0, 255).astype(np.uint8))
        sheet.alpha_composite(image, ((index % 4) * width, (index // 4) * height))
    sheet.save(output)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-sheet", type=Path, required=True)
    parser.add_argument("--terminal-frame", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--contact-sheet", type=Path)
    args = parser.parse_args()

    sources = load_key_frames(args.source_sheet, args.terminal_frame)
    if len({frame.shape for frame in sources}) != 1:
        raise ValueError("All sit key frames must share one canvas size")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    frames = [morph(sources[first], sources[last], amount) for first, last, amount in FRAME_PLAN]
    for index, frame in enumerate(frames):
        save_frame(args.output_dir / f"sit-{index}.png", frame)
    if args.contact_sheet:
        args.contact_sheet.parent.mkdir(parents=True, exist_ok=True)
        make_contact_sheet(frames, args.contact_sheet)


if __name__ == "__main__":
    main()
