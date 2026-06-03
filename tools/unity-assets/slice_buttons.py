#!/usr/bin/env python3
"""
精确裁切按钮，用边框内侧主色完整填充文字区域。
"""
from PIL import Image, ImageDraw
from pathlib import Path
import os

SRC = Path("H:/Babel/concept_art/mainmenu_v3_metalslug.png")
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
img = Image.open(SRC)
W, H = img.size

# ── 绿色开始按钮 y:1212~1322, x:70~954 ──
btn_start = img.crop((70, 1212, 954, 1322)).copy()
bw, bh = btn_start.size
# 取按钮左侧内边缘绿色（x=40, y=中间）作为填充色
fill_green = btn_start.getpixel((40, bh // 2))
# 完整填充内部区域（留 18px 边框）
border = 18
ImageDraw.Draw(btn_start).rectangle(
    [border, border, bw - border, bh - border],
    fill=fill_green
)
btn_start.save(OUT / "btn_start.png")
print(f"btn_start: {btn_start.size}, fill={fill_green}")

# ── 灰色退出按钮 y:1375~1455, x:290~730 ──
btn_exit = img.crop((290, 1375, 730, 1455)).copy()
bw2, bh2 = btn_exit.size
fill_grey = btn_exit.getpixel((30, bh2 // 2))
border2 = 14
ImageDraw.Draw(btn_exit).rectangle(
    [border2, border2, bw2 - border2, bh2 - border2],
    fill=fill_grey
)
btn_exit.save(OUT / "btn_exit.png")
print(f"btn_exit: {btn_exit.size}, fill={fill_grey}")

# ── 背景：场景插画 y:0~1200，底部填深棕色 ──
bg_clean = img.crop((0, 0, W, 1200))
full_bg = Image.new("RGB", (W, H), (65, 47, 28))
full_bg.paste(bg_clean, (0, 0))
full_bg.save(OUT / "bg_mainmenu.png")
print(f"bg_mainmenu: {full_bg.size}")

# 清理调试文件
for f in OUT.glob("_*.png"):
    os.remove(f)

print("Done.")
