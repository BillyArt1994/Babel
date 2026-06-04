# Art Style

生成任何美术资源（尤其 AI 生图）前，**先读风格圣经**：

📖 `docs/references/art/STYLE_GUIDE.md`

里面按章节（角色 / UI / 塔 / 场景）记录了：画风要点、定稿参考图、**可复用提示词配方**、踩坑记录。

## 速记
- 角色画风：**硬朗折线 + 扁平硬色块**（古希腊卡通），禁渐变厚涂。
- 生图工具：`image-generator` skill（勿自写脚本），`--mode variations` 喂定稿图作画风锚点最稳。
- 画风锚点图：`docs/references/art/concept-art/worker_v2_raw.png`。
- 横版朝向：腿脚朝右侧向步态即可，上半身可偏正，别强求纯侧面。

## 姊妹篇：角色设定集

生成海报/过场/视频前，除画风外还要读**角色设定**（年龄/性格/技能/背景）：

📖 `design/lore/characters.md`

风格统一靠 STYLE_GUIDE，设定准确靠 characters.md，两份配合才能产出对味的宣发素材。机制字段以 CSV 为准。
