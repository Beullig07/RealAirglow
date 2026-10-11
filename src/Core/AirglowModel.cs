using System;
using System.Collections.Generic;

namespace RealAirglow.Core
{
    /// <summary>Minimal double-precision vector (planet-centred, kilometres).</summary>
    public readonly struct Vec3
    {
        public readonly double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public double Length => Math.Sqrt(Dot(this, this));
        public Vec3 Normalized => this * (1.0 / Length);
    }

    /// <summary>One emission component, as exported to Config/Earth.cfg by the reference model.</summary>
    public sealed class LayerDef
    {
        public string Name = "";
        public string Group = "";
        public bool Enabled = true;
        public double PeakKm, HwhmLowKm, HwhmHighKm;
        public double IntensityR;            // zenith column at f107Reference, rayleighs
        public double SolarCoefficient;      // relative change per 100 sfu
        public double[] CameraRGB = new double[3], CieRGB = new double[3];
        public double ScotopicY;
        // Extinction bins (BodyAirglow.ExtinctionBinsNm): per bin camera RGB, CIE RGB, scotopic Y,
        // and (sigma Rayleigh m^2, sigma O3 m^2, aerosol spectral factor).
        public double[] BinCameraRGB = new double[0], BinCieRGB = new double[0], BinScotopicY = new double[0];
        public double[] BinExtinction = new double[0];
        public int BinCount => BinScotopicY.Length;

        public const double SupportHwhm = 4.5;   // profile support, as in profiles.AsymGaussian
        public double BandLowKm => Math.Max(0.0, PeakKm - SupportHwhm * HwhmLowKm);
        public double BandHighKm => PeakKm + SupportHwhm * HwhmHighKm;

        private static readonly double Ln2 = Math.Log(2.0);

        /// <summary>Unit-column profile, km^-1: integrates to 1 over altitude.</summary>
        public double Profile(double zKm)
        {
            double h = zKm < PeakKm ? HwhmLowKm : HwhmHighKm;
            double u = (zKm - PeakKm) / h;
            double norm = 0.5 * Math.Sqrt(Math.PI / Ln2) * (HwhmLowKm + HwhmHighKm);
            return Math.Exp(-Ln2 * u * u) / norm;
        }

        public double IntensityAt(double f107, double f107Ref) =>
            IntensityR * (1.0 + SolarCoefficient * (f107 - f107Ref) / 100.0);
    }

    public sealed class LowerAtmosphereDef
    {
        public double[] AirLogDensityPoly = new double[0]; // ln(n / m^-3) = sum c_k (z/top)^k
        public double OzoneColumnDU = 300, OzonePeakKm = 22, OzoneFwhmKm = 15;
        public double AerosolTau550 = 0.05, AerosolScaleHeightKm = 1.2, TopKm = 60;
        public const double DobsonUnit = 2.686763e20; // molecules m^-2

        public double AirDensity(double zKm)
        {
            double x = zKm / TopKm, acc = 0, p = 1;
            foreach (var c in AirLogDensityPoly) { acc += c * p; p *= x; }
            return Math.Exp(acc);
        }

        public double OzoneDensity(double zKm)
        {
            double sig = OzoneFwhmKm / 2.354820045;
            double col = OzoneColumnDU * DobsonUnit;
            double u = (zKm - OzonePeakKm) / sig;
            return col / (sig * 1e3 * Math.Sqrt(2 * Math.PI)) * Math.Exp(-0.5 * u * u);
        }

        public double AerosolExtinction550(double zKm)
        {
            double H = AerosolScaleHeightKm * 1e3;
            return AerosolTau550 / H * Math.Exp(-Math.Max(zKm, 0.0) * 1e3 / H);
        }
    }

    /// <summary>
    /// CPU version of the shader's ray integration (double precision), mirroring
    /// reference/airglow_ref/geometry.py: each layer's altitude band is crossed in at most two
    /// path intervals, integrated with Gauss-Legendre quadrature.
    /// </summary>
    public static class AirglowIntegrator
    {
        public struct LayerResult
        {
            public double ColumnR;            // apparent column, rayleighs
            public double[] CameraRGB;        // transmitted camera-mode linear sRGB, cd m^-2
        }

        private static readonly double[] GlX, GlW;

        static AirglowIntegrator()
        {
            GaussLegendre(64, out GlX, out GlW);
        }

        /// <summary>Gauss-Legendre nodes and weights on [-1, 1] (Newton iteration).</summary>
        public static void GaussLegendre(int n, out double[] x, out double[] w)
        {
            x = new double[n]; w = new double[n];
            for (int i = 0; i < n; i++)
            {
                double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), pp = 0;
                for (int it = 0; it < 100; it++)
                {
                    double p1 = 1, p2 = 0;
                    for (int j = 1; j <= n; j++) { double p3 = p2; p2 = p1; p1 = ((2 * j - 1) * z * p2 - (j - 1) * p3) / j; }
                    pp = n * (z * p1 - p2) / (z * z - 1);
                    double dz = p1 / pp; z -= dz;
                    if (Math.Abs(dz) < 1e-15) break;
                }
                x[i] = -z; w[i] = 2 / ((1 - z * z) * pp * pp);
            }
        }

