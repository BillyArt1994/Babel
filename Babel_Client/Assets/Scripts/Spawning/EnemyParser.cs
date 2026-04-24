using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Babel
{
    public static class EnemyParser
    {
        private const string LOG_PREFIX = "[BABEL][EnemyParser]";

        public static List<EnemyData> Parse(string csvText)
        {
            var results = new List<EnemyData>();
            if (string.IsNullOrEmpty(csvText)) return results;

            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return results;

            var header = lines[0].Split(',');
            var colMap = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
                colMap[header[i].Trim().ToLower()] = i;

            string[] required = { "enemyid", "enemyname", "hp", "movespeed", "buildcontribution", "buildcharges", "expreward", "prefab" };
            foreach (var col in required)
            {
                if (!colMap.ContainsKey(col))
                    throw new FormatException($"{LOG_PREFIX} Missing required column: '{col}'");
            }

            for (int lineIdx = 1; lineIdx < lines.Length; lineIdx++)
            {
                var fields = lines[lineIdx].Split(',');
                if (fields.Length < required.Length) continue;

                try
                {
                    var data = new EnemyData
                    {
                        EnemyId = fields[colMap["enemyid"]].Trim(),
                        EnemyName = fields[colMap["enemyname"]].Trim(),
                        Hp = ParseFloat(fields[colMap["hp"]]),
                        MoveSpeed = ParseFloat(fields[colMap["movespeed"]]),
                        BuildContribution = int.Parse(fields[colMap["buildcontribution"]].Trim()),
                        BuildCharges = int.Parse(fields[colMap["buildcharges"]].Trim()),
                        ExpReward = int.Parse(fields[colMap["expreward"]].Trim()),
                        Prefab = fields[colMap["prefab"]].Trim()
                    };

                    // Optional ability columns
                    if (colMap.TryGetValue("abilitytype", out int atIdx) && atIdx < fields.Length)
                        data.AbilityType = fields[atIdx].Trim();
                    if (colMap.TryGetValue("abilityradius", out int arIdx) && arIdx < fields.Length && !string.IsNullOrWhiteSpace(fields[arIdx]))
                        data.AbilityRadius = ParseFloat(fields[arIdx]);
                    if (colMap.TryGetValue("abilityvalue", out int avIdx) && avIdx < fields.Length && !string.IsNullOrWhiteSpace(fields[avIdx]))
                        data.AbilityValue = ParseFloat(fields[avIdx]);
                    if (colMap.TryGetValue("abilitycooldown", out int acIdx) && acIdx < fields.Length && !string.IsNullOrWhiteSpace(fields[acIdx]))
                        data.AbilityCooldown = ParseFloat(fields[acIdx]);
                    if (colMap.TryGetValue("buildtime", out int btIdx) && btIdx < fields.Length && !string.IsNullOrWhiteSpace(fields[btIdx]))
                        data.BuildTime = ParseFloat(fields[btIdx]);

                    results.Add(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{LOG_PREFIX} Error parsing line {lineIdx + 1}: {ex.Message}");
                }
            }

            return results;
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value.Trim(), CultureInfo.InvariantCulture);
        }
    }
}
