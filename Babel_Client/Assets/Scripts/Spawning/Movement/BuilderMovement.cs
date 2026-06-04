using UnityEngine;

namespace Babel
{
    /// <summary>
    /// 标准建造者移动策略。包含原 Enemy 状态机的全部逻辑。
    /// 同时实现 IBuildInterruptible：当目标点被他人建完时立即中断并选下一个目标。
    /// </summary>
    public class BuilderMovement : IEnemyMovement, IBuildInterruptible
    {
        private Enemy _owner;
        private EnemyData _data;
        private ITargetSelector _selector;

        private EnemyMoveState _state = EnemyMoveState.MovingToBuildPoint;
        private int _targetBuildPointIndex = -1;
        private Transform _passageTarget;
        private float _buildTimer;
        private int _buildChargesLeft;

        public bool IsMoving =>
            _state == EnemyMoveState.MovingToBuildPoint ||
            _state == EnemyMoveState.MovingToPassage;

        public BuilderMovement() : this(DefaultBuildSelector.Instance) { }

        public BuilderMovement(ITargetSelector selector)
        {
            _selector = selector ?? DefaultBuildSelector.Instance;
        }

        public void Init(Enemy owner, EnemyData data)
        {
            _owner = owner;
            _data = data;
            _buildChargesLeft = data.BuildCharges;
            _state = EnemyMoveState.MovingToBuildPoint;
            _targetBuildPointIndex = -1;
            _buildTimer = 0f;
            _passageTarget = null;
            ReserveNextTarget();
        }

        public void Tick(float deltaTime)
        {
            switch (_state)
            {
                case EnemyMoveState.MovingToBuildPoint:
                    UpdateMovingToBuildPoint(deltaTime);
                    break;
                case EnemyMoveState.Building:
                    UpdateBuilding(deltaTime);
                    break;
                case EnemyMoveState.MovingToPassage:
                    UpdateMovingToPassage(deltaTime);
                    break;
                case EnemyMoveState.ClimbingPassage:
                    ExecuteClimbing();
                    break;
                case EnemyMoveState.Finished:
                    ExecuteFinished();
                    break;
            }
        }

        public void OnRemoved()
        {
            ReleaseCurrentTarget();
        }

        public void OnTargetBuildCompleted(BuildPoint point)
        {
            if (_state != EnemyMoveState.Building) return;
            if (_targetBuildPointIndex < 0) return;

            var path = _owner.currentPath;
            if (path == null) return;
            if (_targetBuildPointIndex >= path.wayPointList.Length) return;
            if (path.wayPointList[_targetBuildPointIndex] != point) return;

            // 目标点已被他人建完：释放预约，不扣 charge，选下一目标
            path.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;

            ChooseNextAfterRelease();
        }

        private void UpdateMovingToBuildPoint(float dt)
        {
            if (_targetBuildPointIndex < 0)
            {
                bool canClimb = _owner.currentPath.nextLayerPath != null
                    && (_owner.currentPath.IsCompleted || _owner.currentPath.IsGatewayBuilt());
                if (canClimb) StartMovingToPassage();
                return;
            }

            // 移动途中目标点已被他人建完：放弃该点，不扣 charge，重选目标
            var wayPoints = _owner.currentPath.wayPointList;
            if (_targetBuildPointIndex >= wayPoints.Length)
            {
                _owner.currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
                ChooseNextAfterRelease();
                return;
            }
            var targetBp = wayPoints[_targetBuildPointIndex];
            if (targetBp == null || targetBp.IsBuildCompleted)
            {
                _owner.currentPath.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
                ChooseNextAfterRelease();
                return;
            }

            var target = _owner.currentPath.wayPointList[_targetBuildPointIndex];
            var targetPos = GetBuildApproachPosition(target);
            UpdateFacing(targetPos.x);
            _owner.transform.position = Vector3.MoveTowards(
                _owner.transform.position, targetPos, _owner.EffectiveSpeed * dt);

            if (IsAtHorizontalTarget(targetPos))
            {
                _owner.transform.position = targetPos;
                _buildTimer = _data.BuildTime;
                _state = EnemyMoveState.Building;
                target.BeginBuild();
                target.AttachBuilder(this);
                BuildEvents.RaiseBuildStarted(target);
            }
        }

