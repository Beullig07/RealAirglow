"""Reference-model tests (docs/SPEC.md deliverable 3 and the intensity-control tests)."""
import numpy as np
import pytest

from airglow_ref import atmosphere, colour, exposure, geometry, layers, render, solar
from airglow_ref.constants import R_EARTH
from airglow_ref.layers import Component
from airglow_ref.profiles import AsymGaussian

GROUND = np.array([0.0, 0.0, R_EARTH + 1.0])


@pytest.fixture(scope="module")
def comps():
    return layers.default_components()


def synthetic(peak_km, fwhm_km, intensity=100.0):
    h = fwhm_km / 2
    return Component("test", "green", AsymGaussian(peak_km, h, h),
                     np.array([557.889]), np.array([intensity]), np.array([0.0]))


def van_rhijn(z_deg, h_km):
    s = np.sin(np.radians(z_deg))
    return 1.0 / np.sqrt(1.0 - (R_EARTH / (R_EARTH + h_km * 1e3)) ** 2 * s ** 2)


def ground_columns(comp, z_deg):
    z = np.radians(np.asarray(z_deg, float))
    d = np.stack([np.sin(z), np.zeros_like(z), np.cos(z)], axis=1)
    o = np.tile(GROUND, (len(z), 1))
    return geometry.integrate_component(o, d, comp).column_R.sum(1)


# --- zenith intensities ------------------------------------------------------------------

def test_zenith_intensity_every_component(comps):
    for c in comps:
        assert geometry.zenith_column_R(c) == pytest.approx(c.intensity_R, rel=1e-5), c.name


def test_reference_intensities_match_palace(comps):
    by = {c.name: c for c in comps}
    assert by["OI 557.7 mesospheric"].intensity_R == pytest.approx(163.0, abs=1.0)   # [Noll2025]
    assert by["Na D"].intensity_R == pytest.approx(36.5, abs=0.1)
    assert by["FeO-like continuum"].intensity_R == pytest.approx(2800.0, rel=0.01)
    oh = by["OH Meinel"]
    vis = (oh.line_nm > 642 * 1.000277) & (oh.line_nm < 858 * 1.000277)   # air -> vacuum
    assert oh.line_R[vis].sum() == pytest.approx(3100.0, rel=0.03)
    o2 = by["O2 atmospheric + UV bands"]
    b01 = (o2.line_nm > 860) & (o2.line_nm < 875)
    assert o2.line_R[b01].sum() == pytest.approx(340.0, rel=0.05)
    red = by["OI 630/636 + NI 520"]
    assert red.intensity_R == pytest.approx(80.0 * (164.0 + 2.99) / 164.0, rel=0.02)


def test_solar_cycle_scaling(comps):
    green = next(c for c in comps if c.name == "OI 557.7 mesospheric")
    assert green.intensity_at(200.0) / green.intensity_at(100.0) == pytest.approx(1.754, rel=1e-3)


# --- van Rhijn (ground) ------------------------------------------------------------------

def test_van_rhijn_thin_shell():
    comp = synthetic(97.0, 0.5)
    z = np.array([0, 30, 60, 70, 80, 85, 88])
    col = ground_columns(comp, z)
    np.testing.assert_allclose(col / col[0], van_rhijn(z, 97.0), rtol=2e-3)


def test_van_rhijn_real_green_layer(comps):
    green = next(c for c in comps if c.name == "OI 557.7 mesospheric")
    z = np.array([0, 30, 60, 70, 80])
    col = ground_columns(green, z)
    np.testing.assert_allclose(col / col[0], van_rhijn(z, green.profile.peak_km), rtol=5e-3)


# --- limb enhancement --------------------------------------------------------------------

def test_limb_to_zenith_ratio_10km_layer():
    comp = synthetic(97.0, 10.0)
    t = np.arange(85.0, 100.0, 0.05)
    limb = render.limb_profile(420.0, [comp], t)["test"]
    ratio = limb.max() / comp.intensity_R
    assert ratio == pytest.approx(56.4, rel=0.01)       # docs/PHYSICS.md §3.2
    assert ratio >= 35.0                                # Gemini S011 lower bound [Koomen1971]
    assert t[np.argmax(limb)] == pytest.approx(93.8, abs=0.15)


def test_limb_ratio_real_layers_in_observed_range(comps):
    for name in ("OI 557.7 mesospheric", "OH Meinel", "Na D"):
        c = next(x for x in comps if x.name == name)
        t = np.arange(c.profile.peak_km - 12, c.profile.peak_km + 2, 0.05)
        ratio = render.limb_profile(420.0, [c], t)[name].max() / c.intensity_R
        assert 35.0 <= ratio <= 75.0, (name, ratio)


