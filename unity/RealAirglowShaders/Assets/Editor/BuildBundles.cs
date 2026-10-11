using System.IO;
using UnityEditor;
using UnityEngine;

namespace RealAirglow.Editor
{
    /// <summary>Builds GameData/RealAirglow/Shaders/realairglow-windows.bundle (DX11 only; the
    /// owner does not need macOS/Linux). Batch mode:
    /// Unity.exe -batchmode -quit -projectPath unity/RealAirglowShaders -executeMethod RealAirglow.Editor.BuildBundles.Build</summary>
    public static class BuildBundles
    {
        public const string BundleName = "realairglow";

        [MenuItem("RealAirglow/Build shader bundle")]
        public static void Build()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string tmp = Path.Combine(project, "Build", "windows");
            string outDir = Path.GetFullPath(Path.Combine(project, "..", "..", "GameData", "RealAirglow", "Shaders"));
            Directory.CreateDirectory(tmp);
            Directory.CreateDirectory(outDir);
            var build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { "Assets/Shaders/Airglow.shader" },
            };
            var manifest = BuildPipeline.BuildAssetBundles(tmp, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new System.Exception("asset bundle build failed");
            File.Copy(Path.Combine(tmp, BundleName), Path.Combine(outDir, BundleName + "-windows.bundle"), true);
            Debug.Log("RealAirglow: wrote " + Path.Combine(outDir, BundleName + "-windows.bundle"));
        }
    }
}
