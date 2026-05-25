#!/usr/bin/env python3
"""
裁掉按钮图片的白色边距，只保留按钮本体。
使用 getbbox() 自动检测非白内容边界。
"""
from PIL import Image
from pathlib import Path

OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")

for name in ["btn_start.png", "btn_exit.png"]:
    img = Image.open(OUT / name).convert("RGBA")
    # 把白色像素变为透明，让 getbbox 识别内容
    data = img.getdata()
    new_data = []
    for px in data:
        r, g, b, a = px
        # 白色或接近白色 → 透明
        if r > 240 and g > 240 and b > 240:
            new_data.append((r, g, b, 0))
        else:
            new_data.append(px)
    img.putdata(new_data)

    # 自动裁切到内容边界
    bbox = img.getbbox()
    if bbox:
        cropped = img.crop(bbox)
        # 转回 RGB（Unity 的 alphaIsTransparency 会处理）
        result = Image.new("RGBA", cropped.size, (0, 0, 0, 0))
        result.paste(cropped, (0, 0))
        result.save(OUT / name, "PNG")
        print(f"{name}: {img.size} -> {cropped.size}")
    else:
        print(f"{name}: no content found!")
