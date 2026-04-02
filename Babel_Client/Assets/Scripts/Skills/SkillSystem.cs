using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the gameplay rules from design/gdd/技能系统.md
/// and integrates with design/gdd/升级系统.md.
/// 
/// Responsibilities:
/// - Translate input into click-form attacks
/// - Manage passive stacks and derived modifiers
/// - Drive auto-attack passives
/// - React to combat and enemy death events for effect passives
/// </summary>
[DefaultExecutionOrder(-60)]
public class SkillSystem : MonoBehaviour
{
    private const float CHARGE_MULT = 1.5f;
    private const float CHARGE_RADIUS_MIN = 0.5f;
    private const float CHARGE_SLOW_MIN = 0.5f;
    private const float CRIT_MULT = 2.0f;
    private const float MAX_CRIT_CHANCE = 0.8f;
    private const float MIN_COOLDOWN_MULT = 0.2f;
    private const float AFTERSHOCK_RATIO = 0.3f;
    private const float AFTERSHOCK_RADIUS = 1.5f;
    private const float PLAGUE_RATIO = 0.2f;
    private const float PLAGUE_RADIUS = 2.0f;
    private const float RAGE_COOLDOWN_REDUCTION = 0.5f;
    private const float AUTO_ATTACK_SEARCH_RADIUS_DEFAULT = 30f;

    /// <summary>Pre-allocated buffer for OverlapCircleNonAlloc to avoid per-frame GC allocation.</summary>
    private static readonly Collider2D[] _autoAttackBuffer = new Collider2D[128];

    // Skill IDs — must match the skillId field in SkillData ScriptableObjects
    private const string SKILL_ID_RAGE = "rage";
    private const string SKILL_ID_DIVINE_POWER = "divine_power";
    private const string SKILL_ID_CRIT_STRIKE = "crit_strike";
    private const string SKILL_ID_EXECUTE = "execute";
    private const string SKILL_ID_AFTERSHOCK = "aftershock";
    private const string SKILL_ID_PLAGUE = "plague";
    private const string SKILL_ID_MARK = "mark";
    private const string SKILL_ID_THUNDER = "thunder";
    private const string SKILL_ID_DIVINE_FIRE = "divine_fire";
    private const string SKILL_ID_TRACKING_ORB = "tracking_orb";

    public static SkillSystem Instance { get; private set; }

    public struct PassiveEntry
    {
        public SkillData Data;
        public int Stacks;
    }

    private struct PassiveModifiers
    {
        public float DamageMult;
        public float CooldownMult;
        public float CritChance;
        public bool HasExecute;
        public bool HasAfterShock;
        public bool HasPlague;
        public bool HasMark;
        public bool HasRage;
    }

    [SerializeField] private SkillData _defaultStarterSkill;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private float _autoAttackSearchRadius = AUTO_ATTACK_SEARCH_RADIUS_DEFAULT;

    private SkillData _activeClickForm;
    private List<PassiveEntry> _passives = new List<PassiveEntry>();
    private Dictionary<SkillData, float> _autoTimers = new Dictionary<SkillData, float>();
    private List<PassiveEntry> _autoPassives = new List<PassiveEntry>();
    private readonly List<EnemyController> _randomEnemyBuffer = new List<EnemyController>(32);
    private PassiveModifiers _mods;
    private float _cooldownTimer;
    private bool _isCharging;
    private Vector2 _chargeWorldPos;
    private bool _processingEffectPassives;
    private bool _isActive;

    private TowerConstructionSystem _towerSystem;

    public SkillData GetActiveClickForm() => _activeClickForm;

    public float GetCooldownProgress()
    {
        float actualCooldown = GetActualCooldown(_activeClickForm);
        if (actualCooldown <= 0f || _cooldownTimer <= 0f)
            return 0f;
        return Mathf.Clamp01(_cooldownTimer / actualCooldown);
    }

    public IReadOnlyList<PassiveEntry> GetPassives() => _passives;

