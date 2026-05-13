using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 临时敌人生成器，用 Instantiate 走通刷怪流程；正式对象池完成后应替换。
    /// </summary>
    public sealed class TransientEnemyPool : IEnemyPool
    {
        private const float FALLBACK_RADIUS = 0.3f;
        private const int FALLBACK_SORTING_ORDER = 10;

        private readonly List<GameObject> _activeEnemies = new();
        private readonly HashSet<string> _missingPrefabWarnings = new();

        /// <summary>
        /// 当前仍存活或活跃的敌人数量。
        /// </summary>
        public int ActiveCount
        {
            get
            {
                PruneInactiveEntries();
                return _activeEnemies.Count;
            }
        }

        /// <summary>
        /// 生成一个敌人实例并放置到指定位置。
        /// </summary>
        /// <param name="enemyId">敌人 CSV ID。</param>
        /// <param name="position">生成世界坐标。</param>
        /// <returns>生成出的敌人 GameObject；配置错误时返回 null。</returns>
        public GameObject Get(string enemyId, Vector2 position)
        {
            EnemyData data = EnemyDatabase.GetById(enemyId);
            if (data == null)
            {
                Debug.LogWarning($"[BABEL][TransientEnemyPool] Unknown enemyId '{enemyId}'");
                return null;
            }

            GameObject enemyObject = CreateEnemyObject(data, position);
            ConfigureEnemyObject(enemyObject, data);
            _activeEnemies.Add(enemyObject);
            return enemyObject;
        }

        /// <summary>
        /// 临时销毁敌人；正式对象池接入后改为归还池。
        /// </summary>
        /// <param name="enemy">需要回收的敌人对象。</param>
        public void Return(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            _activeEnemies.Remove(enemy);
            Object.Destroy(enemy);
        }

        private GameObject CreateEnemyObject(EnemyData data, Vector2 position)
        {
            GameObject prefab = LoadPrefab(data.Prefab);
            GameObject enemyObject = prefab != null
                ? Object.Instantiate(prefab, position, Quaternion.identity)
                : CreateFallbackObject(data, position);

            enemyObject.name = $"Enemy_{data.EnemyId}";
            enemyObject.transform.position = position;
            enemyObject.SetActive(true);
            return enemyObject;
        }

        private GameObject LoadPrefab(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null && _missingPrefabWarnings.Add(resourcePath))
            {
                Debug.LogWarning($"[BABEL][TransientEnemyPool] Missing prefab Resources/{resourcePath}; using fallback visual");
            }
            return prefab;
        }

        private static GameObject CreateFallbackObject(EnemyData data, Vector2 position)
        {
            var enemyObject = new GameObject($"Enemy_{data.EnemyId}");
            enemyObject.transform.position = position;

            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFallbackSprite();
            renderer.color = GetFallbackColor(data.EnemyId);
            renderer.sortingOrder = FALLBACK_SORTING_ORDER;
            enemyObject.AddComponent<TransientEnemyVisualCleanup>().Init(renderer.sprite);
            enemyObject.transform.localScale = Vector3.one * 0.6f;
            return enemyObject;
        }

        private static void ConfigureEnemyObject(GameObject enemyObject, EnemyData data)
        {
            if (enemyObject.GetComponent<Enemy>() == null)
            {
                enemyObject.AddComponent<Enemy>();
            }

            CircleCollider2D collider = enemyObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = enemyObject.AddComponent<CircleCollider2D>();
            }
            collider.radius = FALLBACK_RADIUS;

            Rigidbody2D body = enemyObject.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = enemyObject.AddComponent<Rigidbody2D>();
            }
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                enemyObject.layer = enemyLayer;
            }
        }

        private static Sprite CreateFallbackSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Color GetFallbackColor(string enemyId)
        {
            return enemyId switch
            {
                "elite" => Color.magenta,
                "priest" => Color.cyan,
                "engineer" => Color.yellow,
                "zealot" => Color.red,
                _ => Color.white
            };
        }

        private void PruneInactiveEntries()
        {
            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject enemy = _activeEnemies[i];
                if (enemy == null || !enemy.activeInHierarchy)
                {
                    _activeEnemies.RemoveAt(i);
                }
            }
        }
    }

    internal sealed class TransientEnemyVisualCleanup : MonoBehaviour
    {
        private Sprite _sprite;

        public void Init(Sprite sprite)
        {
            _sprite = sprite;
        }

        private void OnDestroy()
        {
            if (_sprite == null)
            {
                return;
            }

            Texture2D texture = _sprite.texture;
            DestroyUnityObject(_sprite);
            if (texture != null)
            {
                DestroyUnityObject(texture);
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
