namespace Babel.Unity.Infrastructure.Pooling
{
    /// <summary>
    /// Optional lifecycle contract for components hosted by a pooled GameObject.
    /// Implementations must restore all transient presentation state in these methods.
    /// </summary>
    public interface IPooledView
    {
        /// <summary>Called immediately before the pooled GameObject is activated.</summary>
        void ResetForSpawn();

        /// <summary>Called immediately before the pooled GameObject is deactivated.</summary>
        void ResetForDespawn();
    }
}
