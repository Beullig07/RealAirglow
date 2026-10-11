using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealAirglow.Core
{
    /// <summary>
    /// The user's airglow intensity controls (docs/SPEC.md, "Airglow intensity controls").
    /// These are display multipliers applied to the final airglow radiance only; they never
    /// touch the physical model (rayleighs, profiles, colours) or anything outside airglow.
    /// Unity-free so it can be unit tested.
    /// </summary>
    public sealed class DisplayControls
    {
        public const double MaxMultiplier = 3.0;      // 300 %
        public const double DefaultMaster = 0.30;     // owner default: 30 % of photographic
        public const double SmoothingSeconds = 0.2;   // brief: smooth changes over ~0.2 s

        /// <summary>Slider groups, in display order. "continuum" covers FeO-like + NO2.</summary>
        public static readonly string[] Groups = { "green", "red", "OH", "Na", "continuum", "O2" };

        public static readonly IReadOnlyDictionary<string, string> GroupLabels = new Dictionary<string, string>
        {
            { "green", "Green line 557.7 nm (mesosphere + F region)" },
            { "red", "Red line 630/636 nm" },
            { "OH", "OH Meinel bands" },
            { "Na", "Sodium D 589 nm" },
            { "continuum", "Continuum (FeO-like + NO₂)" },
            { "O2", "O₂ atmospheric bands" },
        };

        public static readonly (string Name, double Master)[] Presets =
        {
            ("Realistic photographic", 1.00),
            ("My default", 0.30),
            ("Subtle", 0.15),
        };

        private readonly Dictionary<string, double> groupTarget = new Dictionary<string, double>();
        private readonly Dictionary<string, Ramp> groupRamp = new Dictionary<string, Ramp>();
        private Ramp masterRamp;
        private double defaultMaster = DefaultMaster;

        public bool Enabled { get; set; } = true;
        public double MasterTarget { get; private set; } = DefaultMaster;

        public DisplayControls()
        {
            masterRamp = new Ramp(DefaultMaster);
            foreach (var g in Groups)
            {
                groupTarget[g] = 1.0;
                groupRamp[g] = new Ramp(1.0);
            }
        }

        /// <summary>A body's AIRGLOW node may set its own default master; a saved user value
        /// overrides it (call this before Load).</summary>
        public void SetBodyDefaultMaster(double value)
        {
            defaultMaster = Clamp(value);
            SetMaster(defaultMaster, immediate: true);
        }

        public static double Clamp(double v) => double.IsNaN(v) ? 0.0 : Math.Max(0.0, Math.Min(MaxMultiplier, v));

        public void SetMaster(double value, bool immediate = false)
        {
            MasterTarget = Clamp(value);
            masterRamp.SetTarget(MasterTarget, immediate);
        }

        public double GetGroupTarget(string group) => groupTarget[group];

        public void SetGroup(string group, double value, bool immediate = false)
        {
            if (!groupTarget.ContainsKey(group)) throw new ArgumentException("unknown group " + group);
            groupTarget[group] = Clamp(value);
            groupRamp[group].SetTarget(groupTarget[group], immediate);
        }

        public void ResetToDefaults()
        {
            Enabled = true;
            SetMaster(defaultMaster);
            foreach (var g in Groups) SetGroup(g, 1.0);
        }

        public void ApplyPreset(int index) => SetMaster(Presets[index].Master);

        /// <summary>Advance the 0.2 s smoothing ramps.</summary>
        public void Update(double dtSeconds)
        {
            masterRamp.Advance(dtSeconds);
            foreach (var r in groupRamp.Values) r.Advance(dtSeconds);
        }

        /// <summary>Final display multiplier for one group: master x group, or exactly zero when
        /// airglow is switched off. Linear in each factor.</summary>
        public double Effective(string group) => Enabled ? masterRamp.Value * groupRamp[group].Value : 0.0;

        /// <summary>Exposure value relative to full photographic brightness, log2(master).</summary>
        public static double ExposureValue(double master) => master > 0 ? Math.Log(master, 2.0) : double.NegativeInfinity;

        // --- persistence (key/value strings; the KSP layer maps these to a ConfigNode) ---------

        public Dictionary<string, string> Save()
        {
            var d = new Dictionary<string, string>
            {
                ["enabled"] = Enabled ? "True" : "False",
                ["master"] = MasterTarget.ToString("R", CultureInfo.InvariantCulture),
            };
            foreach (var g in Groups)
                d["layer_" + g] = groupTarget[g].ToString("R", CultureInfo.InvariantCulture);
            return d;
        }

        public void Load(IReadOnlyDictionary<string, string> d)
        {
            if (d.TryGetValue("enabled", out var e)) Enabled = !string.Equals(e, "False", StringComparison.OrdinalIgnoreCase);
            if (d.TryGetValue("master", out var m) && TryParse(m, out var mv)) SetMaster(mv, immediate: true);
            foreach (var g in Groups)
                if (d.TryGetValue("layer_" + g, out var s) && TryParse(s, out var v)) SetGroup(g, v, immediate: true);
        }

        private static bool TryParse(string s, out double v) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>Linear ramp that reaches its target exactly after SmoothingSeconds.</summary>
        private sealed class Ramp
        {
            public double Value;
            private double target, rate;

            public Ramp(double v) { Value = target = v; }

            public void SetTarget(double t, bool immediate)
            {
                target = t;
                if (immediate) { Value = t; rate = 0; }
                else rate = Math.Abs(target - Value) / SmoothingSeconds;
            }

            public void Advance(double dt)
            {
                if (Value == target) return;
                double step = rate * dt;
                Value = Math.Abs(target - Value) <= step ? target : Value + Math.Sign(target - Value) * step;
            }
        }
    }
}
