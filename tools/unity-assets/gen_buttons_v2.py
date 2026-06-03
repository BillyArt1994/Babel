#!/usr/bin/env python3
"""
生成与概念图风格一致的干净按钮（无文字，有真实绿色边框质感）
"""
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")

buttons = [
    {
        "file": OUT / "btn_start.png",
        "prompt": (
            "A single game UI button, no text, no characters, no background scene. "
            "Just the button shape isolated. "
            "Style: matches Clash of Clans art style, high quality painted illustration. "
            "Shape: wide rounded rectangle, landscape orientation 8:1 ratio, very wide and short. "
            "The button face: bright lime green gradient from top-left to bottom-right. "
            "The top edge has a bright white/yellow highlight strip giving a 3D raised look. "
            "The outer border: thick dark green outline with golden rim highlight. "
            "The bottom edge: slightly darker green for depth. "
            "Center area of button is a clean flat lime green — NO text, NO icons. "
            "Small subtle circular shine highlight in upper-left area. "
            "The button looks pressable, satisfying, and polished. "
            "Background: pure solid white (#FFFFFF) — this is a UI sprite asset."
        )
    },
    {
        "file": OUT / "btn_exit.png",
        "prompt": (
            "A single game UI button, no text, no characters, no background scene. "
            "Just the button shape isolated on pure white background. "
            "Style: secondary/cancel button, same art style as Clash of Clans. "
            "Shape: wide rounded rectangle, landscape orientation 6:1 ratio. "
            "Color: dark stone grey gradient, matte finish. "
            "Border: medium dark grey/charcoal outline. "
            "Subtle 3D raised effect, less shiny than the primary button. "
            "Center: clean flat grey — NO text, NO icons. "
            "Background: pure solid white (#FFFFFF)."
        )
    },
]

for item in buttons:
    print(f"Generating {item['file'].name}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=item["prompt"],
        size="1536x1024",
        n=1,
    )
    data = response.data[0]
    if hasattr(data, "b64_json") and data.b64_json:
        item["file"].write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, item["file"])
    print(f"  Saved: {item['file']}")

print("Done.")
