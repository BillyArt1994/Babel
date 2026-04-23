using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Babel
{
    public static class WaveParser
    {
        private const string LOG_PREFIX = "[BABEL][WaveParser]";

        public static List<WaveEvent> Parse(string csvText)
        {
            var results = new List<WaveEvent>();
            if (string.IsNullOrEmpty(csvText)) return results;

            if (csvText.Length > 0 && csvText[0] == '\uFEFF')
                csvText = csvText.Substring(1);

            var lines = csvText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return results;

            var header = lines[0].Split(',');
            var colMap = new Dictionary<string, int>();
            for (int i = 0; i < header.Length; i++)
                colMap[header[i].Trim().ToLower()] = i;

            string[] required = { "starttime", "endtime", "mode", "enemypool", "countmin", "countmax", "interval", "spawnside" };
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
                    var evt = new WaveEvent
                    {
                        StartTime = ParseFloat(fields[colMap["starttime"]]),
                        EndTime = ParseFloat(fields[colMap["endtime"]]),
                        Mode = ParseMode(fields[colMap["mode"]].Trim()),
                        EnemyPool = ParsePool(fields[colMap["enemypool"]].Trim()),
                        CountMin = int.Parse(fields[colMap["countmin"]].Trim()),
                        CountMax = int.Parse(fields[colMap["countmax"]].Trim()),
                        Interval = ParseFloat(fields[colMap["interval"]]),
                        Side = ParseSide(fields[colMap["spawnside"]].Trim())
                    };
                    results.Add(evt);
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

        private static SpawnMode ParseMode(string value)
        {
            return value switch
            {
                "Burst" => SpawnMode.Burst,
                "Maintain" => SpawnMode.Maintain,
                "Timed" => SpawnMode.Timed,
                _ => throw new FormatException($"Unknown spawn mode: '{value}'")
            };
        }

        private static SpawnSide ParseSide(string value)
        {
            return value switch
            {
                "Left" => SpawnSide.Left,
                "Right" => SpawnSide.Right,
                "Both" => SpawnSide.Both,
                "Random" => SpawnSide.Random,
                _ => throw new FormatException($"Unknown spawn side: '{value}'")
            };
        }

        private static List<PoolEntry> ParsePool(string value)
        {
            var pool = new List<PoolEntry>();
            var entries = value.Split('|');
            foreach (var entry in entries)
            {
                var parts = entry.Split(':');
                if (parts.Length != 2)
                    throw new FormatException($"Invalid pool entry: '{entry}'. Expected 'enemyId:weight'");

                pool.Add(new PoolEntry(
                    parts[0].Trim(),
                    float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture)
                ));
            }
            return pool;
        }
    }
}
