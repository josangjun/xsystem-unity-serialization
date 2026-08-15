#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class BuildInfoGenerator : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        string commitHash = GetGitCommitHash();
        string buildTime = DateTimeOffset.Now.ToString("yyMMdd HH:mm");
        string json = JsonUtility.ToJson(new BuildInfoData
        {
            commitHash = commitHash,
            buildTime = buildTime
        });

        string buildInfoPath = Path.Combine(Application.dataPath, "StreamingAssets", "BuildInfo.json");
        File.WriteAllText(buildInfoPath, json + Environment.NewLine);
    }

    private static string GetGitCommitHash()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("git", "rev-parse --short=10 HEAD")
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 && output.Length == 10 ? output : "unknown";
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"Could not read the Git commit hash for build metadata: {exception.Message}");
            return "unknown";
        }
    }

    [Serializable]
    private sealed class BuildInfoData
    {
        public string commitHash = string.Empty;
        public string buildTime = string.Empty;
    }
}
#endif