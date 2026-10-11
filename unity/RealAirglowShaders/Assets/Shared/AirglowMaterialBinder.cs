using System;
using System.Collections.Generic;
using RealAirglow.Core;
using UnityEngine;

namespace RealAirglow
{
    public enum SpectralMode { Camera = 0, Eye = 1, Science = 2 }

    /// <summary>Per-view inputs that change per camera and frame.</summary>
    public struct AirglowView
    {
        public Double3 CamPosKm;      // camera relative to the planet centre, km (double precision)
        public Vector3 CamForward;     // world space
        public Vector3[] FrustumRays;  // world-space corner rays: BL, TL, TR, BR
        public float WorldUnitKm;      // km per world unit for depth conversion
        public Vector3 SunDir;         // unit vector planet -> Sun
    }

    /// <summary>Minimal double vector. Named Double3 so it cannot be confused with KSP's
    /// global Vector3d; the file also compiles in the Unity harness project.</summary>
    public struct Double3
    {
        public double x, y, z;
        public Double3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    }

    /// <summary>
    /// Writes the shader uniforms from the physical model plus the display controls. Shared,
    /// as source, by the KSP plugin and the Unity comparison harness so both drive the shader
    /// identically.
    /// </summary>
    public static class AirglowMaterialBinder
    {
        public const int MaxLayers = 12;
        // Solar angular radius at 1 AU: IAU 2015 nominal solar radius 695 700 km / 1 au.
        public const float SunAngularRadius = (float)(695700.0 / 149597870.7);

        public static readonly Dictionary<string, Color> ScienceColours = new Dictionary<string, Color>
        {
            { "green", new Color(0.1f, 1f, 0.1f) }, { "red", new Color(1f, 0.1f, 0.1f) },
            { "OH", new Color(1f, 0.6f, 0f) }, { "Na", new Color(1f, 1f, 0f) },
            { "continuum", new Color(0.3f, 0.5f, 1f) }, { "O2", new Color(0.8f, 0.2f, 1f) },
        };

        private static readonly Vector4[] shape = new Vector4[MaxLayers], colour = new Vector4[MaxLayers],
            sigR = new Vector4[MaxLayers], sigO = new Vector4[MaxLayers], aer = new Vector4[MaxLayers];

        private const double Luma0 = 0.2126729, Luma1 = 0.7151522, Luma2 = 0.0721750;

