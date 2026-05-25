#!/usr/bin/env python3
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(api_key=os.environ["OPENAI_API_KEY"], base_url="https://chat-test.q1.com/v1")
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")

assets = [
    {
        "file": "logo_babel.png",
        "size": "1024x1536",
        "prompt": (
            "Game logo artwork, portrait canvas. "
            "Large bold game title text 'BABEL' centered in the image. "
            "Style: 3D stone-carved letters with warm golden/amber gradient and cracked texture. "
            "Golden crown on top of the B letter. "
            "Strong drop shadow and 3D depth effect. Small lightning sparks on letter edges. "
            "CRITICAL: The background must be completely empty/transparent. "
            "Only the logo text occupies the lower-center portion of the canvas. "
            "PNG with alpha channel. No sky, no ground, no characters, no decorations outside the letters."
        )
    },
    {
        "file": "btn_green.png",
        "size": "1536x1024",
        "prompt": (
            "Mobile game UI button sprite on transparent background, PNG with alpha. "
            "Wide rounded rectangle shape, landscape 3:1 ratio. "
            "Bright lime-to-dark-green vertical gradient fill. "
            "Top edge: thin bright white highlight strip (3D raised look). "
            "Outer border: thick golden/amber outline, rounded corners. "
            "Bottom edge: darker green shadow for depth. "
            "Interior: completely clean flat green, NO text, NO icons, NO writing of any kind. "
            "Border is uniform on left/right sides (9-slice compatible). "
            "The entire outside of the button shape is transparent alpha. "
            "Clash of Clans quality. Juicy, shiny, pressable."
        )
    },
    {
        "file": "btn_grey.png",
        "size": "1536x1024",
        "prompt": (
            "Mobile game UI secondary button sprite on transparent background, PNG with alpha. "
            "Wide rounded rectangle shape, landscape 3:1 ratio, same shape as a primary button. "
            "Dark stone-grey gradient fill, matte texture. "
            "Outer border: dark charcoal grey outline, rounded corners. "
            "Subtle 3D raised effect. "
            "Interior: completely clean flat grey, NO text, NO icons, NO writing of any kind. "
            "Border is uniform on left/right sides (9-slice compatible). "
            "The entire outside of the button shape is transparent alpha. "
            "Secondary/cancel button style, less prominent than a green button."
        )
    },
]

for asset in assets:
    name = asset["file"]
    print(f"Generating {name}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=asset["prompt"],
        size=asset["size"],
        n=1,
    )
    data = response.data[0]
    out_path = OUT / name
    if hasattr(data, "b64_json") and data.b64_json:
        out_path.write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, out_path)
    print(f"  Saved: {out_path}")
print("Done.")
