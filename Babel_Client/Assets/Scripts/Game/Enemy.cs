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
        public int buildCharges = 1;

        [HideInInspector] public Babel.Path currentPath;
        [HideInInspector] public int waveEventId = -1;

        private EnemyMoveState _moveState = EnemyMoveState.MovingToBuildPoint;
        private int _targetBuildPointIndex = -1;
        private Transform _passageTarget;
        private float _buildTimer;

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
            buildCharges = data.BuildCharges;
            currentPath = startPath;
            waveEventId = eventId;
            _moveState = EnemyMoveState.MovingToBuildPoint;
            _targetBuildPointIndex = -1;
            _buildTimer = 0;
            _speedBuffTimer = 0;
            _speedBuffMult = 1.0f;
            _deathFeedbackTimer = 0f;
            _isDying = false;
            _deathCompleted = false;
            ReserveNextTarget();

            // Ability
            _ability?.OnRemoved();
            _ability = data.AbilityType switch
            {
                "heal_aura" => new HealAura(),
                "speed_aura" => new SpeedAura(),
                _ => null
            };
            _ability?.Init(this, data);
        }

        private void Update()
        {
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

            switch (_moveState)
            {
                case EnemyMoveState.MovingToBuildPoint:
                    UpdateMovingToBuildPoint();
                    break;
                case EnemyMoveState.Building:
                    UpdateBuilding();
                    break;
                case EnemyMoveState.MovingToPassage:
                    UpdateMovingToPassage();
                    break;
                case EnemyMoveState.ClimbingPassage:
                    ExecuteClimbing();
                    break;
                case EnemyMoveState.Finished:
                    ExecuteFinished();
                    break;
            }
        }

        private void UpdateMovingToBuildPoint()
        {
            if (_targetBuildPointIndex < 0)
            {
                if (currentPath.IsCompleted)
                {
                    StartMovingToPassage();
                }
                return;
            }

            var target = currentPath.wayPointList[_targetBuildPointIndex];
            var targetPos = GetBuildApproachPosition(target);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

            if (IsAtHorizontalTarget(targetPos))
            {
                transform.position = targetPos;
                _buildTimer = _data != null ? _data.BuildTime : 0f;
                _moveState = EnemyMoveState.Building;
                BuildEvents.RaiseBuildStarted(currentPath.wayPointList[_targetBuildPointIndex]);
            }
        }

        private void UpdateBuilding()
        {
            _buildTimer -= Time.deltaTime;
            if (_buildTimer > 0) return;

            // Building complete
            if (_targetBuildPointIndex >= 0 && _targetBuildPointIndex < currentPath.wayPointList.Length)
            {
                var bp = currentPath.wayPointList[_targetBuildPointIndex];
                if (!bp.IsBuildCompleted)
                {
                    bp.AddBuildProgress(buildAbility);
                }
            }
            currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;

            buildCharges--;

            if (buildCharges <= 0)
            {
                _moveState = EnemyMoveState.Finished;
                return;
            }

            // Find next target
            ReserveNextTarget();
            if (_targetBuildPointIndex >= 0)
            {
                _moveState = EnemyMoveState.MovingToBuildPoint;
            }
            else if (currentPath.IsCompleted)
            {
                StartMovingToPassage();
            }
            else
            {
                _moveState = EnemyMoveState.MovingToBuildPoint;
            }
        }

        private void StartMovingToPassage()
        {
            if (currentPath.nextLayerPath == null)
            {
                UIKit.OpenPanel<UIGameOverPanel>();
                return;
            }

            int gatewayIdx = currentPath.GetGatewayIndex();
            _passageTarget = currentPath.wayPointList[gatewayIdx].transform;
            _moveState = EnemyMoveState.MovingToPassage;
        }

        private void UpdateMovingToPassage()
        {
            if (_passageTarget == null) return;

            var targetPos = new Vector3(_passageTarget.position.x, transform.position.y, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
                _moveState = EnemyMoveState.ClimbingPassage;
            }
        }

        private void ExecuteClimbing()
        {
            currentPath = currentPath.nextLayerPath;

            // Teleport to entrance of new layer
            if (currentPath != null && currentPath.wayPointList.Length > 0)
            {
                transform.position = currentPath.wayPointList[0].transform.position;
            }

            ReserveNextTarget();
            _moveState = EnemyMoveState.MovingToBuildPoint;
        }

        private void ExecuteFinished()
        {
            ReleaseCurrentTarget();
            if (waveEventId >= 0)
            {
                OnChargesExhausted?.Invoke(waveEventId);
            }
            _ability?.OnRemoved();
            _ability = null;
            this.DestroyGameObjGracefully();
        }

        private void ReserveNextTarget()
        {
            if (currentPath == null)
            {
                _targetBuildPointIndex = -1;
                return;
            }
            _targetBuildPointIndex = currentPath.ReserveBuildPoint(transform.position);
        }

        private void ReleaseCurrentTarget()
        {
            if (_targetBuildPointIndex >= 0 && currentPath != null)
            {
                currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
            }
        }

        private Vector3 GetBuildApproachPosition(BuildPoint target)
        {
            return new Vector3(target.transform.position.x, transform.position.y, transform.position.z);
        }

        private bool IsAtHorizontalTarget(Vector3 targetPos)
        {
            return Mathf.Abs(transform.position.x - targetPos.x) <= 0.1f;
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
            ReleaseCurrentTarget();
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
            EnemyEvents.RaiseEnemyDied(Position);
            if (waveEventId >= 0)
            {
                OnChargesExhausted?.Invoke(waveEventId);
            }

            Global.Exp.Value += _data != null ? _data.ExpReward : 1;
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
