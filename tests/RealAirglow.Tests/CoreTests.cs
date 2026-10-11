using System;
using System.IO;
using System.Linq;
using RealAirglow.Core;
using Xunit;

namespace RealAirglow.Tests
{
    public class DisplayControlsTests
    {
        [Fact]
        public void DefaultIsOnePointFivePercentWithRedAtFiveHundredPercent()
        {
            var c = new DisplayControls();
            Assert.Equal(0.015, c.MasterTarget, 12);
            Assert.Equal(-6.059, DisplayControls.ExposureValue(0.015), 3);
            foreach (var g in DisplayControls.Groups.Where(g => g != "red")) Assert.Equal(0.015, c.Effective(g), 12);
            Assert.Equal(5.0, c.GetGroupTarget("red"), 12);   // owner default from 1.0.2
            Assert.Equal(0.075, c.Effective("red"), 12);
        }

        [Fact]
        public void MultipliersAreLinearInMasterAndLayer()
        {
            var c = new DisplayControls();
            foreach (var m in new[] { 0.0, 0.15, 0.3, 1.0, 2.5 })
                foreach (var l in new[] { 0.0, 0.5, 1.0, 3.0 })
                {
                    c.SetMaster(m, immediate: true);
                    c.SetGroup("green", l, immediate: true);
                    Assert.Equal(m * l, c.Effective("green"), 12);
                }
        }

        [Fact]
        public void ZeroPercentGivesExactlyZero()
        {
            var c = new DisplayControls();
            c.SetMaster(0.0, immediate: true);
            foreach (var g in DisplayControls.Groups) Assert.Equal(0.0, c.Effective(g));
            c.SetMaster(1.0, immediate: true);
            c.SetGroup("red", 0.0, immediate: true);
            Assert.Equal(0.0, c.Effective("red"));
            c.Enabled = false;
            foreach (var g in DisplayControls.Groups) Assert.Equal(0.0, c.Effective(g));
        }

        [Fact]
        public void LayerSliderOnlyAffectsItsOwnGroup()
        {
            var c = new DisplayControls();
            c.SetMaster(1.0, immediate: true);
            foreach (var g in DisplayControls.Groups) c.SetGroup(g, 1.0, immediate: true);
            c.SetGroup("OH", 2.0, immediate: true);
            foreach (var g in DisplayControls.Groups.Where(g => g != "OH")) Assert.Equal(1.0, c.Effective(g), 12);
            Assert.Equal(2.0, c.Effective("OH"), 12);
        }

        [Fact]
        public void ValuesAreClampedToZeroToThreeHundredPercent()
        {
            var c = new DisplayControls();
            c.SetMaster(5.0, immediate: true);
            Assert.Equal(3.0, c.MasterTarget);
            c.SetMaster(-1.0, immediate: true);
            Assert.Equal(0.0, c.MasterTarget);
            c.SetGroup("red", 500.0, immediate: true);
            Assert.Equal(100.0, c.GetGroupTarget("red"));
            c.SetGroup("red", -1.0, immediate: true);
            Assert.Equal(0.0, c.GetGroupTarget("red"));
        }

        [Fact]
        public void SmoothingReachesTargetExactlyAfterTwoTenthsOfASecond()
        {
            var c = new DisplayControls();
            c.SetMaster(1.0, immediate: true);
            c.SetMaster(0.0);
            c.Update(0.1);
            Assert.Equal(0.5, c.Effective("green"), 9);
            c.Update(0.1);
            Assert.Equal(0.0, c.Effective("green"));
            c.Update(1.0);
            Assert.Equal(0.0, c.Effective("green"));
        }

        [Fact]
        public void PresetsAndReset()
        {
            var c = new DisplayControls();
            c.ApplyPreset(0); c.Update(1);
            foreach (var g in DisplayControls.Groups) Assert.Equal(1.0, c.Effective(g), 12);
            c.ApplyPreset(2); c.Update(1);
            foreach (var g in DisplayControls.Groups) Assert.Equal(0.15, c.Effective(g), 12);
            c.ApplyPreset(1); c.Update(1);
            Assert.Equal(0.015, c.Effective("green"), 12);
            Assert.Equal(0.075, c.Effective("red"), 12);
            c.SetGroup("Na", 3.0); c.SetGroup("red", 1.0); c.Enabled = false;
            c.ResetToDefaults(); c.Update(1);
            Assert.True(c.Enabled);
            Assert.Equal(0.015, c.Effective("Na"), 12);
            Assert.Equal(0.075, c.Effective("red"), 12);
        }

