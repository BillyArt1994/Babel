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
        private float _speedBuffTimer;
        private float _speedBuffMult = 1.0f;

        public static event Action<int> OnChargesExhausted;

        // IDamageable
        public Vector2 Position => (Vector2)transform.position;
        public bool IsAlive => HP > 0;
        public float EffectiveSpeed => MovementSpeed * _speedBuffMult;

        public void TakeDamage(float damage, bool isCrit)
        {
            if (!IsAlive) return;
            HP -= damage;
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
            // Death check
            if (HP <= 0)
            {
                ReleaseCurrentTarget();
                EnemyEvents.RaiseEnemyDied(Position);
                if (waveEventId >= 0)
                {
                    OnChargesExhausted?.Invoke(waveEventId);
                }
                _ability?.OnRemoved();
                _ability = null;
                this.DestroyGameObjGracefully();
                Global.Exp.Value += _data != null ? _data.ExpReward : 1;
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
            var targetPos = new Vector3(target.transform.position.x, target.transform.position.y - 0.5f, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

            if ((transform.position - targetPos).magnitude <= 0.1f)
            {
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
    }
}
