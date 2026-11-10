"""Verify exported zombie packets preserve the native packet frame and paper."""
import json
from pathlib import Path
import unittest
from PIL import Image, ImageChops


class ZombiePacketChecks(unittest.TestCase):
    def test_every_registered_packet_has_native_geometry_and_background(self):
        assets = Path(__file__).resolve().parents[1] / "assets/debug-cards"
        background = Image.open(assets / "packet-background.png").convert("RGBA")
        atlas = Image.open(assets / "registered-zombie-packets.png").convert("RGBA")
        entries = json.loads((assets / "registered-zombie-packets.json").read_text())
        self.assertEqual(len(entries), 20)
        self.assertEqual(len({entry["Resource"] for entry in entries}), 20)
        for entry in entries:
            with self.subTest(resource=entry["Resource"]):
                self.assertEqual((entry["Width"], entry["Height"]), (128, 176))
                x, y = entry["X"], atlas.height - entry["Y"] - entry["Height"]
                card = atlas.crop((x, y, x + 128, y + 176))
                for bounds in ((0, 0, 128, 25), (0, 148, 128, 176), (0, 25, 12, 148), (116, 25, 128, 148)):
                    self.assertIsNone(ImageChops.difference(card.crop(bounds), background.crop(bounds)).getbbox(alpha_only=False))
                self.assertIsNotNone(ImageChops.difference(card, background).getbbox(alpha_only=False))


if __name__ == "__main__":
    unittest.main()
