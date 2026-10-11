using System.Linq;
using RealAirglow.Core;
using UnityEngine;

namespace RealAirglow
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class RealAirglowFlight : RealAirglowAddon { }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public sealed class RealAirglowTrackingStation : RealAirglowAddon { }

    /// <summary>Scene-lifetime owner of the model, the display controls, the renderer and the UI.</summary>
    public abstract class RealAirglowAddon : MonoBehaviour
    {
        internal static RealAirglowAddon Instance { get; private set; }

        internal DisplayControls Controls { get; private set; }
        internal RuntimeOptions Options { get; private set; }
        internal BodyAirglow Model { get; private set; }
        internal CelestialBody Body { get; private set; }
        internal ExposureMeter Meter { get; } = new ExposureMeter();
        internal double CurrentExposure;

        private AirglowRenderer airglowRenderer;
        private SettingsWindow window;

        private void Start()
        {
            Instance = this;
            Options = new RuntimeOptions();
            var saved = SettingsStore.Load();
            Options.Load(saved);

            var node = GameDatabase.Instance.GetConfigNodes("AIRGLOW")
                .FirstOrDefault(n => n.GetValue("body") == Options.Body);
            if (node == null)
            {
                Debug.LogError("[RealAirglow] no AIRGLOW node for body " + Options.Body + "; disabled");
                Destroy(this);
                return;
            }
            Model = ConfigReader.Read(new KspConfigView(node));
            Body = FlightGlobals.GetBodyByName(Model.Body);
            if (Body == null)
            {
                Debug.LogError("[RealAirglow] body " + Model.Body + " not found; disabled");
                Destroy(this);
                return;
            }

            Controls = new DisplayControls();
            if (Model.DefaultMaster.HasValue) Controls.SetBodyDefaultMaster(Model.DefaultMaster.Value);
            Controls.Load(saved);

            airglowRenderer = new AirglowRenderer(this);
            window = gameObject.AddComponent<SettingsWindow>();
            Debug.Log($"[RealAirglow] {Model.Layers.Count} layers for {Model.Body}, R = {Body.Radius / 1000.0:F1} km, " +
                      $"colour space {QualitySettings.activeColorSpace}");
        }

        private void Update()
        {
            if (Controls == null) return;
            Controls.Update(Time.unscaledDeltaTime);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (alt && System.Enum.TryParse(Options.HotkeyKey, true, out KeyCode key) && Input.GetKeyDown(key))
                window.ToggleSettings();
        }

        private void LateUpdate() => airglowRenderer?.Update();

        internal void Save() => SettingsStore.Save(Controls, Options);

        private void OnDestroy()
        {
            if (Controls != null) Save();
            airglowRenderer?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
