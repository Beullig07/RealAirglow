using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealAirglow.Editor
{
    /// <summary>Builds the three shader bundles into GameData/RealAirglow/Shaders:
    /// realairglow-windows.bundle (DirectX 11 + OpenGL Core), realairglow-macos.bundle (Metal +
    /// OpenGL Core) and realairglow-linux.bundle (OpenGL Core). The macOS and Linux targets need
    /// the "Mac Build Support (Mono)" and "Linux Build Support (Mono)" modules of Unity
    /// 2019.4.18f1. Batch mode:
    /// Unity.exe -batchmode -quit -projectPath unity/RealAirglowShaders -executeMethod RealAirglow.Editor.BuildBundles.Build</summary>
    public static class BuildBundles
    {
        public const string BundleName = "realairglow";

        private struct Platform
        {
            public string Suffix;
            public BuildTarget Target;
            public GraphicsDeviceType[] Apis;
            public string Module;
        }

        // OpenGL Core goes into every bundle: it is what KSP uses on Linux, and what it uses on
        // Windows and macOS when started with -force-glcore.
        private static readonly Platform[] Platforms =
        {
            new Platform { Suffix = "windows", Target = BuildTarget.StandaloneWindows64,
                Apis = new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.OpenGLCore }, Module = "Windows Build Support" },
            new Platform { Suffix = "macos", Target = BuildTarget.StandaloneOSX,
                Apis = new[] { GraphicsDeviceType.Metal, GraphicsDeviceType.OpenGLCore }, Module = "Mac Build Support (Mono)" },
            new Platform { Suffix = "linux", Target = BuildTarget.StandaloneLinux64,
                Apis = new[] { GraphicsDeviceType.OpenGLCore }, Module = "Linux Build Support (Mono)" },
        };

        [MenuItem("RealAirglow/Build shader bundles")]
        public static void Build()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.GetFullPath(Path.Combine(project, "..", "..", "GameData", "RealAirglow", "Shaders"));
            Directory.CreateDirectory(outDir);
            foreach (var p in Platforms)
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, p.Target))
                    throw new System.Exception("RealAirglow: Unity 2019.4.18f1 is missing the \"" + p.Module +
                        "\" module (Unity Hub > Installs > Add modules); it is needed for the " + p.Suffix + " bundle");
            foreach (var p in Platforms) BuildOne(p, project, outDir);
        }

        private static void BuildOne(Platform p, string project, string outDir)
        {
            string tmp = Path.Combine(project, "Build", p.Suffix);
            Directory.CreateDirectory(tmp);
            // The bundle's shader programs are compiled for the target's graphics API list. Set
            // it for this build only, then go back to the project default so ProjectSettings
            // stays unchanged.
            bool useDefault = PlayerSettings.GetUseDefaultGraphicsAPIs(p.Target);
            var previous = PlayerSettings.GetGraphicsAPIs(p.Target);
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(p.Target, false);
                PlayerSettings.SetGraphicsAPIs(p.Target, p.Apis);
                var build = new AssetBundleBuild
                {
                    assetBundleName = BundleName,
                    assetNames = new[] { "Assets/Shaders/Airglow.shader" },
                };
                var manifest = BuildPipeline.BuildAssetBundles(tmp, new[] { build },
                    BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    p.Target);
                if (manifest == null) throw new System.Exception("asset bundle build failed for " + p.Suffix);
            }
            finally
            {
                if (!useDefault) PlayerSettings.SetGraphicsAPIs(p.Target, previous);
                PlayerSettings.SetUseDefaultGraphicsAPIs(p.Target, useDefault);
            }
            string dest = Path.Combine(outDir, BundleName + "-" + p.Suffix + ".bundle");
            File.Copy(Path.Combine(tmp, BundleName), dest, true);
            Debug.Log("RealAirglow: wrote " + dest + " (" + string.Join(", ", p.Apis) + ")");
        }
    }
}
