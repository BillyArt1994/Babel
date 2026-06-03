#!/usr/bin/env python3
"""
Generate UIGamePanel layout draft based on exact prefab structure:

UIGamePanel
├── EXP_Info (top bar)
│   ├── EXP (label text)
│   ├── LevelText ("LV:3")
│   └── EXPScrollbar (blue progress bar)
├── MainSkill_Image (bottom-left skill icon circle)
│   └── MainSkill_ImageFill (cooldown arc overlay)
├── TimeScale (top-right)
│   └── TimeScaleButton ("2x")
│       └── TimeScaleText
├── LevelTimer (top-center)
│   └── TimerText ("12:34")
├── UpgradePanel (hidden, center fullscreen overlay)
│   ├── Card1Btn / Card2Btn / Card3Btn
│       ├── SkillNameText
│       └── SkillDecsText
└── ChargeRing (hidden, follows touch point)
    ├── ChargeRing_Background
    └── ChargeRing_Fill
"""
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

Path("H:/Babel/concept_art").mkdir(parents=True, exist_ok=True)

prompt = """
You are creating a UI design draft mockup for a Q-style cute mobile game called 'Babel'.
Portrait 9:16 vertical layout. Bright kawaii cartoon style with thick outlines.

This is ONLY the HUD overlay layer — the game world beneath is visible (blue sky + sandy ground + pyramid tower + chibi enemies).
The background should show: blue sky (top 60%), sandy ground (bottom 40%), chibi pyramid tower at center.

Draw the following HUD elements EXACTLY as positioned:

=== TOP BAR (top 8% of screen) ===
A thin dark semi-transparent rounded pill bar spanning full width at the very top.
Contents left to right:
- LEFT: "LV:3" text in a small golden badge pill
- CENTER: Clock icon + "12:34" countdown timer in white bold text
- RIGHT: "2x" speed button, small rounded dark button with amber text

=== EXP BAR (just below top bar) ===
A thin horizontal progress bar spanning full width, glowing blue/cyan color, ~40% filled.
Small "EXP" label at far left.

=== BOTTOM-LEFT: MAIN SKILL ICON ===
Position: bottom-left corner, about 20% from left, 12% from bottom.
A circular skill icon button, ~80px diameter.
Shows a glowing golden pointing finger icon inside a stone medallion circle.
Around the circle: a cooldown arc overlay (dark arc showing remaining cooldown, ~30% covered).
This is the player's active attack skill.

=== CENTER-BOTTOM: CHARGE RING (shown mid-charge) ===
Position: center of screen, slightly below center vertical.
A glowing white circular ring, ~100px diameter.
Inner ring fills up with golden light as charge progresses (shown ~60% filled as example).
This appears when the player holds down their finger.

=== NO OTHER HUD ELEMENTS ===
No pause button. No other buttons. Clean minimal HUD.
The sky area is open and uncluttered.

Style requirements:
- All UI elements use kawaii cartoon style: rounded shapes, soft drop shadows, glowing effects
- Semi-transparent dark backgrounds for readability
- Bright gold/white text
- The overall screen feels open and uncluttered — HUD elements hug the edges
- Thick black outlines on all UI elements for readability
"""

print("Generating UIGamePanel layout draft...")
response = client.images.generate(
    model="gpt-image-2",
    prompt=prompt.strip(),
    size="1024x1536",
    n=1,
)
data = response.data[0]
out = Path("H:/Babel/concept_art/ui_draft_gamepanel.png")
if hasattr(data, "b64_json") and data.b64_json:
    out.write_bytes(base64.b64decode(data.b64_json))
elif hasattr(data, "url") and data.url:
    import urllib.request
    urllib.request.urlretrieve(data.url, out)
print(f"Saved: {out}")
