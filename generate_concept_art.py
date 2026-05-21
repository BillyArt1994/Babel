#!/usr/bin/env python3
"""Generate Babel game concept art using OpenAI gpt-image-1."""

import base64
import os
import sys
from pathlib import Path

try:
    from openai import OpenAI
except ImportError:
    print("Installing openai...", file=sys.stderr)
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "openai"])
    from openai import OpenAI

api_key = os.environ.get("OPENAI_API_KEY")
if not api_key:
    print("Error: OPENAI_API_KEY not set", file=sys.stderr)
    sys.exit(1)

base_url = os.environ.get("OPENAI_BASE_URL", "https://chat-test.q1.com/v1")
client = OpenAI(api_key=api_key, base_url=base_url)

prompt = """
Dark mythological digital concept art for a mobile tower defense game called 'Babel'.
Vertical 9:16 portrait composition.

Scene: A massive ancient Tower of Babel under construction fills the center of the frame,
rising in stepped stone tiers toward storm clouds. Each tier has tiny silhouetted human
workers laboring on it. From both left and right edges, hordes of humans swarm out of
deep shadow toward the tower base like an unstoppable flood.

At the very top of the frame, a colossal divine hand descends from above, fingertips
crackling with golden holy lightning bolts, smiting the humans below. The hand radiates
god-rays of amber light downward.

Sky: churning dark crimson thunderclouds with lightning flashes.
Color palette: dark gold, deep crimson red, charcoal black, amber god-rays.
Mood: epic, oppressive, god's-eye-view cinematic.
Style: high-detail digital painting, dramatic chiaroscuro lighting.
No text or UI elements.
"""

print("Generating concept art with gpt-image-1...", file=sys.stderr)

response = client.images.generate(
    model="dall-e-3",
    prompt=prompt.strip(),
    size="1024x1792",
    quality="hd",
    n=1,
    response_format="b64_json",
)

image_data = response.data[0]

output_path = Path("H:/Babel/concept_art_babel.png")

if hasattr(image_data, 'b64_json') and image_data.b64_json:
    image_bytes = base64.b64decode(image_data.b64_json)
    output_path.write_bytes(image_bytes)
    print(f"Saved: {output_path}")
elif hasattr(image_data, 'url') and image_data.url:
    import urllib.request
    urllib.request.urlretrieve(image_data.url, output_path)
    print(f"Saved: {output_path}")
else:
    print("Error: No image data returned", file=sys.stderr)
    sys.exit(1)
