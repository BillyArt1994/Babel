# Technical Preferences - Unity

## Engine
- **Engine**: Unity 2022.3.73f1
- **Version**: 2022.3.73f1 (LTS)
- **Language**: C#
- **Scripting Backend**: Mono / IL2CPP
- **Rendering Pipeline**: URP (Universal Render Pipeline) - 推荐用于 2D 游戏

## Code Style
- 使用 C# 命名约定（PascalCase for public, camelCase for private）
- 优先使用组件模式而非继承
- 避免在 Update() 中进行重复计算
- 使用对象池管理频繁创建/销毁的对象

## Architecture
- 使用 ScriptableObjects 存储配置数据
- 事件驱动架构（UnityEvents 或自定义事件系统）
- 单一职责原则：每个脚本只做一件事

## Performance
- 缓存组件引用（避免重复 GetComponent）
- 使用协程处理异步操作
- 注意 GC 分配，避免频繁装箱

## Performance Budgets（性能预算）

| 指标 | 目标值 | 说明 |
|------|--------|------|
| 目标帧率 | 60 fps（稳定） | PC 主平台，允许瞬间波动至 55fps |
| 最大帧时间 | ≤ 16.7ms | 对应 60fps |
| 最大同屏敌人数 | 200 单位 | 超过时启用对象池限制生成 |
| 最大同屏粒子数 | 500 粒子 | 单个神力技能特效上限约 100 |
| 内存占用（运行时） | ≤ 512MB | PC 低配目标（4GB 总 RAM 机器） |
| GC 分配（每帧） | ≤ 1KB（稳定游戏状态） | 技能触发帧允许临时超出 |
| Update() 单脚本帧耗时 | ≤ 1ms（主要系统） | 超出时需 Profiler 分析 |