        [Fact]
        public void SaveLoadRoundTripAndUserOverridesBodyDefault()
        {
            var a = new DisplayControls();
            a.SetMaster(0.42, immediate: true);
            a.SetGroup("continuum", 0.7, immediate: true);
            a.Enabled = false;
            var b = new DisplayControls();
            b.SetBodyDefaultMaster(0.2);
            b.Load(a.Save());
            Assert.Equal(0.42, b.MasterTarget, 12);
            Assert.Equal(0.7, b.GetGroupTarget("continuum"), 12);
            Assert.False(b.Enabled);

            var fresh = new DisplayControls();
            fresh.SetBodyDefaultMaster(0.2);
            Assert.Equal(0.2, fresh.Effective("green"), 12);
        }

        [Fact]
        public void SettingsFromBeforeVersionThreeKeepTheNewRedDefault()
        {
            var old = new System.Collections.Generic.Dictionary<string, string>
            {
                ["master"] = "0.015", ["layer_red"] = "1", ["layer_green"] = "0.5",
            };
            var c = new DisplayControls();
            c.Load(old);
            Assert.Equal(DisplayControls.DefaultLayer("red"), c.GetGroupTarget("red"), 12);
            Assert.Equal(0.5, c.GetGroupTarget("green"), 12);

            old["version"] = "2";
            old["layer_red"] = "66.67";
            c.Load(old);
            Assert.Equal(5.0, c.GetGroupTarget("red"), 12);

            old["version"] = "3";
            old["layer_red"] = "1";
            c.Load(old);
            Assert.Equal(1.0, c.GetGroupTarget("red"), 12);
            Assert.Equal("3", new DisplayControls().Save()["version"]);
        }
    }

    public class OutshineTests
    {
        [Fact]
        public void FullAtNightFadesThroughTwilightHiddenInDaylight()
        {
            var e = CameraExposure.IssD4;
            Assert.Equal(1.0, AutoExposure.OutshineWeight(120, e));
            Assert.Equal(1.0, AutoExposure.OutshineWeight(102.5, e));
            double prev = 0;
            for (double z = 95; z <= 102; z += 0.1)
            {
                double w = AutoExposure.OutshineWeight(z, e);
                Assert.True(w > prev);
                prev = w;
            }
            Assert.InRange(AutoExposure.OutshineWeight(95, e), 7.0e-4, 7.8e-4);
            Assert.True(AutoExposure.OutshineWeight(60, e) < 1e-6);
        }
    }

    public class IntegratorTests
    {
        private const double R = 6371.0;

        private static BodyAirglow Earth() =>
            ConfigReader.Read(TextConfigNode.Parse(File.ReadAllText("Earth.cfg")).GetNodes("AIRGLOW").Single());

        private static LayerDef Synthetic(double peak, double fwhm) => new LayerDef
        {
            Name = "test", Group = "green", PeakKm = peak, HwhmLowKm = fwhm / 2, HwhmHighKm = fwhm / 2, IntensityR = 100,
        };

        [Fact]
        public void ExportedConfigParses()
        {
            var e = Earth();
            Assert.Equal("Earth", e.Body);
            Assert.Equal(9, e.Layers.Count);
            Assert.Equal(6, e.Lower.AirLogDensityPoly.Length);
            Assert.Equal(12, e.ExtinctionBinsNm.Length);
            // The bins partition the visible spectrum: their colours sum to the layer total.
            foreach (var l in e.Layers)
                for (int c = 0; c < 3; c++)
                {
                    double sum = 0;
                    for (int k = 0; k < l.BinCount; k++) sum += l.BinCameraRGB[3 * k + c];
                    Assert.Equal(l.CameraRGB[c], sum, Math.Abs(l.CameraRGB[c]) * 1e-4 + 1e-30);
                }
            Assert.Contains(e.Layers, l => l.Name == "OI 557.7 mesospheric" && Math.Abs(l.IntensityR - 162.67) < 0.01);
        }

        [Fact]
        public void ZenithColumnEqualsIntensityForEveryLayer()
        {
            var e = Earth();
            var o = new Vec3(0, 0, R + 1e-3);
            var res = AirglowIntegrator.Integrate(o, new Vec3(0, 0, 1), R, e.Layers, null, 100, 100);
            for (int i = 0; i < e.Layers.Count; i++)
                if (e.Layers[i].Enabled)
                    Assert.Equal(e.Layers[i].IntensityR, res[i].ColumnR, e.Layers[i].IntensityR * 1e-4); // quadrature error at the profile kink ~1e-5
        }

