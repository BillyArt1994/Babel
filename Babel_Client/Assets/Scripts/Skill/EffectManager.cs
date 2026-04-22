using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    public class EffectManager
    {
        // ── Buff ──

        private struct ActiveBuff
        {
            public string StatName;
            public float Value;
            public float Duration;   // -1 = permanent
            public float Remaining;
        }

        private readonly List<ActiveBuff> _activeBuffs = new();

        public void AddBuff(string statName, float value, float duration)
        {
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].StatName == statName)
                {
                    var existing = _activeBuffs[i];
                    existing.Value += value;
                    if (duration >= 0 && existing.Duration >= 0)
                    {
                        existing.Remaining = Mathf.Max(existing.Remaining, duration);
                    }
                    _activeBuffs[i] = existing;
                    return;
                }
            }

            _activeBuffs.Add(new ActiveBuff
            {
                StatName = statName,
                Value = value,
                Duration = duration,
                Remaining = duration
            });
        }

        public float GetStatValue(string statName)
        {
            float total = 1.0f;
            for (int i = 0; i < _activeBuffs.Count; i++)
            {
                if (_activeBuffs[i].StatName == statName)
                {
                    total += _activeBuffs[i].Value;
                }
            }
            return total;
        }

        // ── DoT ──

        private struct ActiveDot
        {
            public Vector2 WorldPos;
            public float Radius;
            public float Dps;
            public float Remaining;
        }

        private readonly List<ActiveDot> _activeDots = new();

        public void AddDot(Vector2 worldPos, float radius, float dps, float duration)
        {
            _activeDots.Add(new ActiveDot
            {
                WorldPos = worldPos,
                Radius = radius,
                Dps = dps,
                Remaining = duration
            });
        }

        // ── Tick ──

        private static readonly Collider2D[] _dotHitBuffer = new Collider2D[64];

        public void Tick(float deltaTime)
        {
            for (int i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                if (buff.Duration < 0) continue;
                buff.Remaining -= deltaTime;
                if (buff.Remaining <= 0)
                {
                    _activeBuffs.RemoveAt(i);
                }
                else
                {
                    _activeBuffs[i] = buff;
                }
            }

            for (int i = _activeDots.Count - 1; i >= 0; i--)
            {
                var dot = _activeDots[i];
                dot.Remaining -= deltaTime;

                float damage = dot.Dps * deltaTime;
                int hitCount = Physics2D.OverlapCircleNonAlloc(dot.WorldPos, dot.Radius, _dotHitBuffer);
                for (int j = 0; j < hitCount; j++)
                {
                    if (_dotHitBuffer[j].TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                    {
                        target.TakeDamage(damage, false);
                    }
                }

                if (dot.Remaining <= 0)
                {
                    _activeDots.RemoveAt(i);
                }
                else
                {
                    _activeDots[i] = dot;
                }
            }
        }

        public void ClearAll()
        {
            _activeBuffs.Clear();
            _activeDots.Clear();
        }
    }
}
