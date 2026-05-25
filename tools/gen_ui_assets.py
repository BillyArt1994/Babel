#!/usr/bin/env python3
"""
生成主菜单标准 UI 资产（每个独立生成，透明背景）
"""
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
OUT.mkdir(parents=True, exist_ok=True)

assets = [
    # ── 背景：纯场景，无任何 UI 元素 ──
    {
        "file": "bg_mainmenu.png",
        "size": "1024x1536",
        "prompt": (
            "A vertical portrait mobile game background illustration, 9:16 ratio. "
            "Q-style cute cartoon art, thick outlines, vibrant colors. "
            "Scene: bright blue sky with white fluffy clouds and warm golden god-rays from above. "
            "Middle ground: a large ancient Tower of Babel under construction at center, "
            "warm sandstone color, multiple tiers, scaffolding visible, partially built. "
            "Foreground: lush green grassy ground with cobblestone path, wildflowers on sides. "
            "Left foreground: 2 tiny chibi human workers in brown outfits, Metal Slug style — "
            "enormous heads, tiny legs, exaggerated scared/determined expressions. "
            "Right foreground: 1 chibi worker with a big grin, holding a flag. "
            "Top-left corner: small cute chibi angel with halo and lightning bolt. "
            "IMPORTANT: NO buttons, NO logo, NO text, NO UI elements anywhere. "
            "Pure illustrated game background only. "
            "Rich detailed painterly illustration like Clash of Clans quality."
        )
    },
    # ── Logo：仅 BABEL 文字，透明背景 ──
    {
        "file": "logo_babel.png",
        "size": "1024x1536",
        "prompt": (
            "Game logo text 'BABEL' on a completely transparent background. "
            "Style: massive bold 3D stone-carved letters. "
            "Each letter: warm golden/amber gradient, inner cracked stone texture, "
            "deep drop shadow giving strong 3D pop effect. "
            "A golden crown sits on top of the letter B. "
            "Small lightning spark effects on edges of letters. "
            "The logo should be centered, taking up most of the image. "
            "CRITICAL: Pure transparent background (PNG alpha), no sky, no scene, no ground. "
            "Only the logo text itself with transparency around it."
        )
    },
    # ── 绿色按钮：无文字，9-slice 友好 ──
    {
        "file": "btn_green.png",
        "size": "1536x1024",
        "prompt": (
            "A single mobile game UI button asset on transparent background. "
            "Shape: wide rounded rectangle, landscape orientation. "
            "Color: bright lime-to-green vertical gradient. "
            "Top edge: thin white highlight strip for 3D raised look. "
            "Border: thick golden/amber outline, beveled corners. "
            "Bottom edge: darker green shadow for depth. "
            "Small subtle shine highlight in upper-left area. "
            "The button interior is a clean flat green — ABSOLUTELY NO TEXT, NO ICONS, NO LABELS. "
            "Left and right thirds have identical border — center area is plain flat green. "
            "This must be suitable as a 9-slice sprite (uniform borders on all sides). "
            "CRITICAL: Transparent PNG background, only the button shape itself. "
            "Clash of Clans quality, juicy and pressable looking."
        )
    },
    # ── 灰色按钮：无文字，9-slice 友好 ──
    {
        "file": "btn_grey.png",
        "size": "1536x1024",
        "prompt": (
            "A single mobile game UI button asset on transparent background. "
            "Shape: wide rounded rectangle, landscape orientation, same proportions as a primary button. "
            "Color: dark grey-blue stone texture gradient, matte finish. "
            "Border: medium dark grey/charcoal outline with slight bevel. "
            "Subtle 3D raised effect, less shiny than a primary button. "
            "The button interior is a clean flat grey — ABSOLUTELY NO TEXT, NO ICONS, NO LABELS. "
            "Left and right thirds have identical border — center area is plain flat grey. "
            "This must be suitable as a 9-slice sprite (uniform borders on all sides). "
            "CRITICAL: Transparent PNG background, only the button shape itself. "
            "Secondary/cancel button style, Clash of Clans art quality."
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

print("\nAll assets generated.")
