"""Render every variant/card/language of the description images to PNG with headless Edge.

Usage: python -I render.py [variant ...]   (default: b_fieldguide; also a_meadow, c_storybook)
Output: Workshop/images/<variant>/<card>_<lang>.png at 2x (1280 px wide), trimmed to content.
"""
import os
import re
import subprocess
import sys
import tempfile

from PIL import Image

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "images")
VARIANTS = sys.argv[1:] or ["b_fieldguide"]
LANGS = ["en", "ru"]
BG = (0x1b, 0x28, 0x38)


def trim_bottom(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    px = im.load()
    bottom = h
    while bottom > 1 and all(px[x, bottom - 1] == BG for x in range(0, w, 7)):
        bottom -= 1
    im.crop((0, 0, w, min(h, bottom + 16))).save(path, optimize=True)


for variant in VARIANTS:
    os.makedirs(os.path.join(OUT, variant), exist_ok=True)
    url = "file:///" + os.path.join(HERE, variant + ".html").replace("\\", "/")
    html = open(os.path.join(HERE, variant + ".html"), encoding="utf-8").read()
    cards = re.findall(r'data-card="(\w+)"', html)
    for card in cards:
        for lang in LANGS:
            target = os.path.join(OUT, variant, f"{card}_{lang}.png")
            with tempfile.TemporaryDirectory() as profile:
                subprocess.run([
                    EDGE, "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    f"--user-data-dir={profile}", "--force-device-scale-factor=2",
                    "--window-size=640,1100", "--virtual-time-budget=6000",
                    "--allow-file-access-from-files", f"--screenshot={target}",
                    f"{url}#{card}/{lang}",
                ], check=True, capture_output=True, timeout=120)
            trim_bottom(target)
            print("rendered", os.path.relpath(target, OUT))
