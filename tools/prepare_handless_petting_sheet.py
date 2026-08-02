import sys
from pathlib import Path

from PIL import Image

from import_transparent_8frame_pack import CANVAS, normalize_frame, split_sheet


def scale_from_bottom(frame, scale_x, scale_y):
    bbox = frame.getchannel("A").getbbox()
    if not bbox:
        raise ValueError("petting frame is empty")
    subject = frame.crop(bbox)
    subject = subject.resize(
        (round(subject.width * scale_x), round(subject.height * scale_y)),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    canvas.alpha_composite(
        subject,
        ((CANVAS[0] - subject.width) // 2, CANVAS[1] - subject.height - 8),
    )
    return canvas


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: prepare_handless_petting_sheet.py INPUT OUTPUT")
    source_frames = split_sheet(Image.open(sys.argv[1]).convert("RGBA"))
    handless = {}
    for index in (0, 1, 6, 7):
        without_hand = normalize_frame(source_frames[index], component_limit=1)
        handless[index] = normalize_frame(without_hand, component_limit=1)
    frames = [
        handless[0],
        handless[1],
        handless[6],
        scale_from_bottom(handless[6], 1.01, 0.99),
        scale_from_bottom(handless[6], 1.015, 0.975),
        scale_from_bottom(handless[6], 1.01, 0.99),
        handless[7],
        handless[0],
    ]
    sheet = Image.new("RGBA", (CANVAS[0] * 4, CANVAS[1] * 2), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        sheet.alpha_composite(frame, ((index % 4) * CANVAS[0], (index // 4) * CANVAS[1]))
    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output, optimize=True)


if __name__ == "__main__":
    main()
