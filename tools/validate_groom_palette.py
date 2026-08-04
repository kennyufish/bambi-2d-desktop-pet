import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from retone_sprite_frames import alpha_sha256, lab_pixels, load_rgba, summarize_images


EXPECTED_ALPHA_SHA256 = {
    "groom-0.png": "d637377ee7884a84db8d029b4b1069199974d9d689b65028f2de5dee7f824cce",
    "groom-1.png": "f9aa2eccf7561dbe215399803c9cdbca7a10f33fee90bff36eb96d926e2534be",
    "groom-2.png": "de0bfb482c07226f6dca4b42fe7a0ac364493866fb8c042f068ac499ed760e06",
    "groom-3.png": "7d68b5fa2598a043b20227a8cdbb79d6ac76c73c7777d415ce81385c0b4dbdc3",
    "groom-4.png": "7fd0a4f01c11a1877057062a0abc62c0307f543404bcc88a3e34f173b5ddac36",
    "groom-5.png": "4531d02241a84d420d63a118109f925626d497762e0f991d412eda4c2cc58464",
    "groom-6.png": "2928b50f49b91fbbeff5d41ea2478155e9f6fa1e4a37f68cff09c74ebc603740",
    "groom-7.png": "90e15383799da0d112896cd825cc6f98903a0b45caa9ed111d990541d9cdc7cf",
    "groomLoop-0.png": "90e15383799da0d112896cd825cc6f98903a0b45caa9ed111d990541d9cdc7cf",
    "groomLoop-1.png": "8752dc6168440f05aff997ca2be04c2063112f645e9805b4827109f3a2f8899b",
    "groomLoop-2.png": "4905ef3827d5c465acfa8d6c09b99c0d0dc09d72ea63347d738eed3ad3348b9a",
    "groomLoop-3.png": "80f45b3d1099c8ccf758653348412077342cecf62b6aa5490d742dfa08230039",
    "groomLoop-4.png": "d619216f4cc15435b192e5673fb445d9c80d147c7ae5ee23798a72779e8f9186",
    "groomLoop-5.png": "1a3516c245663342f33c9539e23613fc16934e40e2f5cac49352df0ce974d53b",
    "groomLoop-6.png": "467224d34c663dc7ec9914aa0218b5fac20e76bc38f539a8e47c8c5921682fcf",
    "groomLoop-7.png": "90e15383799da0d112896cd825cc6f98903a0b45caa9ed111d990541d9cdc7cf",
}


def mean_distance(left, right):
    return float(np.linalg.norm(np.array(left) - np.array(right)))


def main():
    parser = argparse.ArgumentParser(description="Validate the orange-tabby grooming palette and geometry.")
    parser.add_argument("pack_dir", type=Path)
    args = parser.parse_args()

    pack_dir = args.pack_dir
    manifest = json.loads((pack_dir / "manifest.json").read_text(encoding="utf-8"))
    expected_loop = [f"frames/groomLoop-{index}.png" for index in range(8)]
    loop = manifest["actions"]["groomLoop"]
    assert loop["frames"] == expected_loop, "groomLoop must use its own eight frame files"
    assert loop["frameMs"] == 140, "groomLoop frameMs must remain 140"
    assert loop["loop"] is True
    assert manifest["actions"]["groom"]["frameMs"] == 120

    images = {}
    for action in ("walk", "groom", "groomLoop"):
        paths = [pack_dir / relative for relative in manifest["actions"][action]["frames"]]
        assert len(paths) == 8, f"{action}: expected eight frames"
        images[action] = []
        for path in paths:
            with Image.open(path) as image:
                assert image.mode == "RGBA", f"{path}: expected RGBA"
                assert image.size == (520, 520), f"{path}: expected 520x520"
            rgba = load_rgba(path)
            expected_alpha = EXPECTED_ALPHA_SHA256.get(path.name)
            if expected_alpha:
                assert alpha_sha256(rgba) == expected_alpha, f"{path}: alpha changed"
            images[action].append(rgba)

    before_like = {
        action: summarize_images(action_images)
        for action, action_images in images.items()
    }
    walk_mean = before_like["walk"]["labMean"]
    source = images["groom"] + images["groomLoop"]
    combined = summarize_images(source)
    combined_delta = mean_distance(combined["labMean"], walk_mean)
    assert combined_delta <= 1.5, f"combined visible Lab mean distance too high: {combined_delta:.3f}"

    visible_distances = {}
    opaque_distances = {}
    for action in ("groom", "groomLoop"):
        visible_distances[action] = mean_distance(before_like[action]["labMean"], walk_mean)
        opaque_distances[action] = mean_distance(
            summarize_images(images[action], opaque=True)["labMean"],
            summarize_images(images["walk"], opaque=True)["labMean"],
        )
        assert visible_distances[action] <= 6.0, (
            f"{action}: visible Lab mean distance too high: {visible_distances[action]:.3f}"
        )
        assert opaque_distances[action] <= 8.0, (
            f"{action}: opaque Lab mean distance too high: {opaque_distances[action]:.3f}"
        )

    print(json.dumps({
        "status": "GROOM_PALETTE_PASS",
        "mask": "alpha > 0",
        "visibleLabMeanDistance": {
            "before": {
                "groom": round(10.7158, 4),
                "groomLoop": round(16.552, 4),
            },
            "after": {
                "groom": round(visible_distances["groom"], 4),
                "groomLoop": round(visible_distances["groomLoop"], 4),
                "combined": round(combined_delta, 4),
            },
        },
        "opaqueLabMeanDistanceAfter": {
            action: round(distance, 4) for action, distance in opaque_distances.items()
        },
        "geometry": {
            "size": "520x520",
            "alphaHashes": "unchanged",
            "framesPerAction": 8,
        },
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
