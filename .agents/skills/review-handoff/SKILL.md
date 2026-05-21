---
name: review-handoff
description: 对已完成的 Babel Sprint 任务做结构化验收和交接，生成证据链、风险说明和下一步建议。
argument-hint: "[task-id]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

# Review Handoff — Babel/Unity

## Goal
把"做了什么、证据是什么、还有什么风险"整理成可审阅、可归档的交付物。

## Required behavior
1. 回顾 Sprint 任务的验收标准（`production/sprints/sprint-N.md`）。
2. 逐条对照验收标准，给出证据：
   - 代码文件 + 行号
   - `[BABEL][AC][task-id]` 日志输出
   - 编译/测试结果
3. 说明性能影响：已测 / 未测 / 推断（标明依据）。
4. 说明行为一致性影响（是否改变了已有系统的接口或事件流）。
5. 列出已知风险和边界条件。
6. 给出下一阶段建议（不把建议写成"已完成"）。

## Output format
```
## Review Handoff — [task-id]: [任务名]
**完成日期**：YYYY-MM-DD

### 验收标准对照
| 标准 | 状态 | 证据 |
|------|------|------|
| ... | ✅/❌/⚠️ | 文件:行 or AC log |

### 改动文件清单
- `Assets/Scripts/...` — 说明改了什么

### AC 日志证据
（粘贴 read_console 中的 [BABEL][AC][task-id] 输出）

### 性能影响
- 状态：已测 / 未测 / 推断
- 说明：...

### 行为一致性
- 是否改变现有接口：是/否
- 影响系统：...

### 已知风险
- ...

### 下一步建议
- ...
```
