# Babel 美术风格指南 (Art Style Guide)

> 本文档是 Babel 项目的美术风格「圣经」。生成任何新美术资源（尤其用 AI 生图）之前**必读对应章节**，确保风格统一。
>
> 参考图与本文档同住 `docs/references/art/`：概念图在 `concept-art/`，外部美术参考在 `gamer-ref/`。
>
> 工具统一用 `image-generator` skill（`~/.claude/skills/image-generator/scripts/gen-image.js`），**不要再自写生图脚本**。

---

## 通用约定

- **生图工具**：`image-generator` skill，`--mode variations` 喂一张已定稿图作画风锚点最稳。
- **参考图存放**：`docs/references/art/concept-art/`。
- **命名惯例**：`<主体>_<版本>.png`（如 `bigworker_cyclops_v2.png`）；去背产物加 `_nobg` 后缀（如 `tower_v10_nobg.png`）。
- **已知坑**：`--background transparent` 在 `variations` 模式下**不生效**，输出为 RGB 黑底无 alpha。需要透明时另做去背步骤。

---

## 角色 (Characters) ✅ 已定稿

游戏角色（工人及各类人类敌人）统一采用 **硬朗折线 + 扁平硬色块** 的手绘卡通风。

### 画风要点（按优先级）

1. **硬朗折线轮廓（最高优先）**：每个关节（肩、肘、膝）都是**尖锐折角**，剪影看起来像刀刻、有棱角，**绝不**圆滑流畅。
2. **扁平硬色块（cel-shading）**：平涂色块 + 单层硬边阴影，色块边界清晰锐利。每种颜色只用 2-3 个色阶。**禁止**柔和渐变、空气感体积明暗、光泽高光。
3. **手绘墨线描边**：线条有粗细变化、偶有断笔，像矢量剪纸 / 手绘油墨，而非均匀闭合的工业描边。
4. **设定**：古希腊背景（束腰外衣 tunic / 缠腰布 / 长袍、皮革凉鞋）。
5. **造型趣味**：每个角色要有一个**独有记忆点**（如独眼巨人的额心独眼），不要平淡无奇。
6. **横版朝向（统一规则）**：这是横版（左右走）游戏，角色必须有明确的朝右行进方向感。判定标准看**腿脚**——双脚脚尖朝右、双腿侧向前后错开的奔跑/行走步态。上半身可适度偏正以保留表情和动作张力，**不必强求纯侧面剪影**；只要腿脚朝向清晰，整体方向感就立得住。

### 画风锚点图

所有新角色统一用 **`concept-art/worker_v2_raw.png`**（瘦削少年扛石料，硬朗折线+扁平色块的基准）作 `--mode variations` 的 source 喂画风。这是画风一致性的唯一基准，不是角色画廊——具体角色长什么样由 prompt 决定，本文档只固化**规则**与**配方**。

### 可复用提示词配方

用 `--mode variations --source worker_v2_raw.png` 喂画风锚点，prompt 分三段，**三段都要写满**否则模型会漂成圆润厚涂卡通：

**第 1 段 — 死锁画风（最重要，照抄）：**
```
Replicate the EXACT art style of the reference image. HARD ANGULAR INK OUTLINES
with sharp pointed corners at every joint (shoulders, elbows, knees) — silhouette
must look carved and jagged, NOT smooth/rounded. FLAT cel-shaded color blocks with
crisp hard-edged boundaries — absolutely NO soft gradients, NO airbrushed volume
shading, NO glossy highlights. Only 2-3 flat tone steps per color, like vector
cutout art. Loose hand-inked linework with varying line weight and occasional
broken strokes.
```

**第 2 段 — 角色主体：** 描述体型 / 服装 / 动作 / 古希腊设定 / 独有记忆点 / **朝右行进的腿脚步态**。
> ⚠️ **关键特征必须用强措辞**（`exactly ONE` / `CRITICAL` / `defining feature` / `no second eye anywhere`），否则模型不落地。例：独眼写成 `exactly ONE single large round eye in the CENTER of the forehead — only ONE eye, no second eye anywhere, this is the defining feature`。
> ⚠️ **朝向**：要求腿脚朝右即可，描述落在腿脚上（`both feet pointing RIGHT, legs in a side-view running stride`），别去强求整体纯侧面（见踩坑记录）。

**第 3 段 — 收尾再强调画风：**
```
Match the reference's exact rendering style precisely: angular, flat, hard-lined,
NOT soft or painterly.
```

### 完整命令示例（生成独眼巨汉 v2 时实际用的）
```bash
node ~/.claude/skills/image-generator/scripts/gen-image.js \
  --mode variations \
  --source "H:/Babel/docs/references/art/concept-art/worker_v2_raw.png" \
  -p "<第1段画风> <第2段角色主体> <第3段收尾>" \
  --size 1024x1536 --background transparent --quality high \
  -o "H:/Babel/docs/references/art/concept-art/<name>.png"
```

### 踩坑记录
- ❌ 只写 "match the reference style" → 出来是软线条渐变厚涂。必须**显式列禁项**（no gradient / no volume shading）+ **显式要折角轮廓**。
- ❌ 第一版只说 "Cyclops" 没强约束 → 画成两只眼。改成 `exactly ONE ... no second eye anywhere` 才真独眼。
- ⚠️ 体型也要强约束：v2 想要「圆胖呆萌」但没死写，结果漂成精壮肌肉汉。要胖就写满 `round bulging belly, short stubby limbs, NOT muscular`。
- ⚠️ **横版朝向别矫枉过正**：角色显得「不够朝右」时，根因多半在**腿脚**而非整体视角。只需把腿脚改成朝右侧向步态，上半身偏正反而能保住表情/动作张力。若强行要求「纯侧面、单眼侧脸、躯干全侧」，会把欢呼张臂之类的上半身动作压扁、丢掉记忆点——是过度修正。改朝向时**只动腿脚，别动上半身**（用已认可图作 variations source，prompt 明确「保留上半身、只重画腿」最稳）。

---

## UI 界面 (UI / HUD) 🚧 待补

> 占位。现有参考：`concept-art/02_upgrade_select.png`、`gameplay_v3_*.png`、`mainmenu_v3_metalslug.png`。
> 做相关资源时在此章固化画风 + 配方。

---

## 塔 (Tower) 🚧 待补

> 占位。现有参考：`concept-art/tower_babel_v10.png`。

---

## 场景 / 背景 (Environment) 🚧 待补

> 占位。现有参考：`concept-art/01_gameplay_v2.png`、`real_gameplay_01.png`。
