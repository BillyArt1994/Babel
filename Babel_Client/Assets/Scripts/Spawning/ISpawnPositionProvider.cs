using UnityEngine;

namespace Babel
{
    public interface ISpawnPositionProvider
    {
        Vector2 GetSpawnPosition(string spawnPointId);
    }
}
