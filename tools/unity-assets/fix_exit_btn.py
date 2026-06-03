#!/usr/bin/env python3
"""
把 btn_exit 的白边去掉，并稍微加亮颜色提升对比度
"""
from PIL import Image, ImageEnhance
from pathlib import Path

OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")

img = Image.open(OUT / "btn_exit.png").convert("RGBA")
pixels = list(img.getdata())
new_pixels = []
for r, g, b, a in pixels:
    if r > 230 and g > 230 and b > 230:
        new_pixels.append((r, g, b, 0))  # 白色 → 透明
    else:
        new_pixels.append((r, g, b, a))
img.putdata(new_pixels)

bbox = img.getbbox()
trimmed = img.crop(bbox)

# 稍微提亮
brightness = ImageEnhance.Brightness(trimmed)
trimmed = brightness.enhance(1.3)

trimmed.save(OUT / "btn_exit.png", "PNG")
print(f"btn_exit fixed: {trimmed.size}")
