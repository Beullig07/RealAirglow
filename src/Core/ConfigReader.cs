using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RealAirglow.Core
{
    /// <summary>Read-only view of a config node, so the reader works with KSP's ConfigNode in
    /// game and with a plain text parser in tests.</summary>
    public interface IConfigView
    {
        string GetValue(string key);
        IEnumerable<IConfigView> GetNodes(string name);
    }

    /// <summary>One body's airglow definition (an AIRGLOW node).</summary>
    public sealed class BodyAirglow
    {
        public string Body = "Earth";
        public double F107Reference = 100.0;
        public double? DefaultMaster;
        public LowerAtmosphereDef Lower = new LowerAtmosphereDef();
        public List<LayerDef> Layers = new List<LayerDef>();
    }

    public static class ConfigReader
    {
        public static BodyAirglow Read(IConfigView node)
        {
            var b = new BodyAirglow
            {
                Body = node.GetValue("body") ?? "Earth",
                F107Reference = Num(node, "f107Reference", 100.0),
            };
            if (node.GetValue("defaultMasterIntensity") != null)
                b.DefaultMaster = Num(node, "defaultMasterIntensity", DisplayControls.DefaultMaster);
            var lower = node.GetNodes("LOWER_ATMOSPHERE").FirstOrDefault();
            if (lower != null)
            {
                b.Lower.AirLogDensityPoly = Vec(lower, "airLogDensityPoly", -1);
                b.Lower.OzoneColumnDU = Num(lower, "ozoneColumn_DU", 300);
                b.Lower.OzonePeakKm = Num(lower, "ozonePeak_km", 22);
                b.Lower.OzoneFwhmKm = Num(lower, "ozoneFwhm_km", 15);
                b.Lower.AerosolTau550 = Num(lower, "aerosolTau550", 0.05);
                b.Lower.AerosolScaleHeightKm = Num(lower, "aerosolScaleHeight_km", 1.2);
                b.Lower.TopKm = Num(lower, "top_km", 60);
            }
            foreach (var n in node.GetNodes("LAYER"))
            {
                b.Layers.Add(new LayerDef
                {
                    Name = n.GetValue("name") ?? "",
                    Group = n.GetValue("group") ?? "",
                    Enabled = !string.Equals(n.GetValue("enabled"), "False", StringComparison.OrdinalIgnoreCase),
                    PeakKm = Num(n, "peak_km", 0),
                    HwhmLowKm = Num(n, "hwhmLow_km", 1),
                    HwhmHighKm = Num(n, "hwhmHigh_km", 1),
                    IntensityR = Num(n, "intensity_R", 0),
                    SolarCoefficient = Num(n, "solarCoefficient", 0),
                    CameraRGB = Vec(n, "cameraRGB", 3),
                    CieRGB = Vec(n, "cieRGB", 3),
                    ScotopicY = Num(n, "scotopicY", 0),
                    SigmaRayleigh = Vec(n, "sigmaRayleigh", 3),
                    SigmaOzone = Vec(n, "sigmaOzone", 3),
                    AerosolFactor = Vec(n, "aerosolFactor", 3),
                });
            }
            return b;
        }

        private static string Strip(string s)
        {
            if (s == null) return null;
            int c = s.IndexOf("//", StringComparison.Ordinal);
            return (c >= 0 ? s.Substring(0, c) : s).Trim();
        }

        private static double Num(IConfigView n, string key, double fallback)
        {
            var s = Strip(n.GetValue(key));
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        private static double[] Vec(IConfigView n, string key, int expected)
        {
            var s = Strip(n.GetValue(key));
            if (string.IsNullOrEmpty(s)) return new double[Math.Max(expected, 0)];
            var parts = s.Split(',').Select(p => double.Parse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            if (expected > 0 && parts.Length != expected)
                throw new FormatException($"{key}: expected {expected} values, got {parts.Length}");
            return parts;
        }
    }

    /// <summary>Minimal KSP-style config text parser (name = value, NAME { ... }), for tests and
    /// tools outside the game. Comments after // are kept in values and stripped by the reader.</summary>
    public sealed class TextConfigNode : IConfigView
    {
        public readonly string Name;
        private readonly List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
        private readonly List<TextConfigNode> nodes = new List<TextConfigNode>();

        private TextConfigNode(string name) { Name = name; }

        public string GetValue(string key) => values.Where(kv => kv.Key == key).Select(kv => kv.Value).FirstOrDefault();
        public IEnumerable<IConfigView> GetNodes(string name) => nodes.Where(n => n.Name == name);

        public static TextConfigNode Parse(string text)
        {
            var root = new TextConfigNode("root");
            var stack = new Stack<TextConfigNode>();
            stack.Push(root);
            string pending = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("//") || line.Length == 0) continue;
                if (line == "{")
                {
                    var n = new TextConfigNode(pending ?? "");
                    stack.Peek().nodes.Add(n);
                    stack.Push(n);
                    pending = null;
                }
                else if (line == "}") stack.Pop();
                else if (line.Contains("="))
                {
                    int i = line.IndexOf('=');
                    stack.Peek().values.Add(new KeyValuePair<string, string>(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim()));
                }
                else pending = line;
            }
            return root;
        }
    }
}