    public int GetPassiveStacks(SkillData skill)
    {
        if (skill == null) return 0;
        for (int i = 0; i < _passives.Count; i++)
        {
            if (_passives[i].Data == skill)
                return _passives[i].Stacks;
        }
        return 0;
    }

    public bool HasSkill(SkillData skill)
    {
        if (skill == null) return false;
        if (_activeClickForm == skill) return true;
        for (int i = 0; i < _passives.Count; i++)
        {
            if (_passives[i].Data == skill) return true;
        }
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _towerSystem = FindFirstObjectByType<TowerConstructionSystem>();
        RecalculateModifiers();
    }

    private void OnEnable()
    {
        InputEvents.OnMouseDown += OnMouseDown;
        InputEvents.OnMouseHeld += OnMouseHeld;
        InputEvents.OnMouseUp += OnMouseUp;

        GameEvents.OnGameStart += OnGameStart;
        GameEvents.OnGamePaused += OnGamePaused;
        GameEvents.OnGameResumed += OnGameResumed;
        GameEvents.OnLevelUpStart += OnLevelUpStart;
        GameEvents.OnLevelUpComplete += OnLevelUpComplete;
        GameEvents.OnVictory += OnGameStopped;
        GameEvents.OnDefeat += OnGameStopped;

        CombatEvents.OnAttackExecuted += OnAttackExecuted;
        EnemyEvents.OnEnemyDied += OnEnemyDied;

        _isActive = GameLoopManager.Instance != null && GameLoopManager.Instance.IsPlaying();
    }

