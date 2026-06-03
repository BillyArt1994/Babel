#!/usr/bin/env python3
import base64
import os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

prompts = [
    {
        "file": "H:/Babel/concept_art/01_gameplay.png",
        "prompt": (
            "Mobile game screenshot UI concept art, portrait 9:16. 2D tower defense survival game called Babel. "
            "Active gameplay scene: a multi-layered ancient stone tower in screen center viewed from the side, "
            "3 visible horizontal floors connected by ladders. Each floor has glowing circular build points, "
            "some completed in red/orange, some still dark. "
            "Dozens of tiny human figures swarm from left and right toward the tower base. "
            "Top-center: a giant semi-transparent divine hand, finger pointing down, "
            "golden lightning bolt shooting from fingertip hitting a human. "
            "HUD: top bar with countdown timer '12:34', EXP bar '3/5', level 'Lv.3'. "
            "Bottom: glowing circular skill icon with cooldown arc. "
            "Green health bars float above enemies. "
            "Style: stylized 2D painted illustration, dark navy background, amber divine elements, clean mobile game UI."
        )
    },
    {
        "file": "H:/Babel/concept_art/02_upgrade_select.png",
        "prompt": (
            "Mobile game UI screenshot concept, portrait 9:16. Skill upgrade selection screen for a god defense game Babel. "
            "Background: frozen battle scene slightly darkened. "
            "Foreground centered panel: golden header 'LEVEL UP!'. "
            "3 vertical skill cards side by side: "
            "Card 1 'Divine Finger Lv2' with glowing finger icon, blue CLICK badge, '+40% damage 0.8s cooldown'; "
            "Card 2 'Aftershock' with shockwave icon, purple PASSIVE badge, 'AOE burst 30% dmg after each strike'; "
            "Card 3 'Holy Timer' with clock+lightning icon, orange AUTO badge, 'Auto strike every 3s'. "
            "Cards have rounded corners, dark semi-transparent background, hover glow. "
            "Top shows 'Lv.2 to Lv.3'. Dark fantasy mobile game UI style, clean and elegant."
        )
    },
    {
        "file": "H:/Babel/concept_art/03_game_over.png",
        "prompt": (
            "Mobile game defeat screen UI concept, portrait 9:16, game Babel. "
            "Background: fully constructed Tower of Babel glowing ominously, tiny human silhouettes celebrating, dark red sky with lightning. "
            "Foreground: large centered defeat panel, cracked stone letters header 'BABEL COMPLETE' with red glow, "
            "italic subtitle 'The tower reaches the heavens...', "
            "stats: Survival Time 8:42, Enemies Smited 347, Max Level 7, Skills Equipped 4. "
            "Two buttons: gold 'TRY AGAIN' and dark 'MAIN MENU'. "
            "Dramatic vignette, falling embers. Colors: deep crimson, black, dark gold. Epic cinematic mobile game."
        )
    },
]

for item in prompts:
    name = Path(item["file"]).name
    print(f"Generating {name}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=item["prompt"],
        size="1024x1536",
        n=1,
    )
    data = response.data[0]
    if hasattr(data, "b64_json") and data.b64_json:
        Path(item["file"]).write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, item["file"])
    print(f"  Saved: {item['file']}")

print("Done.")
