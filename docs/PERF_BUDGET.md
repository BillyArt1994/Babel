# PERF_BUDGET.md — Babel 性能预算基线

> 数据来源：`production/reports/s2-08-performance-report.md`（Sprint-2 静态分析）
> 需要在真实 100 单位同屏测试后更新实测值。

## 帧时间预算（目标 60fps = 16.67ms）

| 系统 | 预算 | Sprint-2 静态估算 | 状态 |
|------|------|-----------------|------|
| EnemyController.Update（100 units） | 4ms | 1.5–2.5ms | OK |
| Physics（Aura NonAlloc） | 4ms | 0.5–1ms（S3-01 优化后） | OK |
| Rendering（100 Sprites） | 4ms | 1–2ms | OK |
| SkillSystem / ClickAttack | 2ms | 0.5–1.5ms | OK |
| UI（GameHUD） | 1ms | 0.3ms | OK |
| 其他（Events, GameLoop） | 1.67ms | <0.5ms | OK |

## 内存预算（256MB 项目分配）

| 类别 | 预算 | 静态估算 | 状态 |
|------|------|---------|------|
| Enemy GameObjects（100 units） | 20MB | 5–10MB | OK |
| Object Pool 开销 | 5MB | <1MB | OK |
| 纹理（敌人 sprites） | 50MB | 10–30MB | OK |
| Physics Collider2D（100个） | 10MB | 2–5MB | OK |

## GC 预算

| 指标 | 阈值 | 说明 |
|------|------|------|
| GC spike（每帧） | < 2ms | 超过触发回归 |
| 热路径 GC 分配 | 0 | NonAlloc + ObjectPool 覆盖 |

---

## 验证命令（Unity MCP）

### 渲染统计
```
manage_graphics action=stats_get
```
关注：drawCalls、batches、triangles、frameTime

### 编译 + 控制台检查
```
refresh_unity → read_console types=["error","warning"]
```

### 性能基准（100 单位同屏）
1. 进入 Play Mode：`manage_editor action=play`
2. 在 Inspector 触发 PerformanceBenchmark（或快捷键）
3. 读取日志：`read_console filter_text="[PerformanceBenchmark]"`
4. 读取渲染统计：`manage_graphics action=stats_get`

### GC 验证
```
read_console filter_text="[BABEL][AC]" → 检查热路径任务的 AC log
```

---

## 更新策略

每次 Sprint 完成性能相关任务（S3-01~03、S3-11 等）后，用 PerformanceBenchmark 实测并更新"实测值"列。
静态估算在获得实测数据后可删除。
