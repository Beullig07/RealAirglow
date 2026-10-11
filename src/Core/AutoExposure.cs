using System;

namespace RealAirglow.Core
{
    /// <summary>
    /// Adaptive (metered) exposure for the airglow display (docs/PHYSICS.md §5, agreed with the
    /// owner 2026-10-11). The emission is never cut at the terminator; instead the display
    /// exposure follows what an averaging camera meter would choose for the scene, so airglow is
    /// visible when the view is dark and disappears when sunlit ground or twilight sky is in frame.
    ///
    /// Scene luminance per view ray:
    ///  * ray hits the planet: a Lambertian surface, L = A E_v max(mu_sun, 0) / pi;
    ///  * ray misses the planet with the camera inside the atmosphere: the twilight sky brightness
    ///    of Patat et al. (2006), or the sunlit-surface value in daylight;
    ///  * otherwise (space): 0. Stars, Moon and city lights are not included.
    /// Exposure: a reflected-light meter sets N^2 / (t S) = L_avg / K (ISO 2720, K = 12.5), so the
    /// display scale is q K / (78 L_avg) with q and 78 from ISO 12232 (CameraExposure). It is
    /// capped at the documented ISS night exposure (ISS047-E-1114), the most a night camera used.
    /// </summary>
    public static class AutoExposure
    {
        public const double Albedo = 0.29;              // Earth's albedo, ERBE/CERES [Stephens2015]
        public const double SolarIlluminance = 134634;  // lx at 1 au: TSIS-1 HSRS x CIE V(lambda) x 683 (D)
        public const double MeterK = 12.5;              // reflected-light meter constant, cd s m^-2 (ISO 2720)
        public const double MagZeroLuminance = 1.08e5;  // L = 1.08e5 x 10^(-0.4 m_V) cd m^-2 [Bara2020]
        // [Patat2006] Table 1 fit: m = M0 + M1 (z - ZMin) + M2 (z - ZMin)^2 mag arcsec^-2, ZMin..ZMax deg.
        public const double TwilightM0 = 11.84, TwilightM1 = 1.518, TwilightM2 = -0.057;
        public const double TwilightZMin = 95.0, TwilightZMax = 105.0;

        /// <summary>V-band zenith twilight sky luminance (cd m^-2) for a Sun zenith distance in
        /// degrees. Fit of [Patat2006] Table 1 (Paranal), m = 11.84 + 1.518 (z-95) - 0.057 (z-95)^2
        /// mag arcsec^-2, valid 95..105 deg. Outside the fit the edge value is held (brighter than
        /// z = 95 deg, airglow is invisible at any of these levels; beyond 105 deg the sky is at its
        /// night level).</summary>
        public static double TwilightSkyLuminance(double sunZenithDeg)
        {
            double z = Math.Max(TwilightZMin, Math.Min(TwilightZMax, sunZenithDeg)) - TwilightZMin;
            double m = TwilightM0 + TwilightM1 * z + TwilightM2 * z * z;
            return MagZeroLuminance * Math.Pow(10.0, -0.4 * m);
        }

        /// <summary>Sky (or sunlit-ground) luminance at a place where the Sun's zenith distance is
        /// z degrees: the twilight sky for z >= 95, otherwise the larger of the 95 deg sky and the
        /// sunlit Lambertian surface.</summary>
        public static double BackgroundLuminance(double sunZenithDeg)
        {
            double sky = TwilightSkyLuminance(sunZenithDeg);
            if (sunZenithDeg >= TwilightZMin) return sky;
            return Math.Max(sky, SunlitSurfaceLuminance(Math.Cos(sunZenithDeg * Math.PI / 180.0)));
        }

        /// <summary>Local outshining (owner request, 1.0.3): the factor by which sunlight at an
        /// emitting point hides its airglow. It is the metered exposure applied locally: the
        /// night preset's exposure, reduced where the background at that place (twilight sky or
        /// sunlit ground, BackgroundLuminance) is brighter than the luminance that preset meters.
        /// 1 on the night side (z above about 102 deg), falling smoothly through twilight, and
        /// below 1e-3 in daylight.</summary>
        public static double OutshineWeight(double sunZenithDeg, CameraExposure nightPreset) =>
            Math.Min(1.0, MeteredLuminance(nightPreset) / BackgroundLuminance(sunZenithDeg));

        public static double SunlitSurfaceLuminance(double cosSunZenith) =>
            Albedo * SolarIlluminance * Math.Max(cosSunZenith, 0.0) / Math.PI;

        /// <summary>Approximate scene luminance (cd m^-2) along one view ray.</summary>
        public static double RayLuminance(Vec3 camKm, Vec3 dir, double radiusKm, Vec3 sunDir, double atmosphereTopKm)
        {
            if (AirglowIntegrator.Sphere(camKm, dir, radiusKm, out double t0, out _) && t0 > 0)
            {
                var p = (camKm + dir * t0).Normalized;
                return SunlitSurfaceLuminance(Vec3.Dot(p, sunDir));
            }
            double r = camKm.Length;
            if (r - radiusKm > atmosphereTopKm) return 0.0;
            double cosZ = Vec3.Dot(camKm * (1.0 / r), sunDir);
            return BackgroundLuminance(Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosZ))) * 180.0 / Math.PI);
        }

        /// <summary>Luminance an averaging meter would treat as "correctly exposed" at the given
        /// settings: K N^2 / (t S).</summary>
        public static double MeteredLuminance(CameraExposure e) => MeterK * e.FNumber * e.FNumber / (e.Seconds * e.Iso);

        /// <summary>Display scale (luminance -> fraction of saturation) for an average scene
        /// luminance, never brighter than the night preset.</summary>
        public static double Scale(double averageLuminance, CameraExposure nightPreset)
        {
            double floor = MeteredLuminance(nightPreset);
            return CameraExposure.Q * MeterK / (78.0 * Math.Max(averageLuminance, floor));
        }
    }
}
