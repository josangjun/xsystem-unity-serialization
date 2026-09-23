#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XSystem.EditorInternal
{
    public static class InspectBuildReport
    {
        [MenuItem("Tools/Inspect Last Build Report")]
        private static void Inspect()
        {
            BuildReport report = BuildReport.GetLatestReport();

            if (report == null)
            {
                Debug.LogWarning("LastBuild.buildreport를 찾을 수 없습니다.");
                return;
            }

            string outputPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "BuildReport.txt");

            var builder = new StringBuilder();
            builder.AppendLine("Unity Build Report");
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();

            builder.AppendLine("=== Build Files (descending size) ===");
            foreach (var file in report.GetFiles().OrderByDescending(file => file.size))
            {
                builder.AppendLine(
                    $"{FormatBytes(file.size),10}  {file.size,12} bytes  {file.path}");
            }

            builder.AppendLine();
            builder.AppendLine("=== Packed Asset Files (descending size) ===");
            foreach (var packed in report.packedAssets.OrderByDescending(GetPackedFileSize))
            {
                ulong totalSize = GetPackedFileSize(packed);
                builder.AppendLine();
                builder.AppendLine(
                    $"{FormatBytes(totalSize),10}  {totalSize,12} bytes  {packed.shortPath}");
                builder.AppendLine(
                    $"  overhead: {FormatBytes(packed.overhead)} ({packed.overhead} bytes)");

                foreach (var asset in packed.contents.OrderByDescending(asset => asset.packedSize))
                {
                    builder.AppendLine(
                        $"  {FormatBytes(asset.packedSize),10}  {asset.packedSize,12} bytes  {asset.sourceAssetPath}");
                }
            }

            File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
            Debug.Log($"Build Report를 저장했습니다: {outputPath}");
        }

        private static ulong GetPackedFileSize(PackedAssets packed)
        {
            ulong totalSize = packed.overhead;

            foreach (var asset in packed.contents)
            {
                totalSize += asset.packedSize;
            }

            return totalSize;
        }

        private static string FormatBytes(ulong bytes)
        {
            const double kilobyte = 1024d;
            const double megabyte = kilobyte * 1024d;
            const double gigabyte = megabyte * 1024d;

            if (bytes >= gigabyte)
            {
                return $"{bytes / gigabyte:0.00} GB";
            }

            if (bytes >= megabyte)
            {
                return $"{bytes / megabyte:0.00} MB";
            }

            if (bytes >= kilobyte)
            {
                return $"{bytes / kilobyte:0.00} KB";
            }

            return $"{bytes} B";
        }
    }
}
#endif
