using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RealAirglow.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealAirglow.Editor
{
    /// <summary>
    /// Renders fixed test views with the real shader and the real material binder, writing
    /// linear float output (exposure 1, no sRGB encoding, so pixels are luminance-scaled linear
    /// sRGB in cd m^-2) for reference/compare_shader.py. Batch mode (needs a GPU, so no -nographics):
    /// Unity.exe -batchmode -quit -projectPath unity/RealAirglowShaders -executeMethod RealAirglow.Editor.ComparisonHarness.Run
    /// </summary>
    public static class ComparisonHarness
    {
        private const double RadiusKm = 6371.0;

        private struct ViewSpec
        {
            public string Name;
            public double AltKm;        // observer altitude above the reference sphere
            public double ElevationDeg; // view direction elevation above the local horizontal
            public float VFovDeg;
            public int Width, Height;
            public bool Extinction;
        }

        private static readonly ViewSpec[] Views =
        {
            new ViewSpec { Name = "iss_limb", AltKm = 420, ElevationDeg = -16.18, VFovDeg = 6.9f, Width = 300, Height = 200, Extinction = true },
            new ViewSpec { Name = "iss_wide", AltKm = 420, ElevationDeg = -28.8, VFovDeg = 53.1f, Width = 300, Height = 200, Extinction = true },
            new ViewSpec { Name = "ground_horizon", AltKm = 0.001, ElevationDeg = 20.0, VFovDeg = 50.0f, Width = 300, Height = 200, Extinction = true },
        };

        [MenuItem("RealAirglow/Run comparison harness")]
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repo = Path.GetFullPath(Path.Combine(project, "..", ".."));
            string outDir = Path.Combine(repo, "reference", "output", "harness");
            Directory.CreateDirectory(outDir);
            var cfg = TextConfigNode.Parse(File.ReadAllText(Path.Combine(repo, "GameData", "RealAirglow", "Config", "Earth.cfg")));
            var body = ConfigReader.Read(cfg.GetNodes("AIRGLOW").Single());
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Airglow.shader");
            var material = new Material(shader);
            var quad = AirglowMaterialBinder.FullScreenQuad();

            foreach (var v in Views)
            {
                var go = new GameObject("harness camera");
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.fieldOfView = v.VFovDeg;
                cam.aspect = (float)v.Width / v.Height;
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000f;
                // Observer on the +x axis, looking along +y tilted by the elevation; up = +x.
                double el = v.ElevationDeg * Math.PI / 180;
                var fwd = new Vector3((float)Math.Sin(el), (float)Math.Cos(el), 0f);
                go.transform.rotation = Quaternion.LookRotation(fwd, new Vector3(1, 0, 0));
                var rt = new RenderTexture(v.Width, v.Height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                cam.targetTexture = rt;

                AirglowMaterialBinder.BindModel(material, body, g => 1.0, body.F107Reference, SpectralMode.Camera,
                    v.Extinction, nightGate: false, exposure: 1.0, gammaSpace: false);
                var view = new AirglowView
                {
                    CamPosKm = new Double3(RadiusKm + v.AltKm, 0, 0),
                    CamForward = go.transform.forward,
                    FrustumRays = AirglowMaterialBinder.FrustumRays(cam),
                    WorldUnitKm = 0.001f,
                    SunDir = new Vector3(-1, 0, 0),
                };
                AirglowMaterialBinder.BindView(material, view, RadiusKm, useDepth: false);
                var cb = new CommandBuffer { name = "RealAirglow harness" };
                cb.DrawMesh(quad, Matrix4x4.identity, material, 0, 0);
                cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(v.Width, v.Height, TextureFormat.RGBAFloat, false, true);
                tex.ReadPixels(new Rect(0, 0, v.Width, v.Height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var px = tex.GetRawTextureData<float>().ToArray();   // row 0 = bottom
                var bytes = new byte[px.Length * 4];
                Buffer.BlockCopy(px, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(Path.Combine(outDir, v.Name + ".f32"), bytes);

                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.AppendFormat(CultureInfo.InvariantCulture, "  \"width\": {0}, \"height\": {1}, \"radius_km\": {2}, \"alt_km\": {3}, \"extinction\": {4},\n",
                    v.Width, v.Height, RadiusKm, v.AltKm, v.Extinction ? "true" : "false");
                sb.Append("  \"frustum_rays\": [");
                sb.Append(string.Join(", ", view.FrustumRays.Select(r => string.Format(CultureInfo.InvariantCulture, "[{0:R}, {1:R}, {2:R}]", r.x, r.y, r.z))));
                sb.Append("],\n  \"rows\": \"bottom_to_top\", \"channels\": \"RGBA float32\"\n}\n");
                File.WriteAllText(Path.Combine(outDir, v.Name + ".json"), sb.ToString());

                cam.RemoveCommandBuffer(CameraEvent.AfterEverything, cb);
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(go);
                Debug.Log("RealAirglow harness: wrote " + v.Name);
            }
        }
    }
}
