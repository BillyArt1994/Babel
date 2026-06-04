using System;
using UnityEngine;
using QFramework;

namespace Babel
{
    public enum EnemyMoveState
    {
        MovingToBuildPoint,
        Building,
        MovingToPassage,
        ClimbingPassage,
        Finished
    }

    public partial class Enemy : ViewController, IDamageable
    {
        private const float HIT_FLASH_DURATION = 0.12f;
        private static readonly Color HIT_FLASH_COLOR = Color.red;

        public float HP = 15;
        public float MovementSpeed = 2.0f;
        public int buildAbility = 25;

        [HideInInspector] public Babel.Path currentPath;
        [HideInInspector] public int waveEventId = -1;

        private IEnemyMovement _movement;

        private IEnemyAbility _ability;
        private EnemyData _data;
        private float _maxHealth = 15f;
        private float _speedBuffTimer;
        private float _speedBuffMult = 1.0f;
        private SpriteRenderer _hitFlashRenderer;
        private Color _hitFlashOriginalColor = Color.white;
        private float _hitFlashTimer;
        private bool _isHitFlashing;
        private float _deathFeedbackTimer;
        private bool _isDying;
        private bool _deathCompleted;

        // Animator 驱动走路动画
        private Animator _animator;
        private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");
        private bool _lastIsMoving;

        public static event Action<int> OnChargesExhausted;

        // IDamageable
        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;
        public float EffectiveSpeed => MovementSpeed * _speedBuffMult;

        /// <summary>
        /// 当前生命值，供调试 UI 读取。
        /// </summary>
        public float CurrentHealth => HP;

        /// <summary>
        /// 初始最大生命值，供调试 UI 计算血条比例。
        /// </summary>
        public float MaxHealth => _maxHealth;

        /// <summary>
        /// 当前生命值比例 [0, 1]。
        /// </summary>
        public float HealthPercent => _maxHealth > 0f ? Mathf.Clamp01(HP / _maxHealth) : 0f;

