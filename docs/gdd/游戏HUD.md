# 游戏 HUD

> **状态**: 设计中
> **作者**: 用户 + Claude
> **最后更新**: 2026-03-29
> **实现支柱**: 支柱 2（压力递增的绝望感）、支柱 5（简洁的复杂性）

## 概述

游戏 HUD 是游戏进行中的常驻信息界面，以最低视觉噪音显示三类核心信息：剩余时间（MM:SS 倒计时）、累计击杀数、以及技能状态（当前点击形态图标 + 冷却进度条 + 已装备被动小图标列表）。HUD 不显示塔的精确建造进度——玩家需通过场景中塔的视觉造型模糊判断，刻意制造的信息不完整感是紧张感的来源之一。HUD 在游戏进行中始终可见，暂停/结算时隐藏或淡出。

## 玩家幻想

HUD 是"无声的压力放大器"。倒计时的数字在最后 60 秒变红提醒玩家时间紧迫；击杀数的不断增长给予持续的正向反馈；冷却进度条让玩家知道"再等一下就能出大招"。信息够用，但不冗余——玩家应该感受到"全局我大概掌握，但细节我摸不透"的张力，而不是对每个数字了如指掌的安全感。服务支柱 5（简洁的复杂性）和支柱 2（压力递增的绝望感）。

## 详细设计

### 核心规则

**HUD 模块：**

| 模块 | 数据来源 | 更新方式 | 位置 |
|------|---------|---------|------|
| 倒计时 | `GameLoopManager.GetRemainingTime()` | 每帧轮询，格式化为 MM:SS | 屏幕顶部居中 |
| 击杀数 | `StatsTracker.GetKillCount()` | 订阅 `OnKillCountChanged` 事件 | 屏幕顶部一角 |
| 点击形态图标 | `SkillSystem.GetActiveClickForm()` | 订阅 `OnClickFormChanged` 事件 | 屏幕底部中央 |
| 冷却进度条 | `SkillSystem.GetCooldownProgress()` | 每帧轮询，[0→1，1 = 完全冷却，0 = 可用] | 点击形态图标下方 |
| 被动图标列表 | `SkillSystem.GetPassives()` | 订阅 `OnPassiveAdded` 事件 | 点击形态图标两侧 |

**显示规则：**
1. 游戏开始时（`OnGameStart`），HUD 全部显示
2. 游戏暂停/结算时（`OnGamePaused` / `OnVictory` / `OnDefeat`），HUD 隐藏
3. 倒计时 ≤ 60 秒时，时间数字变为警告色（红色或橙色）
4. 冷却进度条满（`GetCooldownProgress() <= 0`，即技能可用）时，图标高亮/脉冲提示

### 状态与转换

| 状态 | HUD 显示 | 进入条件 | 退出条件 |
|------|---------|---------|---------|
| 隐藏 | 全部不可见 | 游戏开始前 / 暂停 / 结算 | `OnGameStart` / `OnGameResumed` |
| 显示中 | 全部元素可见，持续更新 | `OnGameStart` / `OnGameResumed` | `OnGamePaused` / `OnVictory` / `OnDefeat` |

### 与其他系统的交互

| 系统 | 方向 | 接口 |
|------|------|------|
| 游戏循环管理器 | 输入 ← | `GetRemainingTime()`（每帧轮询）；监听 `OnGameStart`、`OnGamePaused`、`OnGameResumed`、`OnVictory`、`OnDefeat` |
| 技能系统 | 输入 ← | `GetActiveClickForm()`；`GetCooldownProgress()`（每帧轮询）；`GetPassives()` |
| StatsTracker | 输入 ← | `GetKillCount()`；监听 `OnKillCountChanged` 事件 |

> **注：** StatsTracker 是一个轻量全局单例，订阅 `EnemyEvents.OnEnemyDied` 自行计数，不属于已定义的 20 个系统，实现时随 HUD 一起创建。结算 UI（系统 16）也从 StatsTracker 读取数据。

## 公式

1. **时间格式化（MM:SS）：**
   ```
   minutes = floor(remainingTime / 60)
   seconds = floor(remainingTime mod 60)
   display = Format("{0:D2}:{1:D2}", minutes, seconds)
   ```

2. **倒计时警告阈值：**
   ```
   isWarning = remainingTime <= WARNING_THRESHOLD    // 默认 60 秒
   ```
   警告时，时间文字颜色切换为警告色（红色/橙色）

3. **冷却进度条填充比例：**
   ```
   fillAmount = SkillSystem.GetCooldownProgress()   // 1 = 刚开冷却（满），0 = 冷却完毕（空）
   ```
   进度条为"消耗型"——随时间递减，清空后图标亮起表示可用

