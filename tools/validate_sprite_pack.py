import json
import sys
from pathlib import Path

from PIL import Image


def main():
    pack = Path(sys.argv[1])
    manifest = json.loads((pack / "manifest.json").read_text(encoding="utf-8"))
    required = {
        "idle", "walk", "lieDown", "sleep",
        "sleepBreathing", "sleepReturn", "pet", "eat", "pickupStart", "pickedUp",
        "landing", "edgeReturn", "restCurled", "restLoaf", "restFaceDown",
        "groom", "groomLoop", "groomReturn",
    }
    assert set(manifest["actions"]) == required
    assert manifest["actions"]["eat"].get("props") == ["bowl"]
    for action in ("pickupStart", "pickedUp"):
        assert manifest["actions"][action].get("interaction") == "drag"
        assert manifest["actions"][action].get("dragAnchor") == {"x": 320, "y": 88}
    expected_size = (manifest["canvas"]["width"], manifest["canvas"]["height"])
    for action, config in manifest["actions"].items():
        assert len(config["frames"]) == 8, f"{action}: expected 8 frames"
        if action == "groomLoop":
            assert config["frames"] == [f"frames/groomLoop-{index}.png" for index in range(8)]
        for relative in config["frames"]:
            image = Image.open(pack / relative).convert("RGBA")
            assert image.size == expected_size, f"{relative}: wrong canvas"
            alpha = image.getchannel("A")
            assert alpha.getbbox(), f"{relative}: empty alpha"
            for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1)):
                assert alpha.getpixel(point) == 0, f"{relative}: opaque corner"
            coverage = sum(1 for value in alpha.getdata() if value > 16) / (image.width * image.height)
            assert 0.03 < coverage < 0.72, f"{relative}: implausible foreground coverage {coverage:.3f}"
    print(f"SPRITE_PACK_PASS actions={len(required)}")


if __name__ == "__main__":
    main()
