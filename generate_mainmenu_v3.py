#!/usr/bin/env python3
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)

prompt = """
Top-tier mobile game main menu, portrait 9:16. Q-style chibi cartoon.
OVERALL COMPOSITION: identical to a CoC-style menu —
bright blue sky with god-rays, large Tower of Babel midground center-right,
lush green grassy foreground with cobblestone path, wildflowers on sides.
Logo "BABEL" (golden 3D stone carved letters, crown on top, crack effects) upper center.
Subtitle "阻止人类触及天庭" in gold below logo.
Big green juicy "开始游戏" CTA button center, grey "退出游戏" smaller below it.
Small chibi angel top-left with halo and lightning bolt.

=== CHARACTER DESIGN — THIS IS THE KEY FOCUS ===
The foreground human worker characters MUST be drawn in METAL SLUG arcade game style:
exaggerated grotesque-cute proportions and over-the-top comedic expressions.

CHARACTER PROPORTIONS (Metal Slug rules):
- Head = 50-60% of total body height. ENORMOUS head relative to body.
- Body = stubby barrel chest, almost no neck.
- Arms = short and chubby, but wildly expressive — flailing, gesturing.
- Legs = extremely short and stumpy, barely visible under the belly.
- Feet = huge comically oversized boots.
- Eyes = giant round eyes taking up 40% of the face, wildly expressive pupils.

CHARACTER EXPRESSIONS AND POSES (must be funny and exaggerated):

LEFT CHARACTER (carrying stone blocks):
- Absolutely TERRIFIED expression looking upward at the sky/player.
- Eyes are huge spirals or X-marks showing shock.
- Legs are running in place while standing still (cartoon blur lines).
- Sweat drops flying off head in all directions.
- Stone blocks stacked impossibly high and wobbling.
- Mouth open in a huge silent scream.

CENTER CHARACTER (foreman with blueprint):
- Determined/oblivious expression — eyebrows furrowed heroically.
- Chest puffed out proudly, tiny hands on hips.
- Blueprint held upside-down without realizing.
- Comically oversized hardhat sitting crooked on head.
- Round potbelly sticking out.

RIGHT CHARACTER (with flag/hammer):
- Waving tiny flag with enormous grin — huge teeth showing, eyes closed in happiness.
- One leg kicked up behind them in a joyful skip.
- Flag says tiny "工" character on it.
- Hammer that is bigger than their entire torso.

ALL CHARACTERS:
- Thick black outlines.
- Warm earthy brown/tan colors for clothing.
- Each character has their own shadow on the ground.
- Same rich illustrated style as the background — they feel part of the world.
- The exaggeration should make players immediately want to interact with/destroy them (in a fun way).

This is the visual identity goal: players see these workers and LAUGH before they even tap play.
Metal Slug x Clash of Clans art direction fusion.
"""

print("Generating Metal Slug style character variant...")
response = client.images.generate(
    model="gpt-image-2",
    prompt=prompt.strip(),
    size="1024x1536",
    n=1,
)
data = response.data[0]
out = Path("H:/Babel/concept_art/mainmenu_v3_metalslug.png")
if hasattr(data, "b64_json") and data.b64_json:
    out.write_bytes(base64.b64decode(data.b64_json))
elif hasattr(data, "url") and data.url:
    import urllib.request
    urllib.request.urlretrieve(data.url, out)
print(f"Saved: {out}")
