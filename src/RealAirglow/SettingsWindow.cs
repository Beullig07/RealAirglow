using System;
using System.Globalization;
using System.Linq;
using KSP.UI.Screens;
using RealAirglow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealAirglow
{
    /// <summary>
    /// Stock IMGUI settings window (toolbar button + Alt+G) and a separate debug window.
    /// Click-through is blocked with KSP's own InputLockManager while the mouse is over a window.
    /// </summary>
    internal sealed class SettingsWindow : MonoBehaviour
    {
        private const string LockId = "RealAirglowWindowLock";
        private static readonly int SettingsId = "RealAirglow.Settings".GetHashCode();
        private static readonly int DebugId = "RealAirglow.Debug".GetHashCode();

        private static readonly CameraEvent[] Events =
        {
            CameraEvent.AfterForwardOpaque, CameraEvent.BeforeImageEffectsOpaque, CameraEvent.AfterImageEffectsOpaque,
            CameraEvent.BeforeForwardAlpha, CameraEvent.AfterForwardAlpha, CameraEvent.BeforeImageEffects,
            CameraEvent.AfterImageEffects, CameraEvent.AfterEverything,
        };

        private RealAirglowAddon addon;
        private ApplicationLauncherButton button;
        private bool showSettings, showDebug, locked, dirty;
        private Rect settingsRect = new Rect(260, 120, 420, 10), debugRect = new Rect(700, 120, 420, 10);
        private string masterText = "", f107Text = "";
        private float saveAt;

        private void Start()
        {
            addon = GetComponent<RealAirglowAddon>();
            masterText = Percent(addon.Controls.MasterTarget);
            f107Text = addon.Options.F107.ToString("0.#", CultureInfo.InvariantCulture);
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            if (ApplicationLauncher.Ready) AddButton();
        }

        private void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null) return;
            var icon = GameDatabase.Instance.GetTexture("RealAirglow/Icons/toolbar", false) ?? Texture2D.whiteTexture;
            button = ApplicationLauncher.Instance.AddModApplication(
                () => SetSettings(true), () => SetSettings(false), null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION,
                icon);
        }

        private void SetSettings(bool on) { showSettings = on; if (!on) showDebug = false; }

        internal void ToggleSettings()
        {
            showSettings = !showSettings;
            if (button != null) { if (showSettings) button.SetTrue(false); else button.SetFalse(false); }
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime >= saveAt) { addon.Save(); dirty = false; }
        }

        private void MarkDirty() { dirty = true; saveAt = Time.unscaledTime + 1f; }

        private void OnGUI()
        {
            if (addon == null || addon.Controls == null) return;
            if (showSettings) settingsRect = GUILayout.Window(SettingsId, settingsRect, DrawSettings, "RealAirglow");
            if (showSettings && showDebug) debugRect = GUILayout.Window(DebugId, debugRect, DrawDebug, "RealAirglow debug");

            Vector2 mouse = Event.current.mousePosition;
            bool over = (showSettings && settingsRect.Contains(mouse)) || (showSettings && showDebug && debugRect.Contains(mouse));
            if (over && !locked) { InputLockManager.SetControlLock(ControlTypes.ALLBUTCAMERAS, LockId); locked = true; }
            else if (!over && locked) { InputLockManager.RemoveControlLock(LockId); locked = false; }
        }

        // Sliders: position 0 is off; above that, logarithmic. Master spans 0.1 %..300 % so the
        // 1.5 % default sits in the useful part of the travel; layers span 1 %..10000 %.
        private const float SliderOff = 0.02f;
        private const double MasterMin = 0.001, LayerMin = 0.01;
        private static float ToSlider(double v, double min, double max) =>
            v <= 0 ? 0f : SliderOff + (1 - SliderOff) * (float)(Math.Log(Math.Max(v, min) / min) / Math.Log(max / min));
        private static double FromSlider(float s, double min, double max) =>
            s < SliderOff ? 0.0 : min * Math.Pow(max / min, (s - SliderOff) / (1 - SliderOff));
        private static float MasterToSlider(double m) => ToSlider(m, MasterMin, DisplayControls.MaxMultiplier);
        private static double SliderToMaster(float s) => FromSlider(s, MasterMin, DisplayControls.MaxMultiplier);
        private static float LayerToSlider(double l) => ToSlider(l, LayerMin, DisplayControls.MaxLayerMultiplier);
        private static double SliderToLayer(float s) => FromSlider(s, LayerMin, DisplayControls.MaxLayerMultiplier);

        /// <summary>Percent with three significant figures (1.5, 30, 500).</summary>
        private static string Percent(double v)
        {
            double p = v * 100;
            return p.ToString(p >= 100 ? "0" : p >= 10 ? "0.#" : "0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>Round to three significant figures so slider drags land on readable values.</summary>
        private static double Round3(double v) =>
            v <= 0 ? 0 : Math.Round(v, Math.Max(0, Math.Min(15, 2 - (int)Math.Floor(Math.Log10(v)))));

        private void DrawSettings(int id)
        {
            var c = addon.Controls;
            GUILayout.BeginVertical();
            bool en = GUILayout.Toggle(c.Enabled, " Airglow on");
            if (en != c.Enabled) { c.Enabled = en; MarkDirty(); }
            var o = addon.Options;
            bool sun = GUILayout.Toggle(o.Outshine, " Outshined by sunlight (fades through twilight)");
            if (sun != o.Outshine) { o.Outshine = sun; MarkDirty(); }

            double ev = DisplayControls.ExposureValue(c.MasterTarget);
            GUILayout.Label("Master intensity: " + Percent(c.MasterTarget) + " % of photographic  (" +
                            (double.IsNegativeInfinity(ev) ? "off" : ev.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " EV") + ")");
            GUILayout.BeginHorizontal();
            float s = GUILayout.HorizontalSlider(MasterToSlider(c.MasterTarget), 0f, 1f, GUILayout.Width(300));
            double fromSlider = SliderToMaster(s);
            if (Math.Abs(fromSlider - c.MasterTarget) > 1e-4 && Math.Abs(s - MasterToSlider(c.MasterTarget)) > 1e-4)
            {
                c.SetMaster(Round3(fromSlider)); masterText = Percent(c.MasterTarget); MarkDirty();
            }
            masterText = GUILayout.TextField(masterText, GUILayout.Width(50));
            if (GUILayout.Button("Set", GUILayout.Width(40)) &&
                double.TryParse(masterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            {
                c.SetMaster(pct / 100.0); masterText = Percent(c.MasterTarget); MarkDirty();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < DisplayControls.Presets.Length; i++)
                if (GUILayout.Button(DisplayControls.Presets[i].Name + " (" + Percent(DisplayControls.Presets[i].Master) + "%)"))
                {
                    c.ApplyPreset(i); masterText = Percent(c.MasterTarget); MarkDirty();
                }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Per-layer intensity (final = master × layer)");
            foreach (var g in DisplayControls.Groups)
            {
                double l = c.GetGroupTarget(g);
                GUILayout.Label(DisplayControls.GroupLabels[g] + ": " + Percent(l) + " %  (final " + Percent(c.MasterTarget * l) + " %)");
                float pos = LayerToSlider(l);
                float v = GUILayout.HorizontalSlider(pos, 0f, 1f);
                if (Math.Abs(v - pos) > 1e-4) { c.SetGroup(g, Round3(SliderToLayer(v))); MarkDirty(); }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to defaults")) { c.ResetToDefaults(); masterText = Percent(c.MasterTarget); MarkDirty(); }
            if (GUILayout.Button(showDebug ? "Hide debug" : "Debug…")) showDebug = !showDebug;
            GUILayout.EndHorizontal();
            GUILayout.Label("Alt+" + addon.Options.HotkeyKey + " toggles this window.");
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawDebug(int id)
        {
            var o = addon.Options;
            GUILayout.BeginVertical();
            GUILayout.Label("Spectral response");
            GUILayout.BeginHorizontal();
            foreach (SpectralMode m in Enum.GetValues(typeof(SpectralMode)))
                if (GUILayout.Toggle(o.Mode == m, m.ToString(), "Button") && o.Mode != m) { o.Mode = m; MarkDirty(); }
            GUILayout.EndHorizontal();

            bool ext = GUILayout.Toggle(o.Extinction, " Lower-atmosphere extinction");
            if (ext != o.Extinction) { o.Extinction = ext; MarkDirty(); }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Exposure", GUILayout.Width(70));
            foreach (ExposureMode m in Enum.GetValues(typeof(ExposureMode)))
                if (GUILayout.Toggle(o.Exposure == m, m == ExposureMode.Auto ? "Auto (metered)" : "Fixed (ISS night)", "Button") && o.Exposure != m)
                { o.Exposure = m; MarkDirty(); }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Metered scene luminance {addon.Meter.AverageLuminance:G3} cd/m², exposure {addon.CurrentExposure / CameraExposure.IssD4.Scale * 100:G3} % of ISS night");

            GUILayout.BeginHorizontal();
            GUILayout.Label("F10.7 (sfu)", GUILayout.Width(90));
            f107Text = GUILayout.TextField(f107Text, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(40)) &&
                double.TryParse(f107Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                o.F107 = Math.Max(0, f); MarkDirty();
            }
            if (o.F107 < 65 || o.F107 > 230) GUILayout.Label("outside data range 65-230");
            GUILayout.EndHorizontal();

            GUILayout.Label("Local camera, behind-atmosphere part (before clouds): " + o.LocalEvent);
            if (GUILayout.Button("Next local event")) { o.LocalEvent = Next(o.LocalEvent); MarkDirty(); }
            GUILayout.Label("Local camera, in-front part (after clouds): " + o.FrontEvent);
            if (GUILayout.Button("Next front event")) { o.FrontEvent = Next(o.FrontEvent); MarkDirty(); }
            GUILayout.Label("Composite event (scaled camera): " + o.ScaledEvent);
            if (GUILayout.Button("Next scaled event")) { o.ScaledEvent = Next(o.ScaledEvent); MarkDirty(); }

            GUILayout.Space(6);
            DrawReadout();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static CameraEvent Next(CameraEvent e)
        {
            int i = Array.IndexOf(Events, e);
            return Events[(i + 1) % Events.Length];
        }

        /// <summary>Screen-centre readout from the double-precision CPU integrator (same model as
        /// the shader): apparent column per group in rayleighs and the resulting luminance.</summary>
        private void DrawReadout()
        {
            var cam = Camera.allCameras.FirstOrDefault(x => x.name == (AirglowRenderer.LocalViewWanted ? "Camera 00" : "Camera ScaledSpace"));
            if (cam == null) { GUILayout.Label("no camera"); return; }
            bool scaled = cam.name == "Camera ScaledSpace";
            AirglowRenderer.ViewOrigin(addon.Body, cam, scaled, out var p, out _);
            var dir = cam.transform.forward;
            var res = AirglowIntegrator.Integrate(new Vec3(p.x, p.y, p.z), new Vec3(dir.x, dir.y, dir.z),
                addon.Body.Radius / 1000.0, addon.Model.Layers, addon.Options.Extinction ? addon.Model.Lower : null,
                addon.Options.F107, addon.Model.F107Reference);
            double alt = Math.Sqrt(p.x * p.x + p.y * p.y + p.z * p.z) - addon.Body.Radius / 1000.0;
            GUILayout.Label($"Screen centre, camera altitude {alt:F1} km (no outshining, no display scaling):");
            double lum = 0;
            foreach (var g in DisplayControls.Groups)
            {
                double col = 0, y = 0;
                for (int i = 0; i < res.Length; i++)
                {
                    var L = addon.Model.Layers[i];
                    if (L.Group != g) continue;
                    col += res[i].ColumnR;
                    y += res[i].CameraRGB[0] * 0.2126729 + res[i].CameraRGB[1] * 0.7151522 + res[i].CameraRGB[2] * 0.0721750;
                }
                lum += y;
                GUILayout.Label($"  {g,-9} {col,12:G4} R   {y:G3} cd/m²");
            }
            GUILayout.Label($"  total luminance {lum:G3} cd/m² → {lum * addon.CurrentExposure:G3} of saturation at 100 % master");
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (locked) InputLockManager.RemoveControlLock(LockId);
        }
    }
}
