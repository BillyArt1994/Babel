/// <summary>
/// Concrete pool for EnemyController.
/// Unity cannot add or serialize a raw generic MonoBehaviour (ObjectPool&lt;T&gt;) directly;
/// a non-generic subclass is required for AddComponent, Inspector wiring, and scene serialization.
/// </summary>
public class EnemyControllerPool : ObjectPool<EnemyController> { }
