using System;
using System.Collections.Generic;
using Babel.Unity.Infrastructure.Content;
using Babel.Unity.Infrastructure.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Babel
{
    /// <summary>
    /// Legacy IEnemyPool adapter backed by one GameObjectPool per authored human id.
    /// Prefab and sizing data come from GameContentManifest; an explicit visible runtime
    /// template keeps EditMode and degraded-content startup functional without path lookup.
    /// </summary>
    public sealed class TransientEnemyPool : IEnemyPool, IDisposable
    {
        private const float FALLBACK_RADIUS = 0.5f;
        private const int FALLBACK_SORTING_ORDER = 10;
        private const int DEFAULT_PREWARM = 0;
        private const int DEFAULT_CAPACITY = 32;

        private sealed class FallbackVisualResource
        {
            public FallbackVisualResource(Sprite sprite, Texture2D texture)
            {
                Sprite = sprite;
                Texture = texture;
            }

            public Sprite Sprite { get; }
            public Texture2D Texture { get; }
        }

        private readonly Dictionary<string, GameObjectPool> _pools =
            new Dictionary<string, GameObjectPool>(StringComparer.Ordinal);
        private readonly Dictionary<GameObject, GameObjectPool> _owners =
            new Dictionary<GameObject, GameObjectPool>();
        private readonly List<GameObject> _activeEnemies = new List<GameObject>();
        private readonly List<GameObject> _templates = new List<GameObject>();
        private readonly List<FallbackVisualResource> _fallbackVisuals =
            new List<FallbackVisualResource>();
        private readonly HashSet<string> _missingPrefabWarnings =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _missingConfigWarnings =
            new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;

        /// <summary>Current checked-out enemy count across all human-id pools.</summary>
        public int ActiveCount
        {
            get
            {
                if (_disposed) return 0;
                PruneInactiveEntries();
                return _activeEnemies.Count;
            }
        }

        public GameObject Get(string enemyId, Vector2 position)
        {
            ThrowIfDisposed();

            EnemyData data = EnemyDatabase.GetById(enemyId);
            if (data == null)
            {
                Debug.LogWarning($"[BABEL][TransientEnemyPool] Unknown enemyId '{enemyId}'");
                return null;
            }

            GameObjectPool pool = GetOrCreatePool(data);
            GameObject enemyObject = pool.Get();

            try
            {
                enemyObject.name = $"Enemy_{data.EnemyId}";
                enemyObject.transform.position = position;

                Enemy enemy = enemyObject.GetComponent<Enemy>();
                if (enemy == null)
                    throw new InvalidOperationException($"Pooled human view '{data.EnemyId}' has no Enemy component.");

                enemy.BindPoolReturn(Return);
                _owners.Add(enemyObject, pool);
                _activeEnemies.Add(enemyObject);
                return enemyObject;
            }
            catch
            {
                if (pool.Owns(enemyObject))
                    pool.Return(enemyObject);
                throw;
            }
        }

        public void Return(GameObject enemy)
        {
            if (_disposed || enemy == null) return;

            if (!_owners.TryGetValue(enemy, out GameObjectPool pool))
            {
                Debug.LogWarning($"[Babel][TransientEnemyPool] Ignored return for unowned object '{enemy.name}'.");
                return;
            }

            _activeEnemies.Remove(enemy);
            _owners.Remove(enemy);
            pool.Return(enemy);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _activeEnemies.Clear();
            _owners.Clear();

            foreach (GameObjectPool pool in _pools.Values)
                pool.Dispose();
            _pools.Clear();

            for (int i = 0; i < _templates.Count; i++)
            {
                if (_templates[i] != null)
                    DestroyUnityObject(_templates[i]);
            }
            _templates.Clear();

            for (int i = 0; i < _fallbackVisuals.Count; i++)
            {
                FallbackVisualResource resource = _fallbackVisuals[i];
                if (resource.Sprite != null) DestroyUnityObject(resource.Sprite);
                if (resource.Texture != null) DestroyUnityObject(resource.Texture);
            }
            _fallbackVisuals.Clear();
        }

        private GameObjectPool GetOrCreatePool(EnemyData data)
        {
            if (_pools.TryGetValue(data.EnemyId, out GameObjectPool existing))
                return existing;

            GameObject template = CreateTemplate(data);
            PoolConfig config = ResolvePoolConfig(data.EnemyId);
            var pool = new GameObjectPool(
                data.EnemyId,
                template,
                config.Prewarm,
                config.ExpectedCapacity,
                config.AllowExpansion);

            _pools.Add(data.EnemyId, pool);
            return pool;
        }

        private GameObject CreateTemplate(EnemyData data)
        {
            GameObject source = LoadPrefab(data.EnemyId);
            GameObject template = source != null
                ? Object.Instantiate(source)
                : CreateFallbackTemplate(data);

            template.name = $"[PoolTemplate] Enemy_{data.EnemyId}";
            template.SetActive(false);
            ConfigureEnemyTemplate(template);
            _templates.Add(template);
            return template;
        }

        private GameObject LoadPrefab(string enemyId)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) return null;

            if (GameContentRegistry.TryGetHumanView(enemyId, out GameObject prefab) && prefab != null)
                return prefab;

            if (_missingPrefabWarnings.Add(enemyId))
            {
                Debug.LogWarning(
                    $"[Babel][TransientEnemyPool] Manifest view missing for '{enemyId}'; using visible fallback visual.");
            }

            return null;
        }

        private PoolConfig ResolvePoolConfig(string enemyId)
        {
            GameContentManifest manifest = GameContentRegistry.Current;
            if (manifest != null && manifest.TryGetPoolConfig(enemyId, out PoolConfig configured))
            {
                int capacity = Mathf.Max(1, configured.ExpectedCapacity);
                int prewarm = Mathf.Clamp(configured.Prewarm, 0, capacity);
                return new PoolConfig(prewarm, capacity, configured.AllowExpansion);
            }

            if (manifest != null && _missingConfigWarnings.Add(enemyId))
            {
                Debug.LogWarning(
                    $"[Babel][TransientEnemyPool] Pool config missing for '{enemyId}'; using safe runtime defaults.");
            }

            return new PoolConfig(DEFAULT_PREWARM, DEFAULT_CAPACITY, true);
        }

        private GameObject CreateFallbackTemplate(EnemyData data)
        {
            var enemyObject = new GameObject($"Enemy_{data.EnemyId}");

            Sprite sprite = CreateFallbackSprite(out Texture2D texture);
            _fallbackVisuals.Add(new FallbackVisualResource(sprite, texture));

            SpriteRenderer renderer = enemyObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = GetFallbackColor(data.EnemyId);
            renderer.sortingOrder = FALLBACK_SORTING_ORDER;
            enemyObject.transform.localScale = Vector3.one * 0.6f;
            return enemyObject;
        }

        private static void ConfigureEnemyTemplate(GameObject enemyObject)
        {
            if (enemyObject.GetComponent<Enemy>() == null)
                enemyObject.AddComponent<Enemy>();

            CircleCollider2D collider = enemyObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = enemyObject.AddComponent<CircleCollider2D>();
                collider.radius = FALLBACK_RADIUS;
            }

            Rigidbody2D body = enemyObject.GetComponent<Rigidbody2D>();
            if (body == null)
                body = enemyObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                enemyObject.layer = enemyLayer;
        }

        private static Sprite CreateFallbackSprite(out Texture2D texture)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);
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
                if (enemy != null && enemy.activeInHierarchy) continue;

                _activeEnemies.RemoveAt(i);
                if (enemy == null || !_owners.TryGetValue(enemy, out GameObjectPool pool)) continue;

                _owners.Remove(enemy);
                if (pool.Owns(enemy))
                    pool.Return(enemy);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransientEnemyPool));
        }

        private static void DestroyUnityObject(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
