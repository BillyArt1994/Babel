#!/usr/bin/env python3
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

prompt = (
    "Mobile game screenshot, portrait 9:16 vertical layout, side-scrolling 2D view. "
    "Game called Babel - a god defense game where humans try to build a tower. "

    "LAYOUT (very important): "
    "The upper 65% of the screen is open sky - dramatic dark crimson and stormy sky with god-rays of golden light breaking through. "
    "The lower 35% of the screen is the ground/battlefield strip. "

    "GROUND SECTION (bottom of screen): "
    "On the far left of the ground strip: a partially-built ancient stone tower/ziggurat structure, "
    "workers and scaffolding around its base, glowing construction progress indicators on it. "
    "From the right side, a crowd of small human figures march left along the ground toward the tower - "
    "medieval peasants, workers carrying tools and stones, some in robes. "
    "They are at ground level, side view, marching horizontally. "
    "Green health bars floating above each enemy. "
    "Some enemies near front are being hit by golden divine lightning from above. "

    "SKY SECTION (upper portion of screen): "
    "4 circular skill button icons floating in the sky area, styled as glowing stone medallions with runes: "
    "arranged loosely - two on left side, two on right side at different heights. "
    "Each has a circular progress/cooldown arc border and a glyph icon inside. "
    "One is highlighted/active with golden glow. "

    "HUD: "
    "Top center: small timer bar '08:45'. "
    "Top left: pause button. "
    "No large divine hand - the attack comes as a golden lightning bolt from the sky hitting enemies. "

    "Art style: stylized painterly 2D illustration, warm golden tones vs dark storm sky, "
    "clean mobile game aesthetic similar to classic side-scrolling tower defense games. "
    "The horizon line separating sky and ground should be clearly visible at about 65% from top."
)

print("Generating gameplay v2 (side-scroll layout)...")
response = client.images.generate(
    model="gpt-image-2",
    prompt=prompt,
    size="1024x1536",
    n=1,
)
data = response.data[0]
out = Path("H:/Babel/concept_art/01_gameplay_v2.png")
if hasattr(data, "b64_json") and data.b64_json:
    out.write_bytes(base64.b64decode(data.b64_json))
elif hasattr(data, "url") and data.url:
    import urllib.request
    urllib.request.urlretrieve(data.url, out)
print(f"Saved: {out}")
