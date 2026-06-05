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

游戏所有敌人都是**同一个僭越者种族**，统一采用 **硬朗折线 + 扁平硬色块** 的手绘卡通风，
走 **辛普森式滑稽 + 古希腊贫民** 的「讽刺丑态僭越者」路线。

> **拆成三层理解**（本章只固化前两层，第三层留空给个体）：
> - **① 渲染画风**——*怎么画*。**永远锁定、所有角色照抄**，是画风一致性的根。
> - **② 种族特性**——*是哪个种族*。定义「这些都是同一族」的不变生物/文化特征，**所有角色照抄**。
> - **③ 单角色变量**——*是这族里的谁*。体型、表情、发型、专属记忆点……**因角色而异，不写进本圣经**，由每个角色 prompt 自己写满。后期会从种族特性派生出各种体型/表情/发型的新角色。

### ① 渲染画风（四要素，最高优先，所有角色照抄）

1. **硬朗墨线轮廓**：手绘油墨描边，线条有**粗细变化**、偶有**断笔/收尖**，像矢量剪纸而非均匀工业描边。剪影与各大块边界都是**尖锐折角**，不圆滑流畅。
2. **扁平硬色块（cel-shading）**：平涂色块 + 单层硬边阴影，边界清晰锐利。每色只用 2-3 个色阶。**禁止**柔和渐变、空气感体积明暗、光泽高光、喷枪厚涂。
3. **只在边缘折角**：折角只出现在**剪影**和**大块之间的分界线**上；表面内部**保持干净平涂**，**禁止**内部多边形小切面、low-poly 线框铺满表面。
4. **硬朗清脆边缘**：剪影处、大块交界处一律硬边清脆。

### ② 种族特性（锁定，定义「这是同一个种族」的不变特征）

工人、精英、斥候、祭司、狂信者……都是**同一个种族**的成员。下列特征是**种族级**的——
让玩家一眼认出「这些都是同一族的僭越者」。**所有新角色照抄这几条**，再在此基础上派生个体差异。

1. **类人非人有机活体**：humanoid 但**非人**，是有机活体血肉，**不是**机器人/机械构造体，**绝无**金属部件、装甲板、机械关节。
2. **苍白灰绿色皮肤**：全族统一的**灰绿肤色**（pale grey-green），是种族的视觉签名。
3. **凸出的护目镜般大圆眼**：又大又圆、向外鼓的**眼型**——这是种族五官的形态特征（**眼型固定，眼神/表情按个体变**）。
4. **圆润有机蛋形颅骨**：**蛋形**头骨，结构感只来自侧脸轮廓，**绝不**方块/立方体头（**头骨形态固定，发型按个体变**）。
5. **古希腊贫民文化**：全族共享的穿着文化——单肩短款 exomis 束腰外衣、未染米色粗布、绳腰带、破旧凉鞋。**细节从简**，最多留一处低饱和点缀色，不堆补丁/破洞/绑带。
6. **「讽刺丑态僭越者」基调**：滑稽、丑萌、卑微，**绝不**帅气/英武/英雄感。敌人不需要好看。
7. **横版朝向（统一规则）**：横版（左右走）游戏，角色须有明确朝右行进感。判定看**腿脚**——双脚脚尖朝右、双腿侧向前后错开的步态。上半身可适度偏正以保留表情/动作张力，**不必强求纯侧面剪影**；腿脚朝向清晰即可。

### ③ 单角色变量（自由，**不写进本圣经**，由每个角色 prompt 自己写满）

从种族特性出发，靠这些变量把同族成员区分成不同角色——它们**因角色而异**，所以**不在通用规则里预设**：

- **体型**：工人佝偻、精英魁梧、斥候精瘦、巨汉壮硕……
- **表情/眼神**：工人生无可恋、狂信者狂热癫狂、祭司阴郁……（眼型是种族固定的，但半阖/瞪大/眯眼按角色定）
- **发型**：刺头、秃顶、长须、缠头……
- **专属记忆点**：独眼巨汉的额心独眼、斥候的号角、工人背上的石块——每个角色给**一个**辨识特征。
- **动作/姿势**：扛石、吹号、祈祷、冲锋……

> 一句话：**种族特性回答「这是哪个种族」（锁定），单角色变量回答「这是这族里的谁」（自由）**。
> `worker_v3.png` 的刺头 + 生无可恋脸属于**工人这个角色**的变量，不是种族特征——别把它们当成全族通用照抄。

### 画风锚点图

所有新角色统一用 **`concept-art/worker_v3.png`** 作 `--mode variations` 的 source 喂画风锚点。它把①渲染画风+②种族特性全部坐实（灰绿皮、蛋形头、凸出大圆眼、exomis 短袍、丑萌基调），同时也带着**工人这个角色**的个体变量（佝偻体型、刺头、生无可恋脸）。
> ⚠️ **作锚点用时：照抄它的①渲染+②种族特性，但别照抄它的体型/发型/表情**——那些是工人的个体变量，新角色按 ③ 自定。
> 这是画风一致性的唯一基准，不是角色画廊；本文档只固化**规则**与**配方**。
> 另备 `_proto_titan_replica4.png` 作纯渲染锚点（同一套渲染画风、不同体型，验证①可脱离体型复用）。

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

**第 2 段 — 种族特性（照抄）+ 该角色个体（自定）：**
先写满**种族特性**（灰绿非人有机活体 / 凸出大圆眼 / 蛋形头骨 / 古希腊贫民 exomis / 丑萌僭越者基调），再写**该角色的个体变量**（体型 / 表情眼神 / 发型 / 专属记忆点 / 动作 / 朝右腿脚步态）。
> ⚠️ **种族特性必拷**：`humanoid but NOT human, organic living flesh, NOT a robot, NO metal parts; pale grey-green skin; large round bulging goggle-like eyes; rounded organic egg-shaped skull; Ancient-Greek poor-commoner one-shoulder exomis tunic, undyed coarse beige cloth, rope belt, worn sandals; goofy/dopey, NOT cool/handsome/heroic`。
> ⚠️ **个体变量按角色自定**：表情（疲惫/狂热/阴郁）、发型（刺头/秃顶/缠头）、体型（佝偻/魁梧/精瘦/壮硕）、专属记忆点——这些**不要照抄 worker_v3**，每个角色自己写。
> ⚠️ **关键特征用强措辞**（`exactly ONE` / `CRITICAL` / `defining feature` / `no second eye anywhere`）。例：独眼写 `exactly ONE single large round eye in the CENTER of the forehead — only ONE eye, no second eye anywhere`。
> ⚠️ **头型**：写 `ROUNDED ORGANIC egg-shaped skull, smooth curved dome, structure ONLY from the side-profile contour in the classic Simpsons way`，并**显式禁** `NO boxy / square / cube / flat-sided head`（见踩坑）。
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
  -p "<第1段渲染画风> <第2段种族特性+个体变量> <第3段收尾>" \
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
