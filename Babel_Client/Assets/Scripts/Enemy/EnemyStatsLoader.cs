using UnityEngine;

public class EnemyStatsLoader : MonoBehaviour
{
    private const string CSV_PATH = "Data/enemy_stats";

    [SerializeField] private EnemyDatabase _enemyDatabase;

    public void LoadAndApply()
    {
        TextAsset csv = Resources.Load<TextAsset>(CSV_PATH);
        if (csv == null)
        {
            Debug.LogError($"EnemyStatsLoader: CSV not found at Resources/{CSV_PATH}");
            return;
        }

        var (header, rows) = CsvParser.Parse(csv.text);

        int colId = header.ContainsKey("enemyid") ? header["enemyid"] : -1;
        int colHp = header.ContainsKey("maxhealth") ? header["maxhealth"] : -1;
        int colSpd = header.ContainsKey("movespeed") ? header["movespeed"] : -1;
        int colFaith = header.ContainsKey("faithvalue") ? header["faithvalue"] : -1;
        int colBuild = header.ContainsKey("buildcontribution") ? header["buildcontribution"] : -1;
        int colAbility = header.ContainsKey("specialability") ? header["specialability"] : -1;
        int colHealR = header.ContainsKey("healradius") ? header["healradius"] : -1;
        int colHealS = header.ContainsKey("healpersecond") ? header["healpersecond"] : -1;
        int colExplR = header.ContainsKey("deathexplosionradius") ? header["deathexplosionradius"] : -1;
        int colExplF = header.ContainsKey("deathexplosionforce") ? header["deathexplosionforce"] : -1;

        if (colId < 0)
        {
            Debug.LogError("EnemyStatsLoader: CSV missing 'enemyId' column");
            return;
        }

        int applied = 0;
        foreach (string[] row in rows)
        {
            string enemyId = CsvParser.GetString(row, colId);
            if (string.IsNullOrEmpty(enemyId)) continue;

            EnemyData data = FindByEnemyId(enemyId);
            if (data == null)
            {
                Debug.LogWarning($"EnemyStatsLoader: No EnemyData found for id='{enemyId}'");
                continue;
            }

            EnemySpecialAbility ability = EnemySpecialAbility.None;
            string abilityStr = CsvParser.GetString(row, colAbility);
            if (!string.IsNullOrEmpty(abilityStr))
                System.Enum.TryParse(abilityStr, true, out ability);

            data.OverrideStats(
                CsvParser.GetFloat(row, colHp, data.MaxHealth),
                CsvParser.GetFloat(row, colSpd, data.MoveSpeed),
                CsvParser.GetFloat(row, colFaith, data.FaithValue),
                CsvParser.GetFloat(row, colBuild, data.BuildContribution),
                ability,
                CsvParser.GetFloat(row, colHealR, data.HealRadius),
                CsvParser.GetFloat(row, colHealS, data.HealPerSecond),
                CsvParser.GetFloat(row, colExplR, data.DeathExplosionRadius),
                CsvParser.GetFloat(row, colExplF, data.DeathExplosionForce)
            );
            applied++;

            BabelLogger.AC("CSV", $"Loaded stats: {enemyId} hp={data.MaxHealth} spd={data.MoveSpeed} faith={data.FaithValue}");
        }

        BabelLogger.AC("CSV", $"Applied {applied} enemy stat overrides from CSV");
    }

    private EnemyData FindByEnemyId(string enemyId)
    {
        if (_enemyDatabase == null || _enemyDatabase.AllEnemies == null) return null;

        for (int i = 0; i < _enemyDatabase.AllEnemies.Length; i++)
        {
            EnemyData data = _enemyDatabase.AllEnemies[i];
            if (data != null && string.Equals(data.EnemyId, enemyId, System.StringComparison.OrdinalIgnoreCase))
                return data;
        }
        return null;
    }
}
