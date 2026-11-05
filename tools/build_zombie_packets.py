"""Build debug seed packets from transparent portraits, never almanac frames."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
FUSION = ROOT.parent / "fusion-mod"


def build():
    output = ROOT / "assets/debug-cards"
    inputs = {}

    def read(path):
        inputs[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
        return Image.open(path).convert("RGBA")

    packet = read(output / "packet-background.png")
    atlas = read(output / "zombie-packets.png")
    index = output / "zombie-packets.json"
    inputs[str(index)] = hashlib.sha256(index.read_bytes()).hexdigest()
    portraits = {}
    for entry in json.loads(index.read_text()):
        if entry["Role"] != "Portrait":
            continue
        x, y = entry["X"], atlas.height - entry["Y"] - entry["Height"]
        portraits[str(entry["Type"])] = atlas.crop((x, y, x + entry["Width"], y + entry["Height"]))
    portraits["eyes"] = read(FUSION / "assets/debug-cards/eyes-zombie.png")
    portraits["hypno"] = read(FUSION / "assets/hypno-zombie.png")
    duck = read(ROOT.parent / "zombie-library/assets/duck.png")
    # Include every exported native portrait, including all additional pool variants.
    keys = sorted(portraits, key=lambda key: (not key.isdigit(), int(key) if key.isdigit() else key))
    entries = []
    result = Image.new("RGBA", (128 * len(keys), 176 * 2))
    for column, key in enumerate(keys):
        for swimming in (False, True):
            art = portraits[key].copy()
            art = art.crop(art.getbbox())
            if swimming:
                # Keep the same upper-body and tube composition as the library's
                # almanac thumbnail, but start with transparent source artwork.
                art = art.crop((0, 0, art.width, round(art.height * .62)))
            art.thumbnail((104, 123), Image.Resampling.LANCZOS)
            card = packet.copy()
            card.alpha_composite(art, ((128 - art.width) // 2, 25 + (123 - art.height) // 2))
            if swimming:
                tube = duck.copy()
                tube.thumbnail((80, 48), Image.Resampling.LANCZOS)
                card.alpha_composite(tube, ((128 - tube.width) // 2, 148 - tube.height))
            row = int(swimming)
            result.alpha_composite(card, (column * 128, row * 176))
            entries.append(dict(Resource="FusionMod.ZombieAlmanac." + key + ("-duck" if swimming else "") + ".png",
                                X=column * 128, Y=(1 - row) * 176, Width=128, Height=176))
    result.save(output / "registered-zombie-packets.png")
    (output / "registered-zombie-packets.json").write_text(json.dumps(entries, indent=2) + "\n")
    proof = ROOT / "references/puff-duo-21-1-0/packets.json"
    proof.parent.mkdir(parents=True, exist_ok=True)
    proof.write_text(json.dumps(dict(inputs=inputs, outputs={path.name: hashlib.sha256(path.read_bytes()).hexdigest()
        for path in output.glob("registered-zombie-packets.*")}), indent=2) + "\n")
    print(f"Built {len(entries)} registered zombie seed packets with one shared background.")


if __name__ == "__main__":
    build()
