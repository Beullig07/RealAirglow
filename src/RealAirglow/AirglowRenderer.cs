using System.IO;
using System.Linq;
using RealAirglow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealAirglow
{
    /// <summary>
    /// Chooses which camera draws the airglow and keeps a hook on it.
    ///
    /// Cameras (names as used by KSP and read by Scatterer's source): "Camera ScaledSpace";
    /// "Camera 00" (near) and, on OpenGL, "Camera 01" (far); D3D11 runs a single unified local
    /// camera. When the body's PQS is active and the map is closed, the airglow is drawn once
    /// on the earliest local camera so terrain and vessels occlude it through the depth buffer.
    /// Otherwise it is drawn on the scaled-space camera. Drawing on both would double the sky.
    /// </summary>
    internal sealed class AirglowRenderer
    {
        private static Shader shader;
        private readonly RealAirglowAddon addon;
        private CameraHook scaledHook, localHook;

        internal AirglowRenderer(RealAirglowAddon addon)
        {
            this.addon = addon;
            if (shader == null) shader = LoadShader();
        }

        private static Shader LoadShader()
        {
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "RealAirglow", "Shaders", "realairglow-windows.bundle");
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) { Debug.LogError("[RealAirglow] cannot load " + path); return null; }
            var s = bundle.LoadAllAssets<Shader>().FirstOrDefault(x => x.name == "RealAirglow/Airglow");
            if (s == null) Debug.LogError("[RealAirglow] shader RealAirglow/Airglow missing from bundle");
            bundle.Unload(false);
            return s;
        }

        internal bool LocalMode
        {
            get
            {
                var pqs = addon.Body.pqsController;
                return HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled && pqs != null && pqs.isActive;
            }
        }

        private float nextCameraScan;

        internal void Update()
        {
            if (shader == null) return;
            bool lost = scaledHook == null || scaledHook.Camera == null || (localHook != null && localHook.Camera == null);
            if (lost || Time.unscaledTime >= nextCameraScan)
            {
                nextCameraScan = Time.unscaledTime + 1f;   // Camera.allCameras allocates; scan at most once a second
                var cams = Camera.allCameras;
                var scaledCam = cams.FirstOrDefault(c => c.name == "Camera ScaledSpace");
                var far = cams.FirstOrDefault(c => c.name == "Camera 01");
                var localCam = far != null ? far : cams.FirstOrDefault(c => c.name == "Camera 00");
                scaledHook = Rehook(scaledHook, scaledCam, scaled: true);
                localHook = Rehook(localHook, localCam, scaled: false);
            }
            bool localMode = LocalMode && localHook != null;
            if (scaledHook != null) scaledHook.Active = !localMode;
            if (localHook != null) localHook.Active = localMode;
        }

        private CameraHook Rehook(CameraHook hook, Camera cam, bool scaled)
        {
            if (hook != null && hook.Camera == cam && cam != null) return hook;
            if (hook != null) Object.Destroy(hook);
            return Attach(cam, scaled);
        }

        private CameraHook Attach(Camera cam, bool scaled)
        {
            if (cam == null) return null;
            var hook = cam.gameObject.GetComponent<CameraHook>() ?? cam.gameObject.AddComponent<CameraHook>();
            hook.Init(addon, cam, new Material(shader), scaled);
            return hook;
        }

        internal void Dispose()
        {
            if (scaledHook != null) Object.Destroy(scaledHook);
            if (localHook != null) Object.Destroy(localHook);
        }

        /// <summary>Camera position relative to the body centre in km, and km per world unit.</summary>
        internal static void ViewOrigin(CelestialBody body, Camera cam, bool scaled, out Double3 posKm, out float worldUnitKm)
        {
            if (scaled)
            {
                Vector3d rel = (Vector3d)(cam.transform.position - body.scaledBody.transform.position) * ScaledSpace.ScaleFactor;
                posKm = new Double3(rel.x / 1000.0, rel.y / 1000.0, rel.z / 1000.0);
                worldUnitKm = ScaledSpace.ScaleFactor / 1000f;
            }
            else
            {
                Vector3d rel = (Vector3d)cam.transform.position - body.position;
                posKm = new Double3(rel.x / 1000.0, rel.y / 1000.0, rel.z / 1000.0);
                worldUnitKm = 0.001f;
            }
        }

        internal static Vector3 SunDirection(CelestialBody body)
        {
            var sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            if (sun == null || sun == body) return Vector3.up;
            return ((Vector3)(sun.position - body.position)).normalized;
        }
    }

    /// <summary>Per-camera command buffer that draws the full-screen airglow pass.</summary>
    internal sealed class CameraHook : MonoBehaviour
    {
        internal Camera Camera { get; private set; }
        internal bool Active;

        private RealAirglowAddon addon;
        private Material material;
        private bool scaled;
        private CommandBuffer buffer;
        private CameraEvent registeredEvent;
        private bool registered;
        private Mesh quad;

        internal void Init(RealAirglowAddon owner, Camera cam, Material mat, bool isScaled)
        {
            addon = owner; Camera = cam; scaled = isScaled;
            if (material != null) Destroy(material);
            material = mat;
            if (quad == null) quad = AirglowMaterialBinder.FullScreenQuad();
            if (buffer == null) buffer = new CommandBuffer { name = "RealAirglow" };
        }

        private CameraEvent WantedEvent => scaled ? addon.Options.ScaledEvent : addon.Options.LocalEvent;

        private void OnPreRender()
        {
            if (addon == null || Camera == null) return;
            if (!registered || registeredEvent != WantedEvent)
            {
                if (registered) Camera.RemoveCommandBuffer(registeredEvent, buffer);
                registeredEvent = WantedEvent;
                Camera.AddCommandBuffer(registeredEvent, buffer);
                registered = true;
            }
            buffer.Clear();
            if (!Active || !addon.Controls.Enabled) return;

            Camera.depthTextureMode |= DepthTextureMode.Depth;
            var o = addon.Options;
            AirglowMaterialBinder.BindModel(material, addon.Model, addon.Controls.Effective, o.F107, o.Mode,
                o.Extinction, o.NightGate, CameraExposure.IssD4.Scale,
                QualitySettings.activeColorSpace == ColorSpace.Gamma);
            AirglowRenderer.ViewOrigin(addon.Body, Camera, scaled, out var posKm, out var unitKm);
            var view = new AirglowView
            {
                CamPosKm = posKm,
                CamForward = Camera.transform.forward,
                FrustumRays = AirglowMaterialBinder.FrustumRays(Camera),
                WorldUnitKm = unitKm,
                SunDir = AirglowRenderer.SunDirection(addon.Body),
            };
            AirglowMaterialBinder.BindView(material, view, addon.Body.Radius / 1000.0, useDepth: true);
            buffer.DrawMesh(quad, Matrix4x4.identity, material, 0, 0);
        }

        private void OnDestroy()
        {
            if (registered && Camera != null) Camera.RemoveCommandBuffer(registeredEvent, buffer);
            buffer?.Release();
            if (material != null) Destroy(material);
            if (quad != null) Destroy(quad);
        }
    }
}