        private void UpdateBuilding(float dt)
        {
            _buildTimer -= dt;
            if (_buildTimer > 0f) return;

            var path = _owner.currentPath;
            if (_targetBuildPointIndex >= 0 && _targetBuildPointIndex < path.wayPointList.Length)
            {
                var bp = path.wayPointList[_targetBuildPointIndex];
                bp.DetachBuilder(this);
                if (!bp.IsBuildCompleted)
                    bp.AddBuildProgress(_owner.buildAbility);
                // AddBuildProgress 内部若触发完成会调 OnTargetBuildCompleted，
                // 但此时已 DetachBuilder，故不会二次触发。
            }

            path.ReleaseBuildPoint(_targetBuildPointIndex);
            _targetBuildPointIndex = -1;
            _buildChargesLeft--;

            if (_buildChargesLeft <= 0)
            {
                _state = EnemyMoveState.Finished;
                return;
            }

            ChooseNextAfterRelease();
        }

        private void ChooseNextAfterRelease()
        {
            ReserveNextTarget();
            if (_targetBuildPointIndex >= 0)
            {
                _state = EnemyMoveState.MovingToBuildPoint;
            }
            else if (_owner.currentPath.nextLayerPath != null
                     && (_owner.currentPath.IsCompleted || _owner.currentPath.IsGatewayBuilt()))
            {
                StartMovingToPassage();
            }
            else
            {
                _state = EnemyMoveState.MovingToBuildPoint;
            }
        }

        private void StartMovingToPassage()
        {
            if (_owner.currentPath.nextLayerPath == null)
            {
                GameSession.EndGame(GameEndReason.Defeat);
                return;
            }
            int gatewayIdx = _owner.currentPath.GetGatewayIndex();
            _passageTarget = _owner.currentPath.wayPointList[gatewayIdx].transform;
            _state = EnemyMoveState.MovingToPassage;
        }

        private void UpdateMovingToPassage(float dt)
        {
            if (_passageTarget == null) return;
            var targetPos = new Vector3(
                _passageTarget.position.x,
                _owner.transform.position.y,
                _owner.transform.position.z);
            UpdateFacing(targetPos.x);
            _owner.transform.position = Vector3.MoveTowards(
                _owner.transform.position, targetPos, _owner.EffectiveSpeed * dt);

            if ((_owner.transform.position - targetPos).magnitude <= 0.1f)
                _state = EnemyMoveState.ClimbingPassage;
        }

        private void ExecuteClimbing()
        {
            _owner.currentPath = _owner.currentPath.nextLayerPath;
            if (_owner.currentPath != null && _owner.currentPath.wayPointList.Length > 0)
                _owner.transform.position = _owner.currentPath.wayPointList[0].transform.position;
            ReserveNextTarget();
            _state = EnemyMoveState.MovingToBuildPoint;
        }

        private void ExecuteFinished()
        {
            ReleaseCurrentTarget();
            _owner.NotifyChargesExhausted();
        }

        private void ReserveNextTarget()
        {
            if (_owner.currentPath == null) { _targetBuildPointIndex = -1; return; }
            _targetBuildPointIndex = _owner.currentPath.ReserveBuildPoint(
                _owner.transform.position, _selector);
        }

        private void ReleaseCurrentTarget()
        {
            if (_targetBuildPointIndex >= 0 && _owner.currentPath != null)
            {
                var path = _owner.currentPath;
                if (_targetBuildPointIndex < path.wayPointList.Length)
                    path.wayPointList[_targetBuildPointIndex].DetachBuilder(this);
                path.ReleaseBuildPoint(_targetBuildPointIndex);
                _targetBuildPointIndex = -1;
            }
        }

        private Vector3 GetBuildApproachPosition(BuildPoint target)
            => new Vector3(target.transform.position.x,
                           _owner.transform.position.y,
                           _owner.transform.position.z);

        private void UpdateFacing(float targetX)
        {
            if (_owner.Circle == null) return;
            float dx = targetX - _owner.transform.position.x;
            if (Mathf.Abs(dx) < 0.01f) return;
            _owner.Circle.flipX = dx < 0f;
        }

        private bool IsAtHorizontalTarget(Vector3 targetPos)
            => Mathf.Abs(_owner.transform.position.x - targetPos.x) <= 0.1f;
    }
}
