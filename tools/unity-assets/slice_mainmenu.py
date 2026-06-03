# H:/Babel/tools/slice_mainmenu.py
from PIL import Image
from pathlib import Path

SRC = Path("H:/Babel/concept_art/mainmenu_v3_metalslug.png")
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
OUT.mkdir(parents=True, exist_ok=True)

img = Image.open(SRC)  # 1024 x 1536

# 背景：整张图
img.save(OUT / "bg_mainmenu.png")
print("bg_mainmenu.png saved")

# Logo + 副标题区域：顶部 y:30-375
logo = img.crop((60, 30, 964, 375))
logo.save(OUT / "logo_babel.png")
print("logo_babel.png saved")
