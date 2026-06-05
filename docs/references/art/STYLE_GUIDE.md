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

游戏角色（工人及各类人类敌人）统一采用 **硬朗折线 + 扁平硬色块** 的手绘卡通风，
角色设计走 **辛普森式滑稽 + 古希腊贫民** 的「讽刺丑态僭越者」路线。

> **拆成两层理解（「皮 / 骨」）**：
> - **皮 = 渲染画风**（下方「① 渲染画风」）——**永远锁定、所有角色照抄**，是画风一致性的根。
> - **骨 = 体型比例**——**自由变量**，工人佝偻、精英魁梧、斥候精瘦、巨汉壮硕……后期会持续拓展各种体型。
>   **体型不写进通用规则**，由每个角色的 prompt 自己写满。本章只固化「皮」和「角色设计语言」，不锁体型。

### ① 渲染画风（四要素，最高优先，所有角色照抄）

1. **硬朗墨线轮廓**：手绘油墨描边，线条有**粗细变化**、偶有**断笔/收尖**，像矢量剪纸而非均匀工业描边。剪影与各大块边界都是**尖锐折角**，不圆滑流畅。
2. **扁平硬色块（cel-shading）**：平涂色块 + 单层硬边阴影，边界清晰锐利。每色只用 2-3 个色阶。**禁止**柔和渐变、空气感体积明暗、光泽高光、喷枪厚涂。
3. **只在边缘折角**：折角只出现在**剪影**和**大块之间的分界线**上；表面内部**保持干净平涂**，**禁止**内部多边形小切面、low-poly 线框铺满表面。
4. **硬朗清脆边缘**：剪影处、大块交界处一律硬边清脆。

### ② 角色设计语言（敌人通用，与体型无关）

1. **非人有机生物**：苍白**灰绿色皮肤**的类人活体，**不是**机器人/机械构造体，**绝无**任何金属部件、装甲板、机械关节。
2. **辛普森式滑稽五官**：又大又圆的**凸出护目镜般大眼**；头部是**圆润有机的蛋形颅骨**（结构感只来自侧脸轮廓，**绝不**方块/立方体头）。
3. **生无可恋表情**：半阖的下垂眼皮 + 眼袋 + 平直/下撇的嘴，一脸疲惫麻木。**不要**阳光讨喜的笑——那会冲淡「讽刺丑态」的基调。
4. **辛普森式刺头发型**：头顶一撮撮深色尖刺状头发。
5. **古希腊贫民穿着**：单肩短款 exomis 束腰外衣，未染的米色粗布，绳腰带，破旧凉鞋。**细节从简**——最多留一处低饱和点缀色，不要堆补丁/破洞/绑带。
6. **基调**：「讽刺丑态僭越者」——滑稽、丑萌、卑微，**绝不**帅气/英武/英雄感。敌人不需要好看。
7. **独有记忆点**：每个角色给一个**专属辨识特征**（独眼巨汉的额心独眼、斥候的号角、工人背上的石块），不要平淡无奇。
8. **横版朝向（统一规则）**：横版（左右走）游戏，角色须有明确朝右行进感。判定看**腿脚**——双脚脚尖朝右、双腿侧向前后错开的步态。上半身可适度偏正以保留表情/动作张力，**不必强求纯侧面剪影**；腿脚朝向清晰即可。

### 画风锚点图

所有新角色统一用 **`concept-art/worker_v3.png`**（佝偻扛石工人：灰绿皮、蛋形头、护目镜大眼、刺头、生无可恋脸、exomis 短袍——四要素 + 设计语言全部到位）作 `--mode variations` 的 source 喂画风。这是画风一致性的唯一基准，不是角色画廊——**具体角色长什么样、什么体型由 prompt 决定**，本文档只固化**规则**与**配方**。
> 另备 `_proto_titan_replica4.png` 作纯渲染锚点（同一套「皮」、不同体型，用于验证渲染语言可脱离体型复用）。

### 可复用提示词配方

用 `--mode variations --source worker_v3.png` 喂画风锚点，prompt 分三段，**三段都要写满**否则模型会漂成圆润厚涂卡通：

