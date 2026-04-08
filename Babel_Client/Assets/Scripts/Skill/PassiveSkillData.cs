using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babel
{
    [CreateAssetMenu(menuName = "Babel/PassiveSkillData")]
    public class PassiveSkillData : SkillData
    {
        [Header("–ﬁ Œ£®√ø≤„£©")]
        public float damageBonus;
        public float cooldownReduction;
        public float radiusBonus;
        public PassiveLevelStats[] levelStats;
    }

    [System.Serializable]
    public struct PassiveLevelStats
    {
        public float damageBonus;
        public float cooldownReduction;
        public float radiusBonus;
    }
}