---
name: exec-plan
description: 基于已有研究或任务描述，生成 Babel/Unity 可执行的分阶段实施计划；不直接写代码。
argument-hint: "[task-id 或任务描述]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

# Execution Plan — Babel/Unity

## Goal
把任务拆成可独立验收的 phases，每个 phase 结束都有明确证据。

## Required behavior
1. 读取相关 Sprint 任务描述（`production/sprints/sprint-N.md`）。
2. 扫描受影响的代码文件，理解现有结构。
3. 拆成独立 phases（建议 2-4 个），每个 phase 必须：
   - 有明确目标
   - 列出将修改的文件
   - 有验证方法（见下方验证工具链）
   - 有回滚说明
4. 输出计划，不写代码。

## 验证工具链（按顺序）
1. **编译验证**：创建/修改脚本后调用 `refresh_unity` + `read_console` 检查 errors
2. **运行时验证**：`manage_editor action=play` 进入播放模式
3. **日志验证**：`read_console filter_text="[BABEL][AC][task-id]"` 读取验收日志
4. **测试验证**（如有）：`run_tests` + `get_test_job`
5. **性能验证**（热路径改动）：`manage_graphics action=stats_get`

## Output format
1. Executive summary（一句话）
2. 受影响文件清单
3. Phase 1 / 2 / 3 ...（每个 phase 含：目标、改动文件、验证方式、回滚方式）
4. 验证计划（AC log 预期输出）
5. 风险与未知点
