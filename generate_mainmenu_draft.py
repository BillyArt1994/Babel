#!/usr/bin/env python3
"""
Main Menu concept draft for Babel.
Based on actual code structure in UIMainMenuPanel.BuildRuntimeLayout():
- Full screen dark background
- Tower silhouette at bottom center (210x420 brown rect)
- Lightning accent (yellow stripe, rotated -18deg)
- Title: "BABEL"
- Subtitle: "阻止人类触及天庭"
- Two buttons: "开始游戏" and "退出游戏"
"""
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

prompt = """
Q-style cute kawaii cartoon mobile game MAIN MENU screen, portrait 9:16.
Game name: BABEL. Theme: you are a god trying to stop humans from building the Tower of Babel.
Thick black outlines, vibrant colors, chibi proportions throughout.

=== BACKGROUND ===
Rich illustrated scene: deep twilight purple-blue sky gradient from top to bottom.
Background has distant mountains, ancient ruins silhouettes. Stars and subtle golden clouds.
NOT black - it should be a beautiful illustrated dusk/twilight sky.

=== TOWER (bottom center, takes up bottom 45% of screen) ===
A large chibi Tower of Babel rising from the ground at screen center-bottom.
The tower is a chunky pyramid shape with multiple tiers.
It is partially built - workers visible on the scaffolding around it.
The tower glows faintly with warm amber light from the top.
Tiny cute chibi human silhouettes visible around the base.

=== LIGHTNING (decorative) ===
A bright golden lightning bolt coming from the upper right, striking down toward the tower.
Large, dramatic, slightly transparent. This represents the player's divine power.

=== TITLE AREA (upper center, above tower) ===
Game logo "BABEL" in huge bold cartoon stone letters with golden glow and crack effects.
Letters look ancient and heavy. Golden outline, slight 3D effect.
Below the title: subtitle text "阻止人类触及天庭" in smaller warm golden Chinese text.
A decorative divider line with small star/cross ornaments below the subtitle.

=== BUTTONS (center of screen, between title and tower) ===
Two large rounded rectangle buttons stacked vertically, centered:
TOP BUTTON - "开始游戏" (Start Game):
  - Larger button, golden/amber color with shiny highlight
  - Bold dark text, slight 3D raised appearance
  - Glowing golden border effect
  - Small sword/lightning icon on left side
BOTTOM BUTTON - "退出游戏" (Exit Game):
  - Slightly smaller, darker stone/grey color
  - Lighter text, less prominent than start button

=== DECORATIVE ELEMENTS ===
Small cute chibi angel/god figure floating in upper left corner with halo and wings.
Some sparkle/star particles scattered around the title.
Two small cute chibi worker characters peeking from behind the tower base.

Style: bright kawaii game UI, similar to popular casual mobile RPG/TD games.
Clean, readable, inviting. Not too dark despite the twilight theme.
All UI elements have soft drop shadows and rounded corners.
"""

print("Generating main menu draft...")
response = client.images.generate(
    model="gpt-image-2",
    prompt=prompt.strip(),
    size="1024x1536",
    n=1,
)
data = response.data[0]
out = Path("H:/Babel/concept_art/mainmenu_draft_v1.png")
if hasattr(data, "b64_json") and data.b64_json:
    out.write_bytes(base64.b64decode(data.b64_json))
elif hasattr(data, "url") and data.url:
    import urllib.request
    urllib.request.urlretrieve(data.url, out)
print(f"Saved: {out}")
