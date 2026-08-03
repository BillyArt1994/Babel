using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Babel.EditorTools.Validation
{
    public enum BabelValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class BabelValidationIssue
    {
        public BabelValidationIssue(
            string code,
            BabelValidationSeverity severity,
            string message,
            string assetPath,
            UnityEngine.Object context)
        {
            Code = code;
            Severity = severity;
            Message = message;
            AssetPath = assetPath;
            Context = context;
        }

        public string Code { get; private set; }
        public BabelValidationSeverity Severity { get; private set; }
        public string Message { get; private set; }
        public string AssetPath { get; private set; }
        public UnityEngine.Object Context { get; private set; }

        public override string ToString()
        {
            string location = string.IsNullOrEmpty(AssetPath) ? string.Empty : " (" + AssetPath + ")";
            return "[" + Code + "] " + Message + location;
        }
    }

    public sealed class BabelValidationReport
    {
        private readonly List<BabelValidationIssue> _issues = new List<BabelValidationIssue>();

        public IReadOnlyList<BabelValidationIssue> Issues { get { return _issues; } }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors { get { return ErrorCount > 0; } }

        public void AddError(string code, string message, string assetPath = null, UnityEngine.Object context = null)
        {
            _issues.Add(new BabelValidationIssue(code, BabelValidationSeverity.Error, message, assetPath, context));
            ErrorCount++;
        }

        public void AddWarning(string code, string message, string assetPath = null, UnityEngine.Object context = null)
        {
            _issues.Add(new BabelValidationIssue(code, BabelValidationSeverity.Warning, message, assetPath, context));
            WarningCount++;
        }

        public void Merge(BabelValidationReport other)
        {
            if (other == null) return;
            for (int i = 0; i < other._issues.Count; i++)
            {
                BabelValidationIssue issue = other._issues[i];
                _issues.Add(issue);
                if (issue.Severity == BabelValidationSeverity.Error) ErrorCount++;
                else WarningCount++;
            }
        }

        public string GetSummary()
        {
            return "Babel validation: " + ErrorCount + " error(s), " + WarningCount + " warning(s).";
        }

        public string ToBuildMessage()
        {
            var builder = new StringBuilder(GetSummary());
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Severity != BabelValidationSeverity.Error) continue;
                builder.AppendLine();
                builder.Append(_issues[i].ToString());
            }
            return builder.ToString();
        }

        public void Log()
        {
            for (int i = 0; i < _issues.Count; i++)
            {
                BabelValidationIssue issue = _issues[i];
                if (issue.Severity == BabelValidationSeverity.Error)
                {
                    if (issue.Context == null) Debug.LogError(issue.ToString());
                    else Debug.LogError(issue.ToString(), issue.Context);
                }
                else
                {
                    if (issue.Context == null) Debug.LogWarning(issue.ToString());
                    else Debug.LogWarning(issue.ToString(), issue.Context);
                }
            }

            if (!HasErrors)
                Debug.Log("[Babel][Validation] " + GetSummary());
        }
    }

    internal static class BabelValidationPaths
    {
        internal const string Manifest = "Assets/Babel/Content/Manifests/GameContentManifest.asset";
        internal const string BootScene = "Assets/Babel/Scenes/Boot/BootScene.unity";
    }
}
