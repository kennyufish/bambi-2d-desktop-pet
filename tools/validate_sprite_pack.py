import json
import sys
from pathlib import Path

from PIL import Image, ImageChops


def main():
    pack = Path(sys.argv[1])
    manifest = json.loads((pack / "manifest.json").read_text(encoding="utf-8"))
    required = {
        "idle", "walk", "sit", "sitTail", "sitReturn", "lieDown", "sleep",
        "sleepBreathing", "sleepReturn", "pet", "eat", "pickupStart", "pickedUp",
        "landing", "edgeReturn", "restCurled", "restCurledLoop", "restCurledReturn",
        "restLoaf", "restLoafLoop", "restLoafReturn", "restFaceDown",
        "restFaceDownLoop", "restFaceDownReturn", "groom", "groomLoop", "groomReturn",
    }
    assert set(manifest["actions"]) == required
    assert manifest["actions"]["eat"].get("props") == ["bowl"]
    for action in ("pickupStart", "pickedUp"):
        assert manifest["actions"][action].get("interaction") == "drag"
        assert manifest["actions"][action].get("dragAnchor") == {"x": 320, "y": 88}
    expected_size = (manifest["canvas"]["width"], manifest["canvas"]["height"])
    for action, config in manifest["actions"].items():
        expected_frames = 16 if action in {"sit", "sitReturn"} else 8
        assert len(config["frames"]) == expected_frames, f"{action}: expected {expected_frames} frames"
        for relative in config["frames"]:
            image = Image.open(pack / relative).convert("RGBA")
            assert image.size == expected_size, f"{relative}: wrong canvas"
            alpha = image.getchannel("A")
            assert alpha.getbbox(), f"{relative}: empty alpha"
            for point in ((0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1)):
                assert alpha.getpixel(point) == 0, f"{relative}: opaque corner"
            coverage = sum(1 for value in alpha.getdata() if value > 16) / (image.width * image.height)
            assert 0.03 < coverage < 0.72, f"{relative}: implausible foreground coverage {coverage:.3f}"
    sit = manifest["actions"]["sit"]
    assert sit["frameMs"] == 60
    final_sit = Image.open(pack / sit["frames"][-1]).convert("RGBA")
    initial_tail = Image.open(pack / manifest["actions"]["sitTail"]["frames"][0]).convert("RGBA")
    assert not ImageChops.difference(final_sit, initial_tail).getbbox(), "sit: final frame must match sitTail"
    assert manifest["actions"]["sitReturn"]["frames"] == list(reversed(sit["frames"]))
    assert manifest["actions"]["sitReturn"]["frameMs"] == sit["frameMs"]
    print(f"SPRITE_PACK_PASS actions={len(required)}")


if __name__ == "__main__":
    main()
