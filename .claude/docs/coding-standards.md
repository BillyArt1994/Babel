# Coding Standards - Unity C#

## Naming
- Classes/Methods: `PascalCase`
- Private fields: `_camelCase` with underscore
- Public fields/Properties: `PascalCase`
- Constants: `UPPER_SNAKE_CASE`

## Unity Specifics
- MonoBehaviour 生命周期方法按顺序排列
- 序列化字段使用 `[SerializeField]` 而非 public
- 使用 `[Header]` 和 `[Tooltip]` 提升 Inspector 可读性

## Performance
- 缓存 Transform、Rigidbody 等组件引用
- 避免在 Update 中使用 Find/GetComponent
- 字符串拼接使用 StringBuilder
- 使用对象池减少 GC 压力
