# Main Menu Art Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将主菜单概念图 `mainmenu_v3_metalslug.png` 切分为独立 Sprite 资产，导入 Unity，并重写 `UIMainMenuPanel.BuildRuntimeLayout()` 使用真实 Sprite 替代纯色占位符。

**Architecture:** 用 Python+Pillow 从概念图裁切出背景、Logo、两个按钮四张 Sprite；同时用 gpt-image-2 生成干净的按钮 9-slice 素材（无文字，可拉伸）；最后重写 `UIMainMenuPanel` 加载这些 Sprite，按钮文字保留为 Text 组件以便本地化。

**Tech Stack:** Python 3 + Pillow（裁图）、gpt-image-2（按钮素材）、Unity uGUI Image + Button、C# 资源加载 `Resources.Load<Sprite>`

---

## 文件变更清单

| 操作 | 路径 |
|------|------|
| 新建（Python 生成） | `Babel_Client/Assets/Art/UI/MainMenu/bg_mainmenu.png` |
| 新建（Python 生成） | `Babel_Client/Assets/Art/UI/MainMenu/logo_babel.png` |
| 新建（gpt-image-2） | `Babel_Client/Assets/Art/UI/MainMenu/btn_start.png` |
| 新建（gpt-image-2） | `Babel_Client/Assets/Art/UI/MainMenu/btn_exit.png` |
| 修改 | `Babel_Client/Assets/Scripts/UI/UIMainMenuPanel.cs` |
| 新建（生成脚本，不入项目） | `H:/Babel/tools/slice_mainmenu.py` |
| 新建（生成脚本，不入项目） | `H:/Babel/tools/gen_buttons.py` |

---

## Task 1：裁切背景与 Logo Sprite

**Files:**
- Create: `H:/Babel/tools/slice_mainmenu.py`
- Output: `Babel_Client/Assets/Art/UI/MainMenu/bg_mainmenu.png`
- Output: `Babel_Client/Assets/Art/UI/MainMenu/logo_babel.png`

- [ ] **Step 1：写裁图脚本**

```python
# H:/Babel/tools/slice_mainmenu.py
from PIL import Image
from pathlib import Path

SRC = Path("H:/Babel/concept_art/mainmenu_v3_metalslug.png")
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
OUT.mkdir(parents=True, exist_ok=True)

img = Image.open(SRC)  # 1024 x 1536

# ── 背景：整张图（UI 层在 Unity 叠在上面） ──
img.save(OUT / "bg_mainmenu.png")
print("bg_mainmenu.png saved")

# ── Logo + 副标题区域：顶部约 y:30-370 ──
logo = img.crop((60, 30, 964, 375))
logo.save(OUT / "logo_babel.png")
print("logo_babel.png saved")
```

- [ ] **Step 2：运行脚本**

```bash
python "H:/Babel/tools/slice_mainmenu.py"
```

期望输出：
```
bg_mainmenu.png saved
logo_babel.png saved
```

- [ ] **Step 3：验证两张图片存在且内容正确**

```bash
python -c "
from PIL import Image
bg = Image.open('H:/Babel/Babel_Client/Assets/Art/UI/MainMenu/bg_mainmenu.png')
logo = Image.open('H:/Babel/Babel_Client/Assets/Art/UI/MainMenu/logo_babel.png')
print('bg:', bg.size)
print('logo:', logo.size)
assert bg.size == (1024, 1536)
assert logo.size[0] == 904
print('OK')
"
```

期望输出：
```
bg: (1024, 1536)
logo: (904, 345)
OK
```

- [ ] **Step 4：Commit**

```bash
git add H:/Babel/tools/slice_mainmenu.py
git add "Babel_Client/Assets/Art/UI/MainMenu/bg_mainmenu.png"
git add "Babel_Client/Assets/Art/UI/MainMenu/logo_babel.png"
git commit -m "art: slice main menu background and logo from concept art"
```

---

## Task 2：用 gpt-image-2 生成按钮 Sprite

**Files:**
- Create: `H:/Babel/tools/gen_buttons.py`
- Output: `Babel_Client/Assets/Art/UI/MainMenu/btn_start.png`
- Output: `Babel_Client/Assets/Art/UI/MainMenu/btn_exit.png`

- [ ] **Step 1：写按钮生成脚本**