        private void Awake()
        {
            _maxHealth = Mathf.Max(HP, 1f);
            // 缓存 Animator（挂在根节点，找不到也不崩溃）
            _animator = GetComponent<Animator>();
            ResolveHitFlashRenderer();
            EnsureDebugHealthBar();
        }

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive) return;
            HP -= damage;
            if (damage > 0f)
            {
                StartHitFlash();
            }
        }

        /// <summary>
        /// 推进受击闪红计时，并在结束时恢复原色。
        /// </summary>
        /// <param name="deltaTime">经过时间。</param>
        public void TickHitFlash(float deltaTime)
        {
            if (!_isHitFlashing || _hitFlashRenderer == null)
            {
                return;
            }

            _hitFlashTimer -= deltaTime;
            if (_hitFlashTimer > 0f)
            {
                return;
            }

            _hitFlashRenderer.color = _hitFlashOriginalColor;
            _isHitFlashing = false;
        }

        /// <summary>
        /// 推进致死受击反馈计时，结束后执行死亡结算。
        /// </summary>
        /// <param name="deltaTime">经过时间。</param>
        public void TickDeathFeedback(float deltaTime)
        {
            if (HP > 0f)
            {
                return;
            }

            if (!_isDying)
            {
                BeginDeathFeedback();
                return;
            }

            _deathFeedbackTimer -= Mathf.Max(0f, deltaTime);
            if (_deathFeedbackTimer > 0f)
            {
                return;
            }

            CompleteDeath();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            HP += amount;
        }

        public void ApplySpeedBuff(float mult, float duration)
        {
            _speedBuffMult = Mathf.Max(_speedBuffMult, mult);
            _speedBuffTimer = Mathf.Max(_speedBuffTimer, duration);
        }

        public void Init(Babel.Path startPath, EnemyData data, int eventId)
        {
            _data = data;
            HP = data.Hp;
            _maxHealth = Mathf.Max(data.Hp, 1f);
            MovementSpeed = data.MoveSpeed;
            buildAbility = data.BuildContribution;
            currentPath = startPath;
            waveEventId = eventId;
            _speedBuffTimer = 0;
            _speedBuffMult = 1.0f;
            _deathFeedbackTimer = 0f;
            _isDying = false;
            _deathCompleted = false;

            // 按 MoveMode 选移动策略
            _movement?.OnRemoved();
            _movement = data.MoveMode switch
            {
                "scout"   => new BuilderMovement(GatewayFirstSelector.Instance),
                "support" => new SupportMovement(),
                "builder" => new BuilderMovement(DefaultBuildSelector.Instance),
                _         => new BuilderMovement(DefaultBuildSelector.Instance) // 空/未知拼写兜底为随机建造者
            };
            _movement.Init(this, data);

            // Ability
            _ability?.OnRemoved();
            _ability = data.AbilityType switch
            {
                "heal_aura" => new HealAura(),
                "speed_aura" => new SpeedAura(),
                _ => null
            };
            _ability?.Init(this, data);

            // 重置 Animator 走路状态
            _lastIsMoving = false;
            if (_animator != null)
            {
                _animator.SetBool(AnimIsMoving, false);
            }
        }

        private void Update()
        {
            if (GameSession.IsGameEnded)
            {
                return;
            }

            TickHitFlash(Time.deltaTime);

            // Death check
            if (HP <= 0)
            {
                TickDeathFeedback(Time.unscaledDeltaTime);
                return;
            }

            // Ability tick
            _ability?.Tick(Time.deltaTime);

            // Speed buff tick
            if (_speedBuffTimer > 0)
            {
                _speedBuffTimer -= Time.deltaTime;
                if (_speedBuffTimer <= 0) _speedBuffMult = 1.0f;
            }

            // 驱动 Animator IsMoving 参数
            UpdateAnimatorState();

            _movement?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 供 BuilderMovement.ExecuteFinished 调用，触发 charges 耗尽事件并销毁。
        /// </summary>
        internal void NotifyChargesExhausted()
        {
            _ability?.OnRemoved();
            _ability = null;
            if (waveEventId >= 0)
                OnChargesExhausted?.Invoke(waveEventId);
            this.DestroyGameObjGracefully();
        }

        /// <summary>
        /// 根据移动状态驱动 Animator 的 IsMoving 参数。
        /// 只在值发生变化时调用 SetBool，避免每帧写参数的开销。
        /// </summary>
        private void UpdateAnimatorState()
        {
            bool moving = _movement?.IsMoving ?? false;

            if (_animator != null && moving != _lastIsMoving)
            {
                _animator.SetBool(AnimIsMoving, moving);
                _lastIsMoving = moving;
            }
        }

        private void StartHitFlash()
        {
            if (_hitFlashRenderer == null)
            {
                ResolveHitFlashRenderer();
            }

            if (_hitFlashRenderer == null)
            {
                return;
            }

            if (!_isHitFlashing)
            {
                _hitFlashOriginalColor = _hitFlashRenderer.color;
            }

            _hitFlashRenderer.color = HIT_FLASH_COLOR;
            _hitFlashTimer = HIT_FLASH_DURATION;
            _isHitFlashing = true;
        }

        private void BeginDeathFeedback()
        {
            _isDying = true;
            _deathFeedbackTimer = HIT_FLASH_DURATION;
            _movement?.OnRemoved();
            _ability?.OnRemoved();
            _ability = null;
        }

        private void CompleteDeath()
        {
            if (_deathCompleted)
            {
                return;
            }

            _deathCompleted = true;
            StatsTracker.RecordKill();
            EnemyEvents.RaiseEnemyDied(Position);
            if (waveEventId >= 0)
            {
                OnChargesExhausted?.Invoke(waveEventId);
            }

            float expAmount = _data != null ? _data.ExpReward : 1f;
            if (XpSystem.Instance != null)
                XpSystem.Instance.GainXp(expAmount);
            else
                Debug.LogWarning("[BABEL][Enemy] XpSystem.Instance 为空，经验未结算");
            DestroyAfterDeath();
        }

        private void DestroyAfterDeath()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(gameObject);
                return;
            }
#endif
            this.DestroyGameObjGracefully();
        }

        private void ResolveHitFlashRenderer()
        {
            if (Circle != null)
            {
                _hitFlashRenderer = Circle;
                return;
            }

            _hitFlashRenderer = GetComponent<SpriteRenderer>();
            if (_hitFlashRenderer != null)
            {
                return;
            }

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].GetComponentInParent<DebugWorldBar>() == null)
                {
                    _hitFlashRenderer = renderers[i];
                    return;
                }
            }
        }

        private void EnsureDebugHealthBar()
        {
            if (GetComponentInChildren<DebugHealthBar>(true) != null)
            {
                return;
            }

            var barObject = new GameObject("DebugHealthBar");
            barObject.transform.SetParent(transform, false);
            barObject.AddComponent<DebugHealthBar>().Init(this);
        }
    }
}
