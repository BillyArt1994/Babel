#!/usr/bin/env python3
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

prompt = """
Top-tier mobile game main menu screen, portrait 9:16. Q-style chibi cartoon art.
Inspired by Clash of Clans, Clash Royale, Hay Day quality level.
Thick outlines, richly illustrated, vibrant warm colors, full of life and personality.

=== OVERALL FEEL ===
Bright, energetic, joyful. Warm golden afternoon sunlight.
The player feels powerful and excited to play immediately.
Rich painterly illustration quality. Depth layers: foreground / midground / background.

=== BACKGROUND (full bleed illustration) ===
Gorgeous bright blue sky with dramatic large white cumulus clouds,
golden god-rays of sunlight breaking through from top-center, lighting the scene.
Far background: ancient hilly landscape, green hills, small ancient city silhouettes.
Mid background: the partially-built Tower of Babel rising majestically from the center,
  warm stone color, chunky and impressive, scaffolding visible, glowing with a magical aura.
Foreground (bottom of screen): a lush grassy ground with cobblestone path,
  colorful wildflowers on the sides.

=== CHARACTERS (foreground, add life and personality) ===
LEFT side foreground: 2-3 tiny cute chibi human workers in brown/beige outfits,
  carrying stone blocks and tools, looking determined but adorably non-threatening.
  One is peeking nervously upward toward the player.
RIGHT side foreground: 1 cute chibi worker waving a tiny flag or hammer.
These characters are charming and funny, not scary - they make the player want to "smite" them.

=== TITLE LOGO (upper third, center) ===
"BABEL" game logo - massive, bold, chunky 3D stone carved letters.
Each letter has: warm golden outline, inner stone texture, deep shadow giving 3D pop.
A crack runs through some letters. Small lightning sparks fly off the edges.
Small decorative crown or halo above the B.
Below logo: subtitle "阻止人类触及天庭" in warm gold gradient text, clean readable font.
Thin ornamental divider line below subtitle (diamond + line motif).

=== MAIN CTA BUTTON ("开始游戏") ===
Position: center of screen, between logo and tower.
Large, wide, very juicy button - classic mobile game button style.
Rich gradient: bright green-to-lime on top, darker green on bottom, 3D raised effect.
Thick golden border with small highlights on top edge (giving shiny 3D look).
White bold text "开始游戏" with soft drop shadow.
Small animated-style sparkle/shine on top right corner of button.
Slight perspective warp making it look 3D and pressable.

=== SECONDARY BUTTON ("退出游戏") ===
Smaller, directly below start button, decent spacing.
Flat grey-blue stone style, less prominent.
White text "退出游戏", simple border.

=== DECORATIVE TOUCHES ===
Top-left corner: a small cute chibi angel/cherub floating on a cloud, holding a lightning bolt,
  looking mischievous and confident - this is the player avatar. Halo glowing.
Top-right corner: a decorative stone pillar or ancient rune icon.
Small gold coin/gem icons floating near the buttons for visual richness.
Soft particle effects: tiny golden sparkles and light motes drifting upward.

=== COLOR PALETTE ===
Sky: bright azure to lighter blue
Ground: rich warm greens and earthy browns
Tower: warm sandstone orange-tan
Logo: golden amber with stone texture
Start button: vibrant lime-to-green gradient
Overall mood: warm afternoon sunlight, joyful and epic at the same time.

Make it feel like opening a AAA mobile game. Every pixel should feel polished and inviting.
"""

for i in range(2):
    print(f"Generating variant {i+1}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=prompt.strip(),
        size="1024x1536",
        n=1,
    )
    data = response.data[0]
    out = Path(f"H:/Babel/concept_art/mainmenu_v2_{i+1}.png")
    if hasattr(data, "b64_json") and data.b64_json:
        out.write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, out)
    print(f"  Saved: {out}")

print("Done.")