```python
# H:/Babel/tools/gen_buttons.py
import base64, os
from pathlib import Path
from openai import OpenAI

client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url="https://chat-test.q1.com/v1"
)
OUT = Path("H:/Babel/Babel_Client/Assets/Art/UI/MainMenu")
OUT.mkdir(parents=True, exist_ok=True)

buttons = [
    {
        "file": OUT / "btn_start.png",
        "prompt": (
            "A single UI button graphic for a mobile game, landscape orientation 4:1 ratio. "
            "Isolated on TRANSPARENT background - no scene, no characters, no text. "
            "Style: Clash of Clans quality juicy 3D button. "
            "Shape: wide rounded rectangle. "
            "Color: bright green-to-lime vertical gradient, top edge has white highlight shine strip. "
            "Border: thick golden/amber outline with small beveled edges. "
            "Bottom has darker green shadow for 3D raised depth. "
            "Small golden sparkle on top-right corner. "
            "The button looks pressable and satisfying. Clean game UI asset. "
            "No text on the button."
        )
    },
    {
        "file": OUT / "btn_exit.png",
        "prompt": (
            "A single UI button graphic for a mobile game, landscape orientation 4:1 ratio. "
            "Isolated on TRANSPARENT background - no scene, no characters, no text. "
            "Style: Clash of Clans quality button, secondary/less prominent. "
            "Shape: wide rounded rectangle, slightly smaller than main button. "
            "Color: dark grey-blue stone texture gradient. "
            "Border: medium thickness grey/silver outline. "
            "Subtle 3D raised effect, not as shiny as start button. "
            "Clean game UI asset. No text on the button."
        )
    },
]

for item in buttons:
    print(f"Generating {item['file'].name}...")
    response = client.images.generate(
        model="gpt-image-2",
        prompt=item["prompt"],
        size="1536x1024",  # landscape for button shape
        n=1,
    )
    data = response.data[0]
    if hasattr(data, "b64_json") and data.b64_json:
        item["file"].write_bytes(base64.b64decode(data.b64_json))
    elif hasattr(data, "url") and data.url:
        import urllib.request
        urllib.request.urlretrieve(data.url, item["file"])
    print(f"  Saved: {item['file']}")
print("Done.")
```

- [ ] **Step 2：运行脚本**

```bash
OPENAI_API_KEY="sk-c3dc41f246bf59f8e6db79b881cc955ee8a004b6" python "H:/Babel/tools/gen_buttons.py"
```

期望输出：
```
Generating btn_start.png...
  Saved: ...btn_start.png
Generating btn_exit.png...
  Saved: ...btn_exit.png
Done.
```

- [ ] **Step 3：验证图片存在**

```bash
python -c "
from PIL import Image
from pathlib import Path
base = Path('H:/Babel/Babel_Client/Assets/Art/UI/MainMenu')
for name in ['btn_start.png', 'btn_exit.png']:
    img = Image.open(base / name)
    print(name, img.size, img.mode)
"
```

- [ ] **Step 4：Commit**

```bash
git add H:/Babel/tools/gen_buttons.py
git add "Babel_Client/Assets/Art/UI/MainMenu/btn_start.png"
git add "Babel_Client/Assets/Art/UI/MainMenu/btn_exit.png"
git commit -m "art: generate main menu button sprites with gpt-image-2"
```

---

## Task 3：Unity 导入设置（TextureImporter via execute_code）

**Files:**
- Modify: 4 个 PNG 的 .meta 设定（通过 Unity MCP execute_code 设置）

- [ ] **Step 1：刷新 AssetDatabase 让 Unity 识别新文件**

使用 `refresh_unity` MCP tool：
```
refresh_unity(mode="force", scope="assets", wait_for_ready=true)
```

- [ ] **Step 2：将 bg_mainmenu 和 logo_babel 设为 Sprite (2D and UI)**

```csharp
// execute_code
var paths = new[] {
    "Assets/Art/UI/MainMenu/bg_mainmenu.png",
    "Assets/Art/UI/MainMenu/logo_babel.png",
    "Assets/Art/UI/MainMenu/btn_start.png",
    "Assets/Art/UI/MainMenu/btn_exit.png",
};
foreach (var path in paths)
{
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    if (importer == null) { UnityEngine.Debug.LogError($"Importer null: {path}"); continue; }
    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    importer.alphaIsTransparency = true;
    importer.mipmapEnabled = false;
    UnityEditor.EditorUtility.SetDirty(importer);
    importer.SaveAndReimport();
    UnityEngine.Debug.Log($"[BABEL][Import] Set Sprite: {path}");
}
return "Import settings applied";
```

- [ ] **Step 3：将两个按钮设置为 9-slice（可拉伸边框）**