        /// <summary>Layer arrays, lower atmosphere, mode keywords and exposure.</summary>
        /// <param name="displayScale">group -> display multiplier (master x layer), from DisplayControls.Effective.</param>
        /// <param name="exposure">luminance (cd m^-2) -> fraction of saturation.</param>
        public static void BindModel(Material m, BodyAirglow body, Func<string, double> displayScale, double f107,
            SpectralMode mode, bool extinction, bool nightGate, double exposure, bool gammaSpace)
        {
            int n = Math.Min(body.Layers.Count, MaxLayers);
            for (int i = 0; i < MaxLayers; i++)
            {
                if (i >= n) { shape[i] = Vector4.zero; continue; }
                var L = body.Layers[i];
                double norm = 0.5 * Math.Sqrt(Math.PI / Math.Log(2.0)) * (L.HwhmLowKm + L.HwhmHighKm);
                double scale = L.Enabled ? L.IntensityAt(f107, body.F107Reference) * displayScale(L.Group) / norm : 0.0;
                shape[i] = new Vector4((float)L.PeakKm, (float)L.HwhmLowKm, (float)L.HwhmHighKm, (float)scale);
                switch (mode)
                {
                    case SpectralMode.Eye:
                        colour[i] = new Vector4((float)L.CieRGB[0], (float)L.CieRGB[1], (float)L.CieRGB[2], (float)L.ScotopicY);
                        break;
                    case SpectralMode.Science:
                        double y = L.CameraRGB[0] * Luma0 + L.CameraRGB[1] * Luma1 + L.CameraRGB[2] * Luma2;
                        ScienceColours.TryGetValue(L.Group, out var c);
                        colour[i] = new Vector4((float)(c.r * y), (float)(c.g * y), (float)(c.b * y), 0f);
                        break;
                    default:
                        colour[i] = new Vector4((float)L.CameraRGB[0], (float)L.CameraRGB[1], (float)L.CameraRGB[2], 0f);
                        break;
                }
                sigR[i] = V(L.SigmaRayleigh); sigO[i] = V(L.SigmaOzone); aer[i] = V(L.AerosolFactor);
            }
            m.SetInt("_LayerCount", n);
            m.SetVectorArray("_LayerShape", shape);
            m.SetVectorArray("_LayerColour", colour);
            m.SetVectorArray("_LayerSigmaR", sigR);
            m.SetVectorArray("_LayerSigmaO3", sigO);
            m.SetVectorArray("_LayerAerosol", aer);

            var p = body.Lower.AirLogDensityPoly;
            double C(int k) => k < p.Length ? p[k] : 0.0;
            m.SetVector("_AirPoly0", new Vector4((float)C(0), (float)C(1), (float)C(2), (float)C(3)));
            m.SetVector("_AirPoly1", new Vector4((float)C(4), (float)C(5), (float)body.Lower.TopKm, 0f));
            m.SetVector("_Ozone", new Vector4((float)(body.Lower.OzoneColumnDU * LowerAtmosphereDef.DobsonUnit),
                (float)body.Lower.OzonePeakKm, (float)(body.Lower.OzoneFwhmKm / 2.354820045), 0f));
            m.SetVector("_Aerosol", new Vector4((float)body.Lower.AerosolTau550, (float)body.Lower.AerosolScaleHeightKm, 0f, 0f));
            m.SetFloat("_Exposure", (float)exposure);
            m.SetFloat("_GammaSpace", gammaSpace ? 1f : 0f);
            m.SetFloat("_SunAngularRadius", SunAngularRadius);

            SetKeyword(m, "MODE_CAMERA", mode == SpectralMode.Camera);
            SetKeyword(m, "MODE_EYE", mode == SpectralMode.Eye);
            SetKeyword(m, "MODE_SCIENCE", mode == SpectralMode.Science);
            SetKeyword(m, "EXTINCTION_ON", extinction);
            SetKeyword(m, "NIGHT_GATE_ON", nightGate);
        }

        public static void BindView(Material m, AirglowView v, double radiusKm, bool useDepth)
        {
            m.SetVector("_CamPosKm", new Vector4((float)v.CamPosKm.x, (float)v.CamPosKm.y, (float)v.CamPosKm.z, 0f));
            m.SetVector("_CamForward", v.CamForward);
            m.SetVectorArray("_FrustumRays", new Vector4[] { v.FrustumRays[0], v.FrustumRays[1], v.FrustumRays[2], v.FrustumRays[3] });
            m.SetFloat("_WorldUnitKm", v.WorldUnitKm);
            m.SetFloat("_RadiusKm", (float)radiusKm);
            m.SetVector("_SunDir", v.SunDir);
            SetKeyword(m, "USE_DEPTH", useDepth);
        }

        /// <summary>World-space frustum corner rays of a camera (BL, TL, TR, BR).</summary>
        public static Vector3[] FrustumRays(Camera cam)
        {
            var corners = new Vector3[4];
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), 1f, Camera.MonoOrStereoscopicEye.Mono, corners);
            for (int i = 0; i < 4; i++) corners[i] = cam.transform.TransformVector(corners[i]);
            return corners;
        }

        /// <summary>A full-screen quad in normalised device coordinates.</summary>
        public static Mesh FullScreenQuad()
        {
            var mesh = new Mesh { name = "RealAirglow quad" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e9f);
            return mesh;
        }

        private static Vector4 V(double[] a) => new Vector4((float)a[0], (float)a[1], (float)a[2], 0f);

        private static void SetKeyword(Material m, string k, bool on)
        {
            if (on) m.EnableKeyword(k); else m.DisableKeyword(k);
        }
    }
}