    private void OnDisable()
    {
        InputEvents.OnMouseDown -= OnMouseDown;
        InputEvents.OnMouseHeld -= OnMouseHeld;
        InputEvents.OnMouseUp -= OnMouseUp;

        GameEvents.OnGameStart -= OnGameStart;
        GameEvents.OnGamePaused -= OnGamePaused;
        GameEvents.OnGameResumed -= OnGameResumed;
        GameEvents.OnLevelUpStart -= OnLevelUpStart;
        GameEvents.OnLevelUpComplete -= OnLevelUpComplete;
        GameEvents.OnVictory -= OnGameStopped;
        GameEvents.OnDefeat -= OnGameStopped;

        CombatEvents.OnAttackExecuted -= OnAttackExecuted;
        EnemyEvents.OnEnemyDied -= OnEnemyDied;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!_isActive) return;

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer < 0f) _cooldownTimer = 0f;
        }

        for (int i = 0; i < _autoPassives.Count; i++)
        {
            SkillData data = _autoPassives[i].Data;
            if (data == null || data.TriggerInterval <= 0f) continue;

            if (!_autoTimers.ContainsKey(data))
                _autoTimers[data] = data.TriggerInterval;

            _autoTimers[data] -= Time.deltaTime;
            if (_autoTimers[data] <= 0f)
            {
                FireAutoAttack(data);
                _autoTimers[data] = data.TriggerInterval;
            }
        }
    }

    public void Initialize(SkillData starterSkill)
    {
        _passives.Clear();
        _autoPassives.Clear();
        _autoTimers.Clear();

        _activeClickForm = starterSkill;
        _cooldownTimer = 0f;
        _isCharging = false;
        _chargeWorldPos = Vector2.zero;
        _processingEffectPassives = false;

        RecalculateModifiers();

        if (starterSkill != null)
            SkillEvents.RaiseClickFormChanged(starterSkill);
    }

    public void AddSkill(SkillData skill)
    {
        if (skill == null) return;

        if (skill.SkillType == SkillType.ClickForm)
        {
            _activeClickForm = skill;
            _cooldownTimer = 0f;
            RecalculateModifiers();
            SkillEvents.RaiseSkillAdded(skill);
            SkillEvents.RaiseClickFormChanged(skill);
            return;
        }

        for (int i = 0; i < _passives.Count; i++)
        {
            if (_passives[i].Data != skill) continue;

            int maxStacks = skill.MaxStackCount <= 0 ? int.MaxValue : skill.MaxStackCount;
            if (_passives[i].Stacks >= maxStacks) return;

            PassiveEntry updatedEntry = _passives[i];
            updatedEntry.Stacks++;
            _passives[i] = updatedEntry;

            for (int autoIndex = 0; autoIndex < _autoPassives.Count; autoIndex++)
            {
                if (_autoPassives[autoIndex].Data != skill) continue;
                PassiveEntry updatedAutoEntry = _autoPassives[autoIndex];
                updatedAutoEntry.Stacks = updatedEntry.Stacks;
                _autoPassives[autoIndex] = updatedAutoEntry;
                break;
            }

            RecalculateModifiers();
            SkillEvents.RaiseSkillAdded(skill);
            return;
        }

        PassiveEntry entry = new PassiveEntry { Data = skill, Stacks = 1 };
        _passives.Add(entry);

        if (skill.PassiveCategory == PassiveCategory.AutoAttack)
        {
            _autoPassives.Add(entry);
            _autoTimers[skill] = skill.TriggerInterval;
        }

        RecalculateModifiers();
        SkillEvents.RaiseSkillAdded(skill);
    }

    private void RecalculateModifiers()
    {
        _mods = new PassiveModifiers
        {
            DamageMult = 1f,
            CooldownMult = 1f,
            CritChance = 0f,
            HasExecute = false,
            HasAfterShock = false,
            HasPlague = false,
            HasMark = false,
            HasRage = false
        };

        for (int i = 0; i < _passives.Count; i++)
        {
            SkillData data = _passives[i].Data;
            int stacks = _passives[i].Stacks;
            if (data == null) continue;

            switch (data.PassiveCategory)
            {
                case PassiveCategory.Frequency:
                    _mods.CooldownMult += data.Cooldown * stacks;
                    if (data.SkillId == SKILL_ID_RAGE) _mods.HasRage = true;
                    break;

                case PassiveCategory.Power:
                    if (data.SkillId == SKILL_ID_DIVINE_POWER)
                        _mods.DamageMult += data.Damage * stacks;
                    else if (data.SkillId == SKILL_ID_CRIT_STRIKE)
                        _mods.CritChance += 0.20f * stacks;
                    else if (data.SkillId == SKILL_ID_EXECUTE)
                        _mods.HasExecute = true;
                    break;

                case PassiveCategory.Effect:
                    if (data.SkillId == SKILL_ID_AFTERSHOCK) _mods.HasAfterShock = true;
                    else if (data.SkillId == SKILL_ID_PLAGUE) _mods.HasPlague = true;
                    else if (data.SkillId == SKILL_ID_MARK) _mods.HasMark = true;
                    break;

                case PassiveCategory.AutoAttack:
                    break;
            }
        }

        _mods.CooldownMult = Mathf.Clamp(_mods.CooldownMult, MIN_COOLDOWN_MULT, 1.0f);
        _mods.CritChance = Mathf.Clamp(_mods.CritChance, 0f, MAX_CRIT_CHANCE);
    }

    private void OnMouseDown(Vector2 worldPos)
    {
        if (!_isActive || _activeClickForm == null) return;
        _isCharging = true;
        _chargeWorldPos = worldPos;
        SkillEvents.RaiseChargeStarted(worldPos);
    }

    private void OnMouseHeld(Vector2 worldPos, float holdDuration)
    {
        if (!_isActive || !_isCharging || _activeClickForm == null) return;
        _chargeWorldPos = worldPos;
        SkillEvents.RaiseChargeUpdated(worldPos, GetChargeRatio(holdDuration));
    }

    private void OnMouseUp(Vector2 worldPos, float holdDuration)
    {
        if (!_isActive) { _isCharging = false; return; }
        if (!_isCharging) return;
        TryFireClickAttack(worldPos, holdDuration);
        _isCharging = false;
    }

    private void TryFireClickAttack(Vector2 worldPos, float holdDuration)
    {
        if (_activeClickForm == null) return;
        if (GameLoopManager.Instance == null || GameLoopManager.Instance.GetCurrentState() != GameState.Playing) return;
        if (_cooldownTimer > 0f) return;

        if (ClickAttackSystem.Instance == null)
        {
            Debug.LogWarning("SkillSystem could not fire click attack because ClickAttackSystem.Instance is null.", this);
            return;
        }

        float chargeRatio = GetChargeRatio(holdDuration);
        bool isCrit = UnityEngine.Random.value < _mods.CritChance;
        float chargeFactor = Mathf.Lerp(1f, CHARGE_MULT, chargeRatio);

        float damage =
            _activeClickForm.Damage *
            chargeFactor *
            _mods.DamageMult *
            (isCrit ? CRIT_MULT : 1f);

        float radiusFactor = Mathf.Lerp(CHARGE_RADIUS_MIN, 1f, chargeRatio);
        float slowFactor = Mathf.Lerp(CHARGE_SLOW_MIN, 1f, chargeRatio);

        AttackRequest request = new AttackRequest
        {
            worldPos = worldPos,
            attackType = InferAttackType(_activeClickForm),
            damage = damage,
            radius = _activeClickForm.AoeRadius * radiusFactor,
            chainCount = _activeClickForm.ChainCount,
            chainRadius = 0f,
            chainDecay = 0f,
            slowPercent = _activeClickForm.SlowPercent * slowFactor,
            slowDuration = _activeClickForm.SlowDuration * slowFactor,
            isPassiveAttack = false
        };

        ClickAttackSystem.Instance.ExecuteAttack(request);
        _cooldownTimer = GetActualCooldown(_activeClickForm);
    }

    private void OnAttackExecuted(AttackResult result)
    {
        if (_processingEffectPassives) return;
        if (result.hits == null || result.hits.Count == 0) return;

        if (_mods.HasAfterShock)
        {
            if (ClickAttackSystem.Instance == null) return;

            _processingEffectPassives = true;
            try
            {
                for (int i = 0; i < result.hits.Count; i++)
                {
                    HitInfo hit = result.hits[i];
                    AttackRequest request = new AttackRequest
                    {
                        worldPos = hit.hitPosition,
                        attackType = AttackType.AOE,
                        damage = hit.damageDone * AFTERSHOCK_RATIO,
                        radius = AFTERSHOCK_RADIUS,
                        chainCount = 0,
                        chainRadius = 0f,
                        chainDecay = 0f,
                        slowPercent = 0f,
                        slowDuration = 0f,
                        isPassiveAttack = true
                    };
                    ClickAttackSystem.Instance.ExecuteAttack(request);
                }
            }
            finally
            {
                _processingEffectPassives = false;
            }
        }

        if (_mods.HasMark)
        {
            for (int i = 0; i < result.hits.Count; i++)
            {
                if (result.hits[i].enemy == null) continue;
                result.hits[i].enemy.ApplySlow(0f, 0f);
            }
        }
    }

    private void OnEnemyDied(EnemyData data, Vector2 deathPosition)
    {
        if (_mods.HasPlague && ClickAttackSystem.Instance != null)
        {
            AttackRequest plagueRequest = new AttackRequest
            {
                worldPos = deathPosition,
                attackType = AttackType.AOE,
                damage = GetPlagueDamage(),
                radius = PLAGUE_RADIUS,
                chainCount = 0,
                chainRadius = 0f,
                chainDecay = 0f,
                slowPercent = 0f,
                slowDuration = 0f,
                isPassiveAttack = true
            };
            ClickAttackSystem.Instance.ExecuteAttack(plagueRequest);
        }

        if (_mods.HasRage)
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - RAGE_COOLDOWN_REDUCTION);
    }

    private void FireAutoAttack(SkillData data)
    {
        if (data == null || ClickAttackSystem.Instance == null) return;

        int hitCount = Physics2D.OverlapCircleNonAlloc(GetTowerBasePosition(), _autoAttackSearchRadius, _autoAttackBuffer, _enemyLayer);
        if (hitCount == 0) return;

        AttackRequest request = new AttackRequest
        {
            attackType = AttackType.Single,
            damage = data.Damage * _mods.DamageMult,
            radius = data.AoeRadius,
            chainCount = data.ChainCount,
            chainRadius = 0f,
            chainDecay = 0f,
            slowPercent = data.SlowPercent,
            slowDuration = data.SlowDuration,
            isPassiveAttack = true
        };

        switch (data.SkillId)
        {
            case SKILL_ID_THUNDER:
            {
                EnemyController randomEnemy = FindRandomEnemy(_autoAttackBuffer, hitCount);
                if (randomEnemy == null) return;
                request.worldPos = randomEnemy.transform.position;
                request.attackType = AttackType.Single;
                break;
            }
            case SKILL_ID_DIVINE_FIRE:
            {
                request.worldPos = GetTowerBasePosition();
                request.attackType = AttackType.AOE;
                request.radius = data.AoeRadius;
                break;
            }
            case SKILL_ID_TRACKING_ORB:
            {
                EnemyController nearestEnemy = FindNearestEnemy(_autoAttackBuffer, hitCount, GetTowerBasePosition());
                if (nearestEnemy == null) return;
                request.worldPos = nearestEnemy.transform.position;
                request.attackType = AttackType.Single;
                break;
            }
            default:
                return;
        }

        ClickAttackSystem.Instance.ExecuteAttack(request);
    }

    private void OnGameStart() { Initialize(_defaultStarterSkill); SetActiveState(true); }
    private void OnGamePaused() => SetActiveState(false);
    private void OnGameResumed() => SetActiveState(true);
    private void OnLevelUpStart() => SetActiveState(false);
    private void OnLevelUpComplete() => SetActiveState(true);
    private void OnGameStopped() => SetActiveState(false);

    private void SetActiveState(bool isActive)
    {
        _isActive = isActive;
        if (!isActive && _isCharging)
        {
            SkillEvents.RaiseChargeUpdated(_chargeWorldPos, 0f);
            _isCharging = false;
        }
    }

    private float GetChargeRatio(float holdDuration)
    {
        if (_activeClickForm == null || _activeClickForm.ChargeTime <= 0f) return 0f;
        return Mathf.Clamp01(holdDuration / _activeClickForm.ChargeTime);
    }

    private float GetActualCooldown(SkillData skill)
    {
        if (skill == null) return 0f;
        return skill.Cooldown * _mods.CooldownMult;
    }

    private AttackType InferAttackType(SkillData skill)
    {
        if (skill == null) return AttackType.Single;
        if (skill.ChainCount > 0) return AttackType.Chain;
        if (skill.Piercing) return AttackType.Pierce;
        if (skill.AoeRadius > 0f) return AttackType.AOE;
        return AttackType.Single;
    }

    private float GetPlagueDamage()
    {
        if (_activeClickForm == null) return 0f;
        return _activeClickForm.Damage * _mods.DamageMult * PLAGUE_RATIO;
    }

    private Vector2 GetTowerBasePosition()
    {
        if (_towerSystem == null)
            _towerSystem = FindFirstObjectByType<TowerConstructionSystem>();
        if (_towerSystem != null) return _towerSystem.transform.position;
        return Vector2.zero;
    }

    private EnemyController FindRandomEnemy(Collider2D[] colliders, int count)
    {
        _randomEnemyBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            if (colliders[i] == null) continue;
            EnemyController enemy = colliders[i].GetComponent<EnemyController>();
            if (enemy != null) _randomEnemyBuffer.Add(enemy);
        }
        if (_randomEnemyBuffer.Count == 0) return null;
        return _randomEnemyBuffer[UnityEngine.Random.Range(0, _randomEnemyBuffer.Count)];
    }

    private EnemyController FindNearestEnemy(Collider2D[] colliders, int count, Vector2 origin)
    {
        EnemyController nearestEnemy = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (colliders[i] == null) continue;
            EnemyController enemy = colliders[i].GetComponent<EnemyController>();
            if (enemy == null) continue;
            float sqrDistance = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestEnemy = enemy;
            }
        }
        return nearestEnemy;
    }
}