        [Theory]
        [InlineData(30, 1.149)]
        [InlineData(60, 1.916)]
        [InlineData(80, 4.116)]
        public void VanRhijnFromGround(double zenithDeg, double expected)
        {
            var layer = Synthetic(97, 0.5);
            var o = new Vec3(0, 0, R + 1e-3);
            double z = zenithDeg * Math.PI / 180;
            var d = new Vec3(Math.Sin(z), 0, Math.Cos(z));
            double col = AirglowIntegrator.Integrate(o, d, R, new[] { layer }, null, 100, 100)[0].ColumnR;
            Assert.Equal(expected, col / 100.0, 3);
        }

        [Fact]
        public void LimbToZenithRatioMatchesPythonReference()
        {
            // reference/tests: 56.4x at tangent 93.8 km for a 10 km FWHM layer at 97 km, from 420 km.
            var layer = Synthetic(97, 10);
            double r0 = R + 420, best = 0, bestT = 0;
            for (double t = 85; t < 100; t += 0.05)
            {
                double sinA = (R + t) / r0;
                var d = new Vec3(-Math.Sqrt(1 - sinA * sinA), sinA, 0);
                double col = AirglowIntegrator.Integrate(new Vec3(r0, 0, 0), d, R, new[] { layer }, null, 100, 100)[0].ColumnR;
                if (col > best) { best = col; bestT = t; }
            }
            Assert.Equal(56.4, best / 100.0, 1);
            Assert.InRange(bestT, 93.7, 93.9);
        }

        [Fact]
        public void ZenithTransmittanceMatchesPythonReference()
        {
            // Python (MSIS column, exported sigmas): green line T = 0.84431 in every camera channel.
            var e = Earth();
            var o = new Vec3(0, 0, R + 1e-3);
            var res = AirglowIntegrator.Integrate(o, new Vec3(0, 0, 1), R, e.Layers, e.Lower, 100, 100);
            int g = e.Layers.FindIndex(l => l.Name == "OI 557.7 mesospheric");
            var L = e.Layers[g];
            double luma(double[] v) => v[0] * 0.2126729 + v[1] * 0.7151522 + v[2] * 0.0721750;
            Assert.Equal(0.84431, luma(res[g].CameraRGB) / (res[g].ColumnR * luma(L.CameraRGB)), 3);
        }

        [Fact]
        public void PhotographicPresetScale()
        {
            Assert.Equal(70.7, CameraExposure.IssD4.Scale, 1);
        }
    }

    public class AutoExposureTests
    {
        private const double R = 6371.0;
        private static readonly Vec3 Sun = new Vec3(1, 0, 0);

        [Fact]
        public void DarkSceneGivesTheIssNightExposure()
        {
            Assert.Equal(CameraExposure.IssD4.Scale, AutoExposure.Scale(0.0, CameraExposure.IssD4), 9);
            Assert.Equal(0.0014724, AutoExposure.MeteredLuminance(CameraExposure.IssD4), 6);
        }

        [Fact]
        public void BrighterSceneScalesExposureDownInProportion()
        {
            double night = AutoExposure.Scale(0, CameraExposure.IssD4);
            double moonlit = AutoExposure.Scale(10 * AutoExposure.MeteredLuminance(CameraExposure.IssD4), CameraExposure.IssD4);
            Assert.Equal(night / 10, moonlit, 6);
        }

        [Fact]
        public void TwilightCurveMatchesPatat2006()
        {
            Assert.Equal(1.98, AutoExposure.TwilightSkyLuminance(95), 2);
            Assert.Equal(6.78e-3, AutoExposure.TwilightSkyLuminance(100), 4);
            Assert.Equal(3.20e-4, AutoExposure.TwilightSkyLuminance(110), 5);  // held at the 105 deg value
        }

        [Fact]
        public void SunlitDiscSeenFromOrbitIsBrightNightSideIsDark()
        {
            var cam = new Vec3(R + 420, 0, 0);
            var down = new Vec3(-1, 0, 0);
            Assert.True(AutoExposure.RayLuminance(cam, down, R, Sun, 60) > 1e4);          // sub-solar point
            Assert.Equal(0.0, AutoExposure.RayLuminance(cam, down, R, new Vec3(-1, 0, 0), 60));
            Assert.Equal(0.0, AutoExposure.RayLuminance(cam, new Vec3(0, 1, 0), R, Sun, 60)); // space
        }

        [Fact]
        public void GroundSkyFollowsTwilight()
        {
            var cam = new Vec3(R + 0.001, 0, 0);
            var up = new Vec3(1, 0, 0);
            double z = 100 * Math.PI / 180;
            var sun100 = new Vec3(Math.Cos(z), Math.Sin(z), 0);
            Assert.Equal(AutoExposure.TwilightSkyLuminance(100), AutoExposure.RayLuminance(cam, up, R, sun100, 60), 9);
        }
    }
}
