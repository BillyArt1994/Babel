# tools/

工程辅助脚本目录。所有脚本均使用 `H:/Babel/...` 绝对路径，从任意位置运行：

```bash
python tools/gen-art/generate_concept_art.py
python tools/gen-video/gen_video.py
```

---

## 子目录说明

### gen-art/  — AI 图像生成
调用图像生成 API 生成概念图、UI 草图等美术素材。
输出默认写到 `docs/references/art/concept-art/` 或脚本内指定的绝对路径。

| 脚本 | 用途 |
|------|------|
| `generate_concept_art.py` | 生成游戏概念图 |
| `generate_gameplay_v2/v3.py` | 生成游戏截图风格概念图 |
| `generate_mainmenu_*.py` | 生成主菜单 UI 草图 |
| `generate_real_concepts.py` | 生成写实风格概念图 |
| `generate_ui_concepts.py` / `generate_ui_draft.py` | 生成 UI 界面草图 |

### gen-video/  — AI 视频生成
调用 doubao-seedance-2.0 image2video API 生成动作参考视频。
输出写到 `video_out/`（已加入 .gitignore，临时目录）；需要长期保留的视频归档到 `production/artifacts/video/`。

| 脚本 | 用途 |
|------|------|
| `gen_video.py` | 提交视频生成任务、轮询、下载到 video_out/ |
| `preview_frames.py` | 从视频抽取关键帧预览（验证生成质量用） |

### unity-assets/  — Unity 资源处理
对图像进行裁切、切图、合成，生产最终导入 Unity 的资源文件。
输出直接写到 `Babel_Client/Assets/Art/...`（绝对路径硬编码在脚本里）。

| 脚本 | 用途 |
|------|------|
| `gen_buttons.py` / `gen_buttons_v2.py` | 生成 UI 按钮图 |
| `gen_ui_assets.py` / `gen_ui_assets_remaining.py` | 生成/补全 UI 资源 |
| `slice_assets.py` / `slice_buttons.py` / `slice_mainmenu.py` | 把大图切成多张小图 |
| `trim_buttons.py` | 裁剪按钮图边距 |
| `fix_exit_btn.py` | 修复退出按钮图尺寸/格式 |

---

## 约定

- **脚本内路径均为绝对路径**（`H:/Babel/...`），移动脚本位置不影响运行，但换机器需全局替换路径。
- 临时产物（调试图、预览帧等）写到 `video_out/`（.gitignore 忽略），不进版本库。
- 需要归档的产物（最终视频、源帧 sheet、检查图）移到 `production/artifacts/` 并提交。
