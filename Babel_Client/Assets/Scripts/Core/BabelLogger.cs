// ============================================================================
// BabelLogger.cs
// Structured logging for AI-driven acceptance testing via Unity Console.
// Does NOT replace existing Debug.Log — provides a standardized format for
// new code so that logs can be grepped by prefix.
//
// Design doc: Sprint-3 AI 自主验收日志规范
// ============================================================================
// Usage examples (do not call directly in this file):
//   BabelLogger.Event(BabelLogger.Tags.Enemy, "Worker spawned at (2,3)");
//   BabelLogger.AC("S3-14", "Enemy moved to passage slot idx=2");
//   BabelLogger.Warn(BabelLogger.Tags.Tower, "No passage slot available");
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

/// <summary>
/// Structured logger with fixed prefixes for grep-friendly Unity Console output.
/// Compiled away in Release builds (zero overhead).
/// </summary>
public static class BabelLogger
{
    // ── Common tag constants ────────────────────────────────────────────

    public static class Tags
    {
        public const string Enemy  = "Enemy";
        public const string Combat = "Combat";
        public const string Skill  = "Skill";
        public const string Tower  = "Tower";
        public const string Game   = "Game";
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// Log a gameplay state event (low-frequency: wave start, enemy death, etc.).
    /// Format: [BABEL][EVENT][tag] message
    /// </summary>
    public static void Event(string tag, string message)
    {
        Debug.Log(string.Concat("[BABEL][EVENT][", tag, "] ", message));
    }

    /// <summary>
    /// Log an acceptance-check result for a Sprint task.
    /// Format: [BABEL][AC][taskId] message
    /// </summary>
    public static void AC(string taskId, string message)
    {
        Debug.Log(string.Concat("[BABEL][AC][", taskId, "] ", message));
    }

    /// <summary>
    /// Log a warning.
    /// Format: [BABEL][WARN][tag] message
    /// </summary>
    public static void Warn(string tag, string message)
    {
        Debug.LogWarning(string.Concat("[BABEL][WARN][", tag, "] ", message));
    }
}

#endif
