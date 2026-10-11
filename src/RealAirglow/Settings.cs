using System;
using System.Collections.Generic;
using System.IO;
using RealAirglow.Core;
using UnityEngine.Rendering;

namespace RealAirglow
{
    /// <summary>Auto: metered exposure (AutoExposure). Fixed: always the ISS night exposure.</summary>
    public enum ExposureMode { Auto = 0, Fixed = 1 }

    /// <summary>Options shown in the debug window, persisted alongside the display controls.</summary>
    public sealed class RuntimeOptions
    {
        public string Body = "Earth";
        public SpectralMode Mode = SpectralMode.Camera;
        public double F107 = 100.0;              // sfu, PALACE reference (owner confirmed)
        public bool Extinction = true;
        public ExposureMode Exposure = ExposureMode.Auto;
        // Debug only: weight emission by the fraction of the solar disc hidden by the planet at
        // the emitting point. Not physical (nightglow does not stop at the terminator); off by
        // default since the metered exposure handles daylight.
        public bool NightGate = false;
        public CameraEvent LocalEvent = CameraEvent.AfterImageEffectsOpaque;   // behind-atmosphere part, before clouds
        public CameraEvent FrontEvent = CameraEvent.AfterForwardAlpha;         // in-front part, after clouds
        public CameraEvent ScaledEvent = CameraEvent.AfterForwardAlpha;
        public string HotkeyKey = "G";           // with Alt

        public void Save(Dictionary<string, string> d)
        {
            d["body"] = Body;
            d["spectralMode"] = Mode.ToString();
            d["f107"] = F107.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            d["extinction"] = Extinction.ToString();
            d["exposure"] = Exposure.ToString();
            d["nightGateDebug"] = NightGate.ToString();
            d["frontEvent"] = FrontEvent.ToString();
            d["localEvent"] = LocalEvent.ToString();
            d["scaledEvent"] = ScaledEvent.ToString();
            d["hotkey"] = HotkeyKey;
        }

        public void Load(IReadOnlyDictionary<string, string> d)
        {
            if (d.TryGetValue("body", out var b) && b.Length > 0) Body = b;
            if (d.TryGetValue("spectralMode", out var m) && Enum.TryParse(m, out SpectralMode sm)) Mode = sm;
            if (d.TryGetValue("f107", out var f) && double.TryParse(f, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var fv)) F107 = fv;
            if (d.TryGetValue("extinction", out var e) && bool.TryParse(e, out var ev)) Extinction = ev;
            if (d.TryGetValue("exposure", out var x) && Enum.TryParse(x, out ExposureMode xm)) Exposure = xm;
            // "nightGateDebug" replaces the Phase 2 key "nightGate" so an old saved True is ignored.
            if (d.TryGetValue("nightGateDebug", out var n) && bool.TryParse(n, out var nv)) NightGate = nv;
            if (d.TryGetValue("frontEvent", out var fe) && Enum.TryParse(fe, out CameraEvent fev)) FrontEvent = fev;
            if (d.TryGetValue("localEvent", out var le) && Enum.TryParse(le, out CameraEvent lev)) LocalEvent = lev;
            if (d.TryGetValue("scaledEvent", out var se) && Enum.TryParse(se, out CameraEvent sev)) ScaledEvent = sev;
            if (d.TryGetValue("hotkey", out var h) && h.Length > 0) HotkeyKey = h;
        }
    }

    /// <summary>Reads and writes GameData/RealAirglow/PluginData/settings.cfg. PluginData is not
    /// scanned by ModuleManager, so saving does not invalidate its cache.</summary>
    public static class SettingsStore
    {
        private const string NodeName = "REALAIRGLOW_SETTINGS";

        public static string FilePath =>
            Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "RealAirglow", "PluginData", "settings.cfg");

        public static Dictionary<string, string> Load()
        {
            var d = new Dictionary<string, string>();
            if (!File.Exists(FilePath)) return d;
            var root = ConfigNode.Load(FilePath);
            var node = root?.GetNode(NodeName);
            if (node == null) return d;
            foreach (ConfigNode.Value v in node.values) d[v.name] = v.value;
            return d;
        }

        public static void Save(DisplayControls controls, RuntimeOptions options)
        {
            var d = controls.Save();
            options.Save(d);
            var root = new ConfigNode();
            var node = root.AddNode(NodeName);
            foreach (var kv in d) node.AddValue(kv.Key, kv.Value);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            root.Save(FilePath);
        }
    }

    /// <summary>IConfigView over KSP's ConfigNode (after ModuleManager patching).</summary>
    public sealed class KspConfigView : IConfigView
    {
        private readonly ConfigNode node;
        public KspConfigView(ConfigNode node) { this.node = node; }
        public string GetValue(string key) => node.GetValue(key);
        public IEnumerable<IConfigView> GetNodes(string name)
        {
            foreach (var n in node.GetNodes(name)) yield return new KspConfigView(n);
        }
    }
}
