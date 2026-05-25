# H:/Babel/tools/gen_buttons.py
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
OUT.mkdir(parents=True, exist_ok=True)

buttons = [
    {
        "file": OUT / "btn_start.png",
        "prompt": (
            "A single UI button graphic for a mobile game, landscape orientation 4:1 ratio. "
            "Isolated on TRANSPARENT background - no scene, no characters, no text. "
            "Style: Clash of Clans quality juicy 3D button. "
            "Shape: wide rounded rectangle. "
            "Color: bright green-to-lime vertical gradient, top edge has white highlight shine strip. "
            "Border: thick golden/amber outline with small beveled edges. "
            "Bottom has darker green shadow for 3D raised depth. "
            "Small golden sparkle on top-right corner. "
            "The button looks pressable and satisfying. Clean game UI asset. "
            "No text on the button. Transparent PNG background."
        )
    },
    {
        "file": OUT / "btn_exit.png",
        "prompt": (
            "A single UI button graphic for a mobile game, landscape orientation 4:1 ratio. "
            "Isolated on TRANSPARENT background - no scene, no characters, no text. "
            "Style: Clash of Clans quality button, secondary/less prominent. "
            "Shape: wide rounded rectangle, slightly smaller than main button. "
            "Color: dark grey-blue stone texture gradient. "
            "Border: medium thickness grey/silver outline. "
            "Subtle 3D raised effect, not as shiny as start button. "
            "Clean game UI asset. No text on the button. Transparent PNG background."
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
