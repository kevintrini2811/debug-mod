"""Export gray native chooser decorations and reuse the native roster brain.

Only decoration sprites change; portraits, controls and original bundles remain intact.
Run with the existing .tools/fusion-workflow Python environment.
"""
import hashlib
import json
from pathlib import Path
import UnityPy
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
GAME = Path("/mnt/FastGaming1/SteamLibrary/steamapps/common/PVZ Replanted")
BUNDLES = GAME / "Replanted_Data/StreamingAssets/aa/StandaloneWindows64"
NAMES = {
    "SPR_MainHud_SeedchooserBackground", "SPR_MainHud_SeedBank_Background",
    "SPR_MainHud_SeedBank_SunAmount", "SPR_MainHud_Seedchooser_CostBackground",
    "SPR_MainHud_Seedchooser_CostBackground_Discount",
    "SPR_BasicButton02_Background", "SPR_BasicButton03_Background",
}


def build():
    paths = sorted(set(BUNDLES.glob("uiatlas*.bundle"))
                   | set(BUNDLES.glob("defaultlocalgroup_assets_ui/sprites*.bundle")))
    environment = UnityPy.load(*map(str, paths))
    output = ROOT / "assets/debug-cards"
    brain_path = output / "brain-8396306499740389251.png"
    brain = Image.open(brain_path).convert("RGBA")
    brain = brain.crop(brain.getbbox())
    found = set()
    for obj in environment.objects:
        if obj.type.name != "Sprite":
            continue
        data = obj.read()
        if data.m_Name not in NAMES:
            continue
        image = data.image.convert("RGBA")
        # Match DebugCardArt's existing zombie roster hue, preserving brightness.
        image.putdata([(round(max(r, g, b) * .88), round(max(r, g, b) * .82),
                        max(r, g, b), a) for r, g, b, a in image.getdata()])
        if data.m_Name == "SPR_MainHud_SeedBank_SunAmount":
            # Keep the native circular rim and counter. Replace only its sun face.
            ImageDraw.Draw(image).ellipse((79, 58, 205, 184), fill=(74, 69, 84, 255))
            icon = brain.copy(); icon.thumbnail((126, 116), Image.Resampling.LANCZOS)
            image.alpha_composite(icon, (142 - icon.width // 2, 122 - icon.height // 2))
        elif "CostBackground" in data.m_Name:
            # Native sun extends outside the cost paper. Restore that paper
            # before placing the brain; keep text in the native label above it.
            paper = image.crop((8, 12, 100, 44)).resize((59, 43), Image.Resampling.LANCZOS)
            ImageDraw.Draw(image).rectangle((116, 0, 174, 56), fill=(0, 0, 0, 0))
            image.alpha_composite(paper, (116, 8))
            icon = brain.copy(); icon.thumbnail((51, 51), Image.Resampling.LANCZOS)
            image.alpha_composite(icon, (145 - icon.width // 2, 28 - icon.height // 2))
        image.save(output / ("zombie-chooser-" + data.m_Name + ".png"))
        found.add(data.m_Name)
    if found != NAMES:
        raise ValueError(f"Missing native chooser sprites: {NAMES - found}")
    proof = {"bundles": {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths},
             "brain": hashlib.sha256(brain_path.read_bytes()).hexdigest(),
             "outputs": {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                         for path in sorted(output.glob("zombie-chooser-*.png"))}}
    destination = ROOT / "references/spreading-21-0-0/chooser-art.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(proof, indent=2) + "\n")
    print(f"Exported {len(found)} zombie chooser decorations.")


if __name__ == "__main__":
    build()
