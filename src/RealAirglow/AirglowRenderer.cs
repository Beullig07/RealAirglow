using System;
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
    /// camera. In flight with the map closed the airglow is drawn on the earliest local camera,
    /// which renders after the scaled camera and also draws EVE's local cloud volumes; its depth
    /// lets terrain and vessels occlude the airglow. In map view and the Tracking Station it is
    /// drawn on the scaled-space camera. Only one camera draws per frame (local cameras clear
    /// depth only, so drawing on both would double the sky).
    ///
    /// On the local camera the airglow is split in two (docs/ARCHITECTURE.md): emission that
    /// reaches the camera without crossing the lower atmosphere is drawn after the clouds (it is
    /// in front of them, e.g. seen from orbit); emission behind the lower atmosphere is drawn
    /// before the clouds so they can hide it (e.g. seen from the ground).
    /// </summary>
    internal sealed class AirglowRenderer
    {
        private static Shader shader;
        private readonly RealAirglowAddon addon;
        private CameraHook scaledHook, localHook;
        private float nextCameraScan;

        internal AirglowRenderer(RealAirglowAddon addon)
        {
            this.addon = addon;
            if (shader == null) shader = LoadShader();
        }

        /// <summary>Shader bundle for the running OS (built by unity/.../BuildBundles.cs): Windows
        /// has DirectX 11 + OpenGL Core, macOS Metal + OpenGL Core, Linux OpenGL Core.</summary>
        internal static string BundleFile(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXPlayer: return "realairglow-macos.bundle";
                case RuntimePlatform.LinuxPlayer: return "realairglow-linux.bundle";
                default: return "realairglow-windows.bundle";
            }
        }

        private static Shader LoadShader()
        {
            string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "RealAirglow", "Shaders", BundleFile(Application.platform));
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) { Debug.LogError("[RealAirglow] cannot load " + path); return null; }
            var s = bundle.LoadAllAssets<Shader>().FirstOrDefault(x => x.name == "RealAirglow/Airglow");
            bundle.Unload(false);
            if (s == null) { Debug.LogError("[RealAirglow] shader RealAirglow/Airglow missing from " + path); return null; }
            if (!s.isSupported)
            {
                Debug.LogError("[RealAirglow] shader not supported on " + SystemInfo.graphicsDeviceType + " (" +
                               SystemInfo.graphicsDeviceVersion + "); airglow disabled");
                return null;
            }
            Debug.Log("[RealAirglow] shader loaded from " + Path.GetFileName(path) + " for " + SystemInfo.graphicsDeviceType);
            return s;
        }

        internal static bool LocalViewWanted => HighLogic.LoadedSceneIsFlight && !MapView.MapIsEnabled;

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
            bool localMode = LocalViewWanted && localHook != null;
            if (scaledHook != null) scaledHook.Active = !localMode;
            if (localHook != null) localHook.Active = localMode;
        }

        private CameraHook Rehook(CameraHook hook, Camera cam, bool scaled)
        {
            if (hook != null && hook.Camera == cam && cam != null) return hook;
            if (hook != null) UnityEngine.Object.Destroy(hook);
            if (cam == null) return null;
            var h = cam.gameObject.GetComponent<CameraHook>() ?? cam.gameObject.AddComponent<CameraHook>();
            h.Init(addon, cam, new Material(shader), scaled);
            return h;
        }

        internal void Dispose()
        {
            if (scaledHook != null) UnityEngine.Object.Destroy(scaledHook);
            if (localHook != null) UnityEngine.Object.Destroy(localHook);
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

    /// <summary>Metered exposure state, smoothed so the display does not flicker as the view
    /// turns (time constant: a display tuning parameter, not physics).</summary>
    internal sealed class ExposureMeter
    {
        private const double TimeConstant = 0.5;   // s
        private double logLum = double.NaN;
        internal double AverageLuminance => double.IsNaN(logLum) ? 0.0 : Math.Exp(logLum);

        /// <summary>Average scene luminance over a 16 x 9 grid of the camera's view rays.</summary>
        internal void Measure(Double3 camKm, Vector3[] frustum, double radiusKm, Vector3 sunDir, double atmosphereTopKm, double dt)
        {
            const int nx = 16, ny = 9;
            var cam = new Vec3(camKm.x, camKm.y, camKm.z);
            var sun = new Vec3(sunDir.x, sunDir.y, sunDir.z);
            double sum = 0;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    float u = (i + 0.5f) / nx, v = (j + 0.5f) / ny;
                    var r = Vector3.Lerp(Vector3.Lerp(frustum[0], frustum[3], u), Vector3.Lerp(frustum[1], frustum[2], u), v).normalized;
                    sum += AutoExposure.RayLuminance(cam, new Vec3(r.x, r.y, r.z), radiusKm, sun, atmosphereTopKm);
                }
            // Log of the average, floored far below any metered level so log() stays finite.
            double l = Math.Log(Math.Max(sum / (nx * ny), 1e-9));
            if (double.IsNaN(logLum)) logLum = l;
            else logLum += (l - logLum) * (1 - Math.Exp(-dt / TimeConstant));
        }
    }

    /// <summary>Per-camera command buffers that draw the full-screen airglow pass(es).</summary>
    internal sealed class CameraHook : MonoBehaviour
    {
        internal Camera Camera { get; private set; }
        internal bool Active;

        private RealAirglowAddon addon;
        private Material material;
        private bool scaled;
        private Mesh quad;
        private CommandBuffer mainBuffer, frontBuffer;
        private CameraEvent mainEvent, frontEvent;
        private bool mainRegistered, frontRegistered;
        private MaterialPropertyBlock allPass, backPass, frontPass;
        private readonly Vector3[] frustum = new Vector3[4];
        private Func<string, double> displayScale;   // cached: a method group allocates a delegate per use

        internal void Init(RealAirglowAddon owner, Camera cam, Material mat, bool isScaled)
        {
            addon = owner; Camera = cam; scaled = isScaled;
            displayScale = owner.Controls.Effective;
            if (material != null) Destroy(material);
            material = mat;
            if (quad == null) quad = AirglowMaterialBinder.FullScreenQuad();
            if (mainBuffer == null) mainBuffer = new CommandBuffer { name = "RealAirglow" };
            if (frontBuffer == null) frontBuffer = new CommandBuffer { name = "RealAirglow (in front of clouds)" };
            allPass = Pass(0f); backPass = Pass(2f); frontPass = Pass(1f);
        }

        private static MaterialPropertyBlock Pass(float mode)
        {
            var b = new MaterialPropertyBlock();
            b.SetFloat("_PassMode", mode);
            return b;
        }

        private void Register(CommandBuffer buffer, ref bool registered, ref CameraEvent current, CameraEvent wanted, bool want)
        {
            if (registered && (!want || current != wanted)) { Camera.RemoveCommandBuffer(current, buffer); registered = false; }
            if (want && !registered) { current = wanted; Camera.AddCommandBuffer(current, buffer); registered = true; }
        }

        private void OnPreRender()
        {
            if (addon == null || Camera == null) return;
            var o = addon.Options;
            Register(mainBuffer, ref mainRegistered, ref mainEvent, scaled ? o.ScaledEvent : o.LocalEvent, true);
            Register(frontBuffer, ref frontRegistered, ref frontEvent, o.FrontEvent, !scaled);
            mainBuffer.Clear();
            frontBuffer.Clear();
            if (!Active || !addon.Controls.Enabled) return;

            Camera.depthTextureMode |= DepthTextureMode.Depth;
            AirglowRenderer.ViewOrigin(addon.Body, Camera, scaled, out var posKm, out var unitKm);
            AirglowMaterialBinder.FrustumRays(Camera, frustum);
            var sunDir = AirglowRenderer.SunDirection(addon.Body);
            double radiusKm = addon.Body.Radius / 1000.0;

            addon.Meter.Measure(posKm, frustum, radiusKm, sunDir, addon.Model.Lower.TopKm, Time.unscaledDeltaTime);
            double exposure = o.Exposure == ExposureMode.Auto
                ? AutoExposure.Scale(addon.Meter.AverageLuminance, CameraExposure.IssD4)
                : CameraExposure.IssD4.Scale;
            addon.CurrentExposure = exposure;

            AirglowMaterialBinder.BindModel(material, addon.Model, displayScale, o.F107, o.Mode,
                o.Extinction, o.Outshine, exposure, QualitySettings.activeColorSpace == ColorSpace.Gamma);
            var view = new AirglowView
            {
                CamPosKm = posKm,
                CamForward = Camera.transform.forward,
                FrustumRays = frustum,
                WorldUnitKm = unitKm,
                SunDir = sunDir,
            };
            AirglowMaterialBinder.BindView(material, view, radiusKm, useDepth: true);
            if (scaled)
                mainBuffer.DrawMesh(quad, Matrix4x4.identity, material, 0, 0, allPass);
            else
            {
                mainBuffer.DrawMesh(quad, Matrix4x4.identity, material, 0, 0, backPass);
                frontBuffer.DrawMesh(quad, Matrix4x4.identity, material, 0, 0, frontPass);
            }
        }

        private void OnDestroy()
        {
            if (Camera != null)
            {
                if (mainRegistered) Camera.RemoveCommandBuffer(mainEvent, mainBuffer);
                if (frontRegistered) Camera.RemoveCommandBuffer(frontEvent, frontBuffer);
            }
            mainBuffer?.Release();
            frontBuffer?.Release();
            if (material != null) Destroy(material);
            if (quad != null) Destroy(quad);
        }
    }
}