## 边缘情况

1. **倒计时显示负数**
   - 处理：`remainingTime = max(0, GetRemainingTime())`，格式化前钳制
   - 原因：防止出现 "-0:01" 的异常显示

2. **击杀数超过显示宽度（如 99999+）**
   - 处理：超过 5 位时显示为 "99999+"，不撑坏布局
   - 原因：防御性 UI，极端玩家可能刷到高击杀数

3. **被动列表超过最大显示槽位**
   - 处理：最多显示 `MAX_PASSIVE_SLOTS`（默认 8）个小图标，超出时最早装备的图标靠后隐藏
   - 原因：屏幕空间有限

4. **游戏刚开始时 `GetPassives()` 返回空列表**
   - 处理：被动区域留空，无异常显示
   - 原因：首局开始时无被动，属正常状态

5. **技能切换瞬间图标更新**
   - 处理：图标直接替换，无过渡动画
   - 原因：符合支柱 5 的简洁原则，不为 HUD 添加动画复杂度

## 依赖关系

**上游依赖（此系统依赖的系统）：**

| 系统 | 依赖类型 | 接口 |
|-----|---------|------|
| 游戏循环管理器 | 硬依赖 | `GetRemainingTime()`；`OnGameStart`、`OnGamePaused`、`OnGameResumed`、`OnVictory`、`OnDefeat` 事件 |
| 技能系统 | 硬依赖 | `GetActiveClickForm()`；`GetCooldownProgress()`；`GetPassives()` |
| StatsTracker（附属组件） | 硬依赖 | `GetKillCount()`；`OnKillCountChanged` 事件 |

**下游依赖（依赖此系统的系统）：** 无（终端系统）

## 调优参数

| 参数名 | 默认值 | 安全范围 | 说明 |
|--------|--------|---------|------|
| `WARNING_THRESHOLD` | 60 秒 | [30, 120] | 倒计时低于此值时时间文字变色，触发紧张感 |
| `MAX_PASSIVE_SLOTS` | 8 | [4, 16] | HUD 被动图标区最大显示数量 |

## 视觉/音频需求

无特殊音频需求。视觉上：倒计时警告色为红色（RGB: #FF4444 或类似）；冷却可用时图标脉冲效果（闪烁 1-2 次）。具体色值和动画参数由美术确定。

## UI 需求

**布局规划（概念，具体由 UI 实现时调整）：**

```
┌────────────────────────────────────────┐
│  [击杀: 0]          [15:00]            │  ← 顶部栏
│                                        │
│         [游戏主场景]                    │
│                                        │
│   [被动图标...] [当前技能图标] [被动图标...]  │
│               [冷却进度条]             │  ← 底部技能栏
└────────────────────────────────────────┘
```

- 所有 HUD 元素使用 Unity Canvas（Screen Space - Overlay）
- 倒计时字体较大（压迫感），击杀数字体较小（次要信息）
- 技能图标建议尺寸：64×64px；被动小图标：32×32px

## 验收标准

**功能测试：**
1. 游戏开始时 HUD 显示，初始倒计时为 "15:00"，击杀数为 "0"
2. 每帧倒计时正确递减，格式始终为 MM:SS，不出现负数
3. 倒计时 ≤ 60 秒时，时间文字颜色切换为警告色
4. 击杀敌人后，击杀数立即更新
5. 技能切换后，点击形态图标立即更新
6. 冷却期间进度条从满到空，清空时图标高亮
7. 装备新被动后，被动列表立即追加对应图标
8. 游戏暂停时 HUD 隐藏，恢复后 HUD 重新显示且数据无丢失

**边缘情况测试：**
9. `remainingTime` 格式化后不出现负数或非法字符
10. 被动图标超过 8 个时，HUD 布局不发生破坏

**性能标准：**
11. HUD `Update()` 每帧耗时 ≤ 0.5ms

## 待解决问题

1. **StatsTracker 的归属**：StatsTracker 未在 20 个系统中定义，实现时需确认它是独立 MonoBehaviour、GameLoopManager 的内部模块，还是其他方案。建议作为 GameLoopManager 的子组件，随游戏开始/结束自动初始化/清空。
2. **被动图标素材**：被动小图标需要美术资源，MVP 阶段可用纯色方块占位。
3. **HUD 是否需要在升级选择（LevelingUp 状态）时隐藏**：当前设计为暂停时隐藏，但升级选择 UI 弹出时 HUD 是否也应隐藏？建议升级选择 UI 覆盖 HUD 但不主动隐藏 HUD，由 Canvas 层级决定遮挡关系。
