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
        public void DefaultIsThirtyPercentAndMinusOnePointSevenEv()
        {
            var c = new DisplayControls();
            Assert.Equal(0.30, c.MasterTarget, 12);
            Assert.Equal(-1.737, DisplayControls.ExposureValue(0.30), 3);
            foreach (var g in DisplayControls.Groups) Assert.Equal(0.30, c.Effective(g), 12);
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
            Assert.Equal(1.0, c.Effective("green"), 12);
            c.ApplyPreset(2); c.Update(1);
            Assert.Equal(0.15, c.Effective("green"), 12);
            c.SetGroup("Na", 3.0); c.Enabled = false;
            c.ResetToDefaults(); c.Update(1);
            Assert.True(c.Enabled);
            Assert.Equal(0.30, c.Effective("Na"), 12);
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
            for (int c = 0; c < 3; c++)
                Assert.Equal(0.84431, res[g].TransmittedR[c] / res[g].ColumnR, 3);
        }

        [Fact]
        public void PhotographicPresetScale()
        {
            Assert.Equal(70.7, CameraExposure.IssD4.Scale, 1);
        }
    }
}