```csharp
// execute_code
var btnPaths = new[] {
    "Assets/Art/UI/MainMenu/btn_start.png",
    "Assets/Art/UI/MainMenu/btn_exit.png",
};
foreach (var path in btnPaths)
{
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
    if (importer == null) continue;
    // 9-slice border: 左右各留 40px，上下各留 20px
    importer.spriteBorder = new UnityEngine.Vector4(40, 20, 40, 20);
    UnityEditor.EditorUtility.SetDirty(importer);
    importer.SaveAndReimport();
    UnityEngine.Debug.Log($"[BABEL][Import] 9-slice set: {path}");
}
return "9-slice applied";
```

- [ ] **Step 4：读 console 确认无报错**

```
read_console(types=["error","warning"], count=10)
```

期望：无 Import 相关错误

- [ ] **Step 5：Commit**

```bash
git add "Babel_Client/Assets/Art/UI/MainMenu/"
git commit -m "art: configure main menu sprite import settings in Unity"
```

---

## Task 4：重写 UIMainMenuPanel 使用 Sprite

**Files:**
- Modify: `Babel_Client/Assets/Scripts/UI/UIMainMenuPanel.cs`

保留所有按钮逻辑、`SetActionsForTests`、`StartGameFromMenu` 等不变，只替换 `BuildRuntimeLayout()` 及相关辅助方法。

- [ ] **Step 1：在文件顶部添加 Resources 路径常量**

在 `UIMainMenuPanel` 类体最上方添加：

```csharp
private const string BG_SPRITE_PATH       = "Art/UI/MainMenu/bg_mainmenu";
private const string LOGO_SPRITE_PATH     = "Art/UI/MainMenu/logo_babel";
private const string BTN_START_SPRITE_PATH= "Art/UI/MainMenu/btn_start";
private const string BTN_EXIT_SPRITE_PATH = "Art/UI/MainMenu/btn_exit";
```

- [ ] **Step 2：替换 `BuildRuntimeLayout()` 方法**

将原有 `BuildRuntimeLayout()` 替换为：

```csharp
private void BuildRuntimeLayout()
{
    ClearChildren();
    RectTransform panelRect = transform as RectTransform;
    if (panelRect != null) Stretch(panelRect);

    // 背景全屏插画
    Sprite bgSprite = Resources.Load<Sprite>(BG_SPRITE_PATH);
    CreateSpriteImage("MenuBackground", Stretch, bgSprite, Color.white, Image.Type.Simple);

    // Logo（上方 40% 居中，锚在顶部）
    Sprite logoSprite = Resources.Load<Sprite>(LOGO_SPRITE_PATH);
    CreateSpriteImage("LogoImage", ConfigureLogo, logoSprite, Color.white, Image.Type.Simple);

    // 副标题文字（Logo 下方）
    CreateText("MenuSubtitle", "阻止人类触及天庭",
        new Vector2(0f, 80f), new Vector2(360f, 52f), 24,
        new Color(1f, 0.86f, 0.58f, 1f));

    // 开始按钮
    Sprite startSprite = Resources.Load<Sprite>(BTN_START_SPRITE_PATH);
    CreateSpriteButton("StartButton", "开始游戏", new Vector2(0f, -96f),
        new Vector2(300f, 72f), startSprite, new Color(0.12f, 0.08f, 0.02f, 1f));

    // 退出按钮
    Sprite exitSprite = Resources.Load<Sprite>(BTN_EXIT_SPRITE_PATH);
    CreateSpriteButton("ExitButton", "退出游戏", new Vector2(0f, -186f),
        new Vector2(240f, 58f), exitSprite, new Color(0.85f, 0.82f, 0.80f, 1f));

    _handled = false;
}
```

- [ ] **Step 3：添加 `ConfigureLogo` 和 `CreateSpriteImage`、`CreateSpriteButton` 辅助方法**

在文件末尾（`QuitGame` 方法之前）插入：

```csharp
private static void ConfigureLogo(RectTransform rect)
{
    rect.anchorMin = new Vector2(0.5f, 1f);
    rect.anchorMax = new Vector2(0.5f, 1f);
    rect.pivot     = new Vector2(0.5f, 1f);
    rect.anchoredPosition = new Vector2(0f, -40f);
    rect.sizeDelta = new Vector2(420f, 160f);
}

private Image CreateSpriteImage(string name, Action<RectTransform> configure,
    Sprite sprite, Color tint, Image.Type imageType)
{
    var go = new GameObject(name, typeof(RectTransform), typeof(Image));
    go.transform.SetParent(transform, false);
    configure((RectTransform)go.transform);
    Image img = go.GetComponent<Image>();
    img.sprite = sprite;
    img.color  = sprite != null ? tint : tint * new Color(0.3f, 0.3f, 0.3f, 1f);
    img.type   = imageType;
    return img;
}

private Button CreateSpriteButton(string name, string label, Vector2 position,
    Vector2 size, Sprite sprite, Color textColor)
{
    var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
    go.transform.SetParent(transform, false);
    RectTransform rect = (RectTransform)go.transform;
    rect.anchorMin        = new Vector2(0.5f, 0.5f);
    rect.anchorMax        = new Vector2(0.5f, 0.5f);
    rect.pivot            = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = position;
    rect.sizeDelta        = size;
    Image img = go.GetComponent<Image>();
    img.sprite = sprite;
    img.type   = Image.Type.Sliced;
    img.color  = sprite != null ? Color.white : new Color(0.95f, 0.78f, 0.38f, 0.96f);
    CreateButtonLabel(rect, label, textColor);
    return go.GetComponent<Button>();
}
```