def abel_limb(profile, tangent_km, u_max_km=4000.0):
    """Independent limb integral: L(rt) = 2 * integral over u of eps(z(u)) du,
    with R + z = sqrt(rt^2 + u^2) (u = distance from the tangent point)."""
    rt = R_EARTH + tangent_km * 1e3
    u = np.linspace(0.0, u_max_km * 1e3, 400001)
    z = (np.sqrt(rt ** 2 + u ** 2) - R_EARTH) / 1e3
    return 2.0 * np.trapezoid(profile(z), u)


def test_limb_peak_tangent_altitudes_from_400km(comps):
    for c in comps:
        if not c.enabled or c.profile.peak_km > 150:
            continue
        t = np.arange(c.profile.peak_km - 12, c.profile.peak_km + 3, 0.05)
        ray = render.limb_profile(400.0, [c], t)[c.name]
        abel = np.array([abel_limb(c.profile, x) for x in t[::10]])
        t_ray = t[np.argmax(ray)]
        t_abel = t[::10][np.argmax(abel)]
        assert t_ray == pytest.approx(t_abel, abs=0.6), c.name
        # Limb brightness peaks below the VER peak by roughly 0.75 sigma (docs §3.2).
        sigma_lo = c.profile.hwhm_lo_km / 1.1774
        assert c.profile.peak_km - t_ray == pytest.approx(0.75 * sigma_lo, abs=0.6), c.name


def test_red_layer_limb_peak_below_ver_peak(comps):
    red = next(c for c in comps if c.group == "red" and c.enabled)
    t = np.arange(150.0, 300.0, 1.0)
    prof = render.limb_profile(420.0, [red], t)[red.name]
    assert 190.0 < t[np.argmax(prof)] < red.profile.peak_km


# --- extinction and colour ---------------------------------------------------------------

def test_vertical_rayleigh_optical_depth_550nm():
    air = atmosphere.AirColumnTable.build(atmosphere.MsisConditions(lat_deg=45.0))
    z = np.linspace(0, 60, 6001)
    N = np.trapezoid(air.density(z), z * 1e3)
    tau = N * atmosphere.rayleigh_cross_section(550.0)
    assert tau == pytest.approx(0.0973, rel=0.03)  # [Bodhaine1999] Table 3: ~0.0973


def test_gamut_map_preserves_luminance_and_removes_negatives():
    rgb = np.array([[-0.2, 1.0, 0.05], [1.0, -0.01, 0.02], [0.3, 0.3, 0.3]])
    out = colour.gamut_map(rgb)
    np.testing.assert_allclose(out @ colour.LUMA, rgb @ colour.LUMA, rtol=1e-12)
    assert (out >= -1e-15).all()
    np.testing.assert_allclose(out[2], rgb[2])


@pytest.mark.parametrize("make", [colour.cie_response, colour.camera_response])
def test_line_hues(comps, make):
    resp = make()
    by = {c.name: c for c in comps}
    g = colour.gamut_map(resp.component_rgb(by["OI 557.7 mesospheric"]))
    r = colour.gamut_map(resp.component_rgb(by["OI 630/636 + NI 520"]))
    assert g[1] > g[0] and g[1] > g[2]
    assert r[0] > r[1] and r[0] > r[2]


def test_cie_luminance_of_green_line(comps):
    green = next(c for c in comps if c.name == "OI 557.7 mesospheric")
    Y = colour.cie_response().component_rgb(green) @ colour.LUMA
    assert Y == pytest.approx(1.933e-7, rel=0.01)   # docs §2: 1.93e-7 cd m^-2 per R


def test_mesopic_limits():
    m, L = colour.mesopic_m(np.array([1e-4, 100.0]), np.array([1e-4, 100.0]))
    assert m[0] == 0.0 and m[1] == 1.0
    assert L[1] == pytest.approx(100.0)


# --- display multipliers (owner's intensity controls) -------------------------------------

def test_display_multipliers_linear_and_zero():
    rng = np.random.default_rng(1)
    layer = {"green": rng.random((4, 3)), "red": rng.random((4, 3))}
    base = exposure.apply_display_multipliers(layer, 1.0, {})
    np.testing.assert_allclose(exposure.apply_display_multipliers(layer, 0.3, {}), 0.3 * base)
    np.testing.assert_allclose(exposure.apply_display_multipliers(layer, 2.0, {}), 2.0 * base)
    assert not exposure.apply_display_multipliers(layer, 0.0, {"green": 3.0}).any()
    only_red = exposure.apply_display_multipliers(layer, 1.0, {"green": 0.0})
    np.testing.assert_array_equal(only_red, layer["red"])


def test_display_multipliers_do_not_change_physics(comps):
    before = [c.intensity_R for c in comps]
    exposure.apply_display_multipliers({"green": np.ones((1, 3))}, 0.3, {"green": 0.5})
    assert [c.intensity_R for c in comps] == before


# --- sodium twilight resonance -----------------------------------------------------------

def test_sodium_g_factors():
    g2, g1 = solar.sodium_g()
    assert g2 == pytest.approx(0.507, rel=0.01)
    assert g1 == pytest.approx(0.299, rel=0.01)
