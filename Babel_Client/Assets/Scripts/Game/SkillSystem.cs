using System.Collections.Generic;
using UnityEngine;
using QFramework;

namespace Babel
{
    public partial class SkillSystem : ViewController
    {
        private readonly List<Skill> _skills = new();
        private readonly EffectManager _effectManager = new();
        private bool _enabled;

        private void Start()
        {
            _enabled = true;
        }

        private void Update()
        {
            if (!_enabled) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Tick(dt);
            }
            _effectManager.Tick(dt);
        }

        public void AddSkill(SkillConfig config)
        {
            var skill = SkillFactory.Create(config, _effectManager, GetBasePosition);
            _skills.Add(skill);
            if (_enabled)
            {
                skill.Trigger.Enable();
            }
        }

        public void RemoveSkill(string skillId)
        {
            for (int i = _skills.Count - 1; i >= 0; i--)
            {
                if (_skills[i].Config.SkillId == skillId)
                {
                    _skills[i].Trigger.Disable();
                    _skills.RemoveAt(i);
                    return;
                }
            }
        }

        public IReadOnlyList<Skill> GetEquippedSkills() => _skills;

        public float GetCooldownProgress(string skillId)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].Config.SkillId == skillId &&
                    _skills[i].Trigger is OnClickTrigger clickTrigger)
                {
                    return clickTrigger.CooldownProgress;
                }
            }
            return 0f;
        }

        public void EnableAll()
        {
            _enabled = true;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Enable();
            }
        }

        public void DisableAll()
        {
            _enabled = false;
            for (int i = 0; i < _skills.Count; i++)
            {
                _skills[i].Trigger.Disable();
            }
        }

        public void ClearAll()
        {
            DisableAll();
            _skills.Clear();
            _effectManager.ClearAll();
        }

        private Vector2 GetBasePosition()
        {
            return (Vector2)transform.position;
        }
    }
}
