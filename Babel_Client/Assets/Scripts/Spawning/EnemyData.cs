namespace Babel
{
    public class EnemyData
    {
        public string EnemyId = "";
        public string EnemyName = "";
        public float Hp;
        public float MoveSpeed;
        public int BuildContribution;
        public int BuildCharges;
        public int ExpReward;
        public string Prefab = "";

        public string AbilityType = "";
        public float AbilityRadius;
        public float AbilityValue;
        public float AbilityCooldown;
        public float BuildTime;

        // 原 TargetMode 改名为 MoveMode；EnemyParser 读取 CSV "moveMode" 列
        public string MoveMode = "";

        // 感知半径（SupportMovement 使用）；CSV 列 "senseRadius"
        public float SenseRadius = 8f;
    }
}
