using UnityEngine;

namespace Babel
{
    [CreateAssetMenu(menuName = "Babel/ActiveSKillData")]
    public class ActiveSkillData : SkillData
    {
        public float damage;
        public float cooldown = 1f;
        public float chargeTime =1f;
        public float aoeRadius =0.0f;
        public SkillType skillType = 0;
        public ClickFormLevelStats[] levelStats = new ClickFormLevelStats[10];
    }

    [System.Serializable]
    public struct ClickFormLevelStats
    {
        public float damage;
        public float cooldown;
        public float chargeTime;
        public float aoeRadius;
    }

    public enum SkillType
    {
        Click,
        Auto,
    }
}