        /// <summary>Entry/exit distances of the ray with a sphere, computed about the point of
        /// closest approach for precision at grazing incidence. False if missed.</summary>
        public static bool Sphere(Vec3 o, Vec3 d, double r, out double t0, out double t1)
        {
            double b = Vec3.Dot(o, d);
            Vec3 p = o - d * b;                 // closest-approach vector
            double h = p.Length;
            t0 = t1 = 0;
            if (h >= r) return false;
            double dt = Math.Sqrt((r - h) * (r + h));
            t0 = -b - dt; t1 = -b + dt;
            return true;
        }

        /// <summary>Path intervals with rLo <= |o + t d| <= rHi and 0 <= t <= tEnd.</summary>
        public static int BandIntervals(Vec3 o, Vec3 d, double rLo, double rHi, double tEnd, double[] a, double[] b)
        {
            int n = 0;
            if (!Sphere(o, d, rHi, out double o0, out double o1)) return 0;
            bool inner = Sphere(o, d, rLo, out double i0, out double i1);
            void Add(double s, double e)
            {
                s = Math.Max(s, 0.0); e = Math.Min(e, tEnd);
                if (e > s) { a[n] = s; b[n] = e; n++; }
            }
            if (inner) { Add(o0, i0); Add(i1, o1); } else Add(o0, o1);
            return n;
        }

        public static double PlanetHit(Vec3 o, Vec3 d, double radiusKm)
        {
            if (Sphere(o, d, radiusKm, out double t0, out double t1) && t0 > 0) return t0;
            return double.PositiveInfinity;
        }

        /// <summary>Integrate all layers along one ray. originKm is planet-centred, dir unit.
        /// lower may be null (no extinction). tLimitKm clips the ray (e.g. depth buffer).</summary>
        public static LayerResult[] Integrate(Vec3 originKm, Vec3 dir, double radiusKm, IList<LayerDef> layers,
            LowerAtmosphereDef lower, double f107, double f107Ref, double tLimitKm = double.PositiveInfinity)
        {
            double tEnd = Math.Min(PlanetHit(originKm, dir, radiusKm), tLimitKm);
            var res = new LayerResult[layers.Count];
            var ea = new double[2]; var eb = new double[2];
            var aa = new double[2]; var ab = new double[2];
            var absAir = new double[2]; var absO3 = new double[2]; var absAer = new double[2];
            int nAbs = 0;
            if (lower != null)
            {
                nAbs = BandIntervals(originKm, dir, radiusKm - 1e-3, radiusKm + lower.TopKm, tEnd, aa, ab);
                for (int j = 0; j < nAbs; j++)
                {
                    double half = 0.5 * (ab[j] - aa[j]), mid = 0.5 * (ab[j] + aa[j]);
                    for (int k = 0; k < GlX.Length; k++)
                    {
                        double t = mid + half * GlX[k];
                        double z = (originKm + dir * t).Length - radiusKm;
                        double w = half * GlW[k] * 1e3;  // km -> m
                        absAir[j] += lower.AirDensity(z) * w;
                        absO3[j] += lower.OzoneDensity(z) * w;
                        absAer[j] += lower.AerosolExtinction550(z) * w;
                    }
                }
            }
            for (int i = 0; i < layers.Count; i++)
            {
                var L = layers[i];
                res[i].CameraRGB = new double[3];
                if (!L.Enabled) continue;
                int n = BandIntervals(originKm, dir, radiusKm + L.BandLowKm, radiusKm + L.BandHighKm, tEnd, ea, eb);
                double scale = L.IntensityAt(f107, f107Ref);
                for (int k = 0; k < n; k++)
                {
                    double half = 0.5 * (eb[k] - ea[k]), mid = 0.5 * (eb[k] + ea[k]), col = 0;
                    for (int q = 0; q < GlX.Length; q++)
                    {
                        double t = mid + half * GlX[q];
                        double z = (originKm + dir * t).Length - radiusKm;
                        col += L.Profile(z) * half * GlW[q];
                    }
                    col *= scale;
                    res[i].ColumnR += col;
                    double nAir = 0, nO3 = 0, aer = 0;
                    for (int j = 0; j < nAbs; j++)
                        if (ab[j] <= ea[k] + 1e-3) { nAir += absAir[j]; nO3 += absO3[j]; aer += absAer[j]; }
                    for (int k2 = 0; k2 < L.BinCount; k2++)
                    {
                        double T = Math.Exp(-L.BinExtinction[3 * k2] * nAir - L.BinExtinction[3 * k2 + 1] * nO3 - L.BinExtinction[3 * k2 + 2] * aer);
                        for (int c = 0; c < 3; c++) res[i].CameraRGB[c] += col * T * L.BinCameraRGB[3 * k2 + c];
                    }
                }
            }
            return res;
        }
    }

    /// <summary>Photographic exposure (docs/PHYSICS.md §12.3): H = q L t / N^2 with q = 0.65,
    /// saturation-based ISO speed S = 78 lx s / H_sat. Scale converts luminance (cd m^-2) to
    /// the fraction of sensor saturation.</summary>
    public readonly struct CameraExposure
    {
        public const double Q = 0.65;
        public readonly double Iso, FNumber, Seconds;
        public CameraExposure(double iso, double fNumber, double seconds) { Iso = iso; FNumber = fNumber; Seconds = seconds; }
        public double Scale => Q * Seconds / (FNumber * FNumber) * Iso / 78.0;

        /// <summary>"Realistic photographic" reference: ISS047-E-1114 EXIF, Nikon D4.</summary>
        public static readonly CameraExposure IssD4 = new CameraExposure(12800, 1.4, 1.3);
    }
}
