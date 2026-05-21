#!/usr/bin/env python3
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

items = [
    {
        "file": "H:/Babel/concept_art/gameplay_v3_main.png",
        "prompt": (
            "Q-style cute chibi cartoon 2D mobile game screenshot, portrait 9:16 vertical. "
            "Side-scrolling view. Bright colorful flat illustration style, thick outlines, vibrant colors. "

            "LAYOUT: "
            "Upper 60% of screen: bright blue sky with fluffy white clouds, slight warm glow in center (god light from above). "
            "Lower 40%: flat ground strip with brown/sandy color. "
            "Clear visible horizon line separating sky and ground at 60% from top. "

            "TOWER (left side of ground strip): "
            "A multi-level chibi Tower of Babel made of stacked stone blocks. "
            "3 visible floors, each floor is a wide platform with 3-4 small glowing circular BUILD POINT markers on it. "
            "Completed build points glow RED/ORANGE. Incomplete ones are dark grey circles. "
            "Simple wooden ladders between floors. Small cute scaffold structure around it. "
            "Tower is chunky, blocky, Q-style proportions. "

            "ENEMIES (ground strip, moving right-to-left): "
            "8-12 tiny chibi human figures marching in a line from right side toward the tower. "
            "Mix of types with different cute round chibi bodies: "
            "white-colored WORKERS carrying stone blocks, "
            "yellow ENGINEERS with hard hats, "
            "cyan PRIESTS in robes, "
            "red ZEALOTS with weapons raised. "
            "Each enemy has a small green health bar floating above their cute round head. "
            "Enemies are at bottom of screen on the ground, seen from the side, marching horizontally. "

            "ATTACK EFFECT: "
            "A golden sparkle/lightning bolt from the sky hitting one of the enemies, small star burst effect. "
            "A circular charge ring (white glowing circle) visible near center-bottom of screen. "

            "HUD (clean mobile UI overlay): "
            "Top center: rounded pill-shaped timer showing '10:42' in white text. "
            "Top left: level badge showing 'LV:3'. "
            "Below timer: a small blue EXP progress bar. "
            "No skill buttons in sky - clean sky area. "

            "Overall style: bright, cute, chibi Q-version cartoon. "
            "Similar art style to casual mobile tower defense games with cute characters. "
            "Clean UI, no realistic elements, pure cartoon style."
        )
    },
    {
        "file": "H:/Babel/concept_art/gameplay_v3_upgrade.png",
        "prompt": (
            "Q-style cute chibi cartoon 2D mobile game screenshot, portrait 9:16 vertical. "
            "Upgrade skill selection screen for a cute tower defense game. "

            "Background: same side-scroll scene (sky + ground + chibi tower + frozen enemy crowd) but darkened with semi-transparent black overlay. "

            "Foreground UI panel (center of screen): "
            "Bright rounded panel with 'LEVEL UP!' header in golden bouncy cartoon letters with sparkle effects. "
            "Subtitle: 'LV:2 → LV:3' in smaller text. "

            "3 vertical skill cards arranged side by side, chibi card style with thick rounded borders: "
            "Card 1 (highlighted with golden glow): chibi glowing finger icon, name 'Divine Finger', "
            "blue tag 'CLICK', short description text below. "
            "Card 2: chibi shockwave burst icon, name 'Aftershock', purple tag 'PASSIVE'. "
            "Card 3: chibi clock icon with lightning, name 'Holy Timer', orange tag 'AUTO'. "

            "Cards are cute and colorful with cartoon icon illustrations. "
            "Flat design, thick outlines, soft drop shadows. "
            "Overall: clean, cute mobile game upgrade UI, Q-style cartoon aesthetic."
        )
    }
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