**第 1 段 — 死锁渲染画风（最重要，照抄）：**
```
Replicate the EXACT art style of the reference image. HARD hand-inked ANGULAR
outlines with varying line weight and occasional broken/tapered strokes, like
brush-and-ink vector cutout art — sharp pointed corners at the silhouette and
between major shapes, NOT smooth/rounded, NOT uniform industrial linework. FLAT
cel-shaded color blocks with crisp hard-edged boundaries — absolutely NO soft
gradients, NO airbrushed volume shading, NO glossy highlights. Only 2-3 flat tone
steps per color. Angularity ONLY at silhouette + major-piece boundaries; surface
interiors stay clean and flat — NO internal polygon facets, NO low-poly wireframe.
```

**第 2 段 — 敌人设计语言 + 角色主体：** 先写满设计语言（灰绿非人活体 / 辛普森式凸眼蛋形头 / 生无可恋脸 / 刺头 / 古希腊贫民 exomis），再写**该角色专属的体型 / 动作 / 独有记忆点 / 朝右腿脚步态**。
> ⚠️ **关键特征用强措辞**（`exactly ONE` / `CRITICAL` / `defining feature` / `no second eye anywhere`）。例：独眼写 `exactly ONE single large round eye in the CENTER of the forehead — only ONE eye, no second eye anywhere`。
> ⚠️ **头型**：写 `ROUNDED ORGANIC egg-shaped skull, smooth curved dome, structure ONLY from the side-profile contour in the classic Simpsons way`，并**显式禁** `NO boxy / square / cube / flat-sided head`（见踩坑）。
> ⚠️ **基调**：写 `goofy / dopey / weary dead-inside, NOT cool, NOT handsome, NOT heroic`。
> ⚠️ **体型**：体型是该角色自己的事，**在 prompt 里写满**（如要佝偻就 `deeply hunched, upper back near-horizontal, bent knees`；要壮硕就 `broad barrel torso, thick limbs`），通用规则里不预设任何体型。
> ⚠️ **朝向**：描述落在腿脚上（`both feet pointing RIGHT, legs in a side-view walking stride`），别强求整体纯侧面。

**第 3 段 — 收尾再强调画风：**
```
Match the reference's exact rendering style precisely: angular, flat, hard-lined,
NOT soft or painterly.
```

### 完整命令示例
```bash
node ~/.claude/skills/image-generator/scripts/gen-image.js \
  --mode variations \
  --source "H:/Babel/docs/references/art/concept-art/worker_v3.png" \
  -p "<第1段渲染画风> <第2段设计语言+角色主体> <第3段收尾>" \
  --size 1024x1536 --background transparent --quality high \
  -o "H:/Babel/docs/references/art/concept-art/<name>.png"
```

### 踩坑记录
- ❌ 只写 "match the reference style" → 出来是软线条渐变厚涂。必须**显式列禁项**（no gradient / no volume shading）+ **显式要折角轮廓**。
- ❌ 头型写「flat forehead plane / faceted skull / angular jaw」→ 出来是僵硬**方块头**（用户当场指出「怎么是方块型头了」）。修法：写 `ROUNDED ORGANIC egg-shaped skull` + 显式禁 `boxy/square/cube/flat-sided` + 强调结构**只来自侧脸轮廓**。
- ❌ 第一版只说 "Cyclops" 没强约束 → 画成两只眼。改成 `exactly ONE ... no second eye anywhere` 才真独眼。
- ⚠️ **不写非人/不写灰绿皮 → 漂成机器人或正常人**。要的是有机活体，须显式 `pale grey-green skin, organic living creature, NOT a robot, NO metal parts`。
- ⚠️ **不写基调 → 漂成帅气英雄**。敌人要丑萌卑微，须显式 `goofy/dopey, weary, NOT cool/handsome/heroic`。
- ⚠️ **衣服细节会自己堆**（补丁/破洞/绑带一大堆）。要简洁须显式 `plain one-piece tunic, NO patches, NO holes, NO extra straps, keep only one small muted accent`。
- ⚠️ **内容审核词坑**：`muzzle` / `jutting` / `doubled over` / `straining hard` / `buck teeth` 等词在某些组合下触发 400。换中性措辞（`protruding mouth area` / `deeply hunched forward` / `cheerful wide cartoon grin`）。
- ⚠️ **横版朝向别矫枉过正**：显得「不够朝右」时根因多半在**腿脚**。只改腿脚为朝右步态，上半身偏正反而保住表情/动作张力。强求「纯侧面、单眼侧脸、躯干全侧」会压扁上半身动作、丢记忆点——过度修正。改朝向时**只动腿脚**（用已认可图作 source，prompt 明确「保留上半身、只重画腿」最稳）。

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
