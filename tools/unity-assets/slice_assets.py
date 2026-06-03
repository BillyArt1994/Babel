#!/usr/bin/env python3
from PIL import Image, ImageDraw
from pathlib import Path

SRC = Path("H:/Babel/concept_art/mainmenu_v3_metalslug.png")
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
img = Image.open(SRC).convert("RGBA")
W, H = img.size  # 1024 x 1536

# ── BG：完整场景，裁去最底部仅保留到按钮下方 ──
# 保留 y:0~1480，底部56px填地面色避免空白
CUT_Y = 1480
ground_color = img.getpixel((W // 2, CUT_Y - 20))[:3]
bg = img.copy().convert("RGB")
draw = ImageDraw.Draw(bg)
draw.rectangle([0, CUT_Y, W, H], fill=ground_color)
bg.save(OUT / "bg_mainmenu.png")
print(f"bg_mainmenu: {bg.size}, ground={ground_color}")

# ── btn_start：y:1212~1322, x:70~954 ──
def crop_transparent(crop_box):
    c = img.crop(crop_box).convert("RGBA")
    px = c.load()
    cw, ch = c.size
    for y in range(ch):
        for x in range(cw):
            r, g, b, a = px[x, y]
            if r > 235 and g > 235 and b > 235:
                px[x, y] = (r, g, b, 0)
    bbox = c.getbbox()
    return c.crop(bbox) if bbox else c

btn_start = crop_transparent((70, 1212, 954, 1322))
btn_start.save(OUT / "btn_start.png")
print(f"btn_start: {btn_start.size}")

# ── btn_exit：重新精确扫描 ──
# 扫描 y:1370~1470 区域确认灰色按钮边界
btn_exit = crop_transparent((250, 1370, 775, 1458))
btn_exit.save(OUT / "btn_exit.png")
print(f"btn_exit: {btn_exit.size}")

# 计算 Unity Canvas 坐标 (720x1280)
IW, IH, CW, CH = 1024, 1536, 720, 1280
def to_canvas(x1, y1, x2, y2):
    cx = ((x1+x2)/2 / IW - 0.5) * CW
    cy = (0.5 - (y1+y2)/2 / IH) * CH
    w  = (x2-x1) / IW * CW
    h  = (y2-y1) / IH * CH
    return round(cx,1), round(cy,1), round(w,1), round(h,1)

print("\nUnity Canvas coords:")
print("btn_start:", to_canvas(70,1212,954,1322))
print("btn_exit: ", to_canvas(250,1370,775,1458))