- [ ] **Step 4：更新 `CreateButtonLabel` 签名以接受文字颜色参数**

将原来的 `CreateButtonLabel(RectTransform buttonRect, string label)` 替换为：

```csharp
private static void CreateButtonLabel(RectTransform buttonRect, string label, Color textColor)
{
    var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
    textObject.transform.SetParent(buttonRect, false);
    RectTransform rect = (RectTransform)textObject.transform;
    Stretch(rect);
    Text text = textObject.GetComponent<Text>();
    text.text      = label;
    text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    text.alignment = TextAnchor.MiddleCenter;
    text.fontSize  = 26;
    text.fontStyle = FontStyle.Bold;
    text.color     = textColor;
}
```

- [ ] **Step 5：删除旧的纯色辅助方法（不再使用）**

删除 `CreateImage(string, Action<RectTransform>, Color)` 和旧 `CreateButton(string, string, Vector2)` 方法，以及 `ConfigureTower`、`ConfigureLightning` 方法。

> ⚠️ 确认 `BindButtons()` 中 `transform.Find("StartButton")` 和 `transform.Find("ExitButton")` 与新方法中的 name 参数完全一致（均为 "StartButton" / "ExitButton"）。

- [ ] **Step 6：等待编译完成，检查 console**

```
read_console(types=["error"], count=15, include_stacktrace=true)
```

期望：无编译错误

- [ ] **Step 7：Commit**

```bash
git add "Babel_Client/Assets/Scripts/UI/UIMainMenuPanel.cs"
git commit -m "feat: UIMainMenuPanel uses sprite assets instead of solid-color placeholders"
```

---

## Task 5：运行验证

- [ ] **Step 1：切换到 MainMenuScene 并进入 Play Mode**

```
execute_code: UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenuScene.unity");
manage_editor(action="play")
```

- [ ] **Step 2：等待 2 秒后截图**

```
sleep 2
manage_camera(action="screenshot", capture_source="game_view", include_image=true, max_resolution=768, screenshot_super_size=2)
```

- [ ] **Step 3：检查截图**

确认：
- 背景显示概念图全屏插画（非黑屏）
- Logo 区域显示 BABEL 图像
- 两个按钮可见且有 Sprite 样式
- 无白色方块或粉红 Missing 错误块

- [ ] **Step 4：检查 console 无运行时错误**

```
read_console(types=["error","warning"], count=10)
```

- [ ] **Step 5：退出 Play Mode**

```
manage_editor(action="stop")
```

- [ ] **Step 6：最终 Commit**

```bash
git add -A
git commit -m "feat: main menu art pass v1 — Metal Slug character concept implemented"
```

---

## Self-Review

**Spec coverage 检查：**
- ✅ 概念图切图 → Task 1 裁切背景+Logo
- ✅ 按钮 Sprite 生成 → Task 2 gpt-image-2 生成
- ✅ Unity 导入设置 → Task 3 TextureImporter
- ✅ UIMainMenuPanel 替换 → Task 4 代码重写
- ✅ 验证 → Task 5 运行截图确认

**类型一致性检查：**
- `CreateSpriteButton` 在 Task 4 Step 2 调用，在 Step 3 定义 ✅
- `CreateButtonLabel` 新签名在 Task 4 Step 3 和 Step 4 均为 `(RectTransform, string, Color)` ✅
- `BG_SPRITE_PATH` 等常量在 Step 1 定义，在 Step 2 使用 ✅
- `Resources.Load<Sprite>` 路径不含 "Assets/" 前缀（Unity Resources 规范）✅

**潜在风险：**
- gpt-image-2 生成的按钮背景不一定是真透明 PNG（API 可能返回白底）。Task 3 Step 2 已设 `alphaIsTransparency=true`，若仍有白底，在 Task 5 验证时视觉上会发现，届时需回到 Task 2 调整 prompt 加 "transparent background, PNG with alpha channel"。
