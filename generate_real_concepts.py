#!/usr/bin/env python3
"""
Generate accurate Babel game concept art based on real scene layout:
- Tower CENTERED on screen, pyramid shape (4 tiers, wider at bottom)
- Enemies from BOTH LEFT and RIGHT sides converging on center
- Portrait 9:16, sky takes upper 60%, ground strip at bottom
- Q-style cute cartoon art
"""
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

Path("H:/Babel/concept_art").mkdir(parents=True, exist_ok=True)

items = [
    {
        "file": "H:/Babel/concept_art/real_gameplay_01.png",
        "prompt": (
            "Q-style cute chibi cartoon 2D mobile game screenshot, portrait 9:16 vertical. "
            "Bright colorful flat illustration, thick black outlines, vibrant saturated colors, kawaii style. "

            "CRITICAL LAYOUT (must follow exactly): "
            "Upper 60% of screen: open bright blue sky with fluffy white clouds and warm sun glow. "
            "Lower 40%: flat sandy/grassy ground strip. "
            "Horizon line at 60% from top. "

            "TOWER (exact center of screen, bottom): "
            "A chibi Tower of Babel pyramid at the HORIZONTAL CENTER of screen. "
            "4 stacked platform tiers, each tier wider at bottom than the one above - pyramid silhouette. "
            "Bottom tier: widest, spans most of screen width. "
            "2nd tier: slightly narrower. "
            "3rd tier: narrower still. "
            "Top tier: smallest, single block. "
            "Each tier is a chunky stone block platform. "
            "Some blocks glow RED (completed build points), some are GREY (incomplete). "
            "Small cute ladder on the side connecting tiers. "
            "The whole tower sits on the ground, centered horizontally. "

            "ENEMIES (key: from BOTH SIDES converging to center): "
            "Left side: 5-6 tiny cute chibi human workers marching RIGHT toward the tower. "
            "Right side: 5-6 tiny cute chibi human workers marching LEFT toward the tower. "
            "Both groups are on the ground strip, level with the tower base. "
            "Workers are round-headed chibi figures carrying tiny stone blocks. "
            "Each has a small green health bar above their head. "
            "Some workers near the tower are already climbing the ladder or building. "

            "ATTACK EFFECT: "
            "Golden divine lightning bolt from the sky hitting one worker, small star burst sparkle effect. "

            "HUD (clean flat UI): "
            "Top center: rounded rectangle pill timer '12:34' white text on dark bg. "
            "Top left: small badge 'LV:3'. "
            "Just below top: thin blue EXP progress bar spanning width. "
            "Top right: small speed button '2x'. "
            "No skill icons in sky - clean open sky. "

            "Style: bright cheerful kawaii cartoon, similar to popular casual mobile games. "
            "Clean readable UI. No realistic elements whatsoever. Pure Q-style cartoon."
        )
    },
    {
        "file": "H:/Babel/concept_art/real_gameplay_02_upgrade.png",
        "prompt": (
            "Q-style cute chibi cartoon 2D mobile game screenshot, portrait 9:16 vertical. "
            "Upgrade skill selection screen. "

            "Background (darkened/blurred): "
            "The same gameplay scene - centered pyramid tower, chibi workers from both sides frozen mid-march, blue sky. "
            "Covered by semi-transparent dark purple overlay. "

            "Foreground center panel: "
            "Bright rounded card panel with bubbly cartoon border. "
            "Header: 'LEVEL UP!' in big golden cartoon letters with sparkles and star effects. "
            "Subtext: 'LV 2 → LV 3' in smaller text. "

            "3 skill cards side by side, each is a vertical rounded card: "
            "Card 1 (golden glow, highlighted): "
            "  Top: chibi glowing golden pointing finger icon on dark bg. "
            "  Blue pill badge: 'ACTIVE'. "
            "  Name: 'Divine Finger' in bold. "
            "  Description: small text '+40% dmg, 0.8s cooldown'. "
            "Card 2 (normal): "
            "  Top: chibi purple explosion shockwave icon. "
            "  Purple pill badge: 'PASSIVE'. "
            "  Name: 'Aftershock'. "
            "  Description: 'AOE 30% dmg after hit'. "
            "Card 3 (normal): "
            "  Top: chibi golden clock with lightning bolt. "
            "  Orange pill badge: 'AUTO'. "
            "  Name: 'Holy Timer'. "
            "  Description: 'Auto strike every 3s'. "

            "Cards have soft drop shadows, cute cartoon icons. "
            "Bottom: current equipped skills shown as tiny icons row. "
            "Overall: clean cute mobile game UI, kawaii cartoon style."
        )
    },
    {
        "file": "H:/Babel/concept_art/real_gameplay_03_gameover.png",
        "prompt": (
            "Q-style cute chibi cartoon 2D mobile game defeat screen, portrait 9:16 vertical. "

            "Background: the pyramid tower is now FULLY BUILT and very tall, "
            "reaching into storm clouds. Tiny chibi humans celebrating at base, "
            "dark dramatic sky but still cartoon/chibi style. "

            "Center panel (defeat results): "
            "Cracked stone-style header 'BABEL COMPLETE' with dark red glow effect. "
            "Sad cloud icon above it. "
            "Italic subtitle: 'The tower reaches the heavens...' "

            "Stats table with cute icons: "
            "⏱ Survival Time: 08:42 "
            "💀 Enemies Smited: 347 "
            "⬆ Max Level: 7 "
            "✨ Skills Used: 4 "

            "Two buttons below: "
            "Big gold rounded button 'TRY AGAIN' with sparkle. "
            "Smaller grey button 'MAIN MENU'. "

            "Kawaii cartoon style throughout, thick outlines, bright colors even in defeat. "
            "Chibi proportions for all elements."
        )
    },
]

for item in items:
    name = Path(item["file"]).name
    print(f"Generating {name}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=item["prompt"],
        size="1024x1536",
        n=1,
    )
    data = response.data[0]
    out = Path(item["file"])
    if hasattr(data, "b64_json") and data.b64_json:
        out.write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, out)
    print(f"  Saved: {out}")

print("Done.")
