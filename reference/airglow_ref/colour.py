"""Spectral response modes (docs/PHYSICS.md §12).

Every mode maps a component spectrum to linear-sRGB tristimulus values whose luminance Y is
in cd m^-2 per rayleigh of apparent column (photopic for camera and science modes). The
spectra are photon spectra (rayleighs per line, R nm^-1 for continua); they are converted to
spectral radiance with E = hc/lambda before weighting, because the CIE functions and the
measured camera sensitivities are defined per unit radiant power.
"""
from __future__ import annotations

import numpy as np

from . import data
from .constants import KM_PHOTOPIC, KM_SCOTOPIC, PHOTONS_PER_RAYLEIGH, photon_energy

# sRGB / Rec. 709 primaries and D65 white (IEC 61966-2-1).
_PRIMARIES = np.array([[0.64, 0.33], [0.30, 0.60], [0.15, 0.06]])
_WHITE = np.array([0.3127, 0.3290])


def _rgb_to_xyz_matrix():
    xyz = np.array([[x / y, 1.0, (1 - x - y) / y] for x, y in _PRIMARIES]).T
    w = np.array([_WHITE[0] / _WHITE[1], 1.0, (1 - _WHITE.sum()) / _WHITE[1]])
    return xyz * np.linalg.solve(xyz, w)


RGB_TO_XYZ = _rgb_to_xyz_matrix()
XYZ_TO_RGB = np.linalg.inv(RGB_TO_XYZ)
LUMA = RGB_TO_XYZ[1]  # Y = LUMA . rgb

# Wavelength grid for continua and transmittance (1 nm, the CIE table spacing).
GRID_NM = np.arange(360.0, 831.0, 1.0)


def _radiance_per_R(lam_nm):
    """W m^-2 sr^-1 per rayleigh of a line at lam_nm (vacuum)."""
    return PHOTONS_PER_RAYLEIGH / (4 * np.pi) * photon_energy(np.asarray(lam_nm) * 1e-9)


def component_spectrum(comp, f107=100.0):
    """Spectral radiance per unit column on GRID_NM plus lines, normalised to 1 R total.

    Returns (line_nm, line_W [W m^-2 sr^-1 per R of component], cont_W [W m^-2 sr^-1 nm^-1
    per R of component on GRID_NM]). Only 360-830 nm is kept; the normalisation uses the
    full-spectrum component intensity, so invisible emission still counts in the rayleighs.
    """
    total = comp.intensity_at(f107)
    m = (comp.line_nm >= GRID_NM[0]) & (comp.line_nm <= GRID_NM[-1])
    line_nm = comp.line_nm[m]
    line_R = comp.line_R[m] * comp.solar_factor_lines(f107)[m] if comp.line_R.size else comp.line_R[m]
    line_W = line_R / total * _radiance_per_R(line_nm)
    cont_W = np.zeros_like(GRID_NM)
    if comp.cont_nm is not None:
        c = np.interp(GRID_NM, comp.cont_nm, comp.cont_R_per_nm, left=0.0, right=0.0)
        cont_W = c * comp.solar_factor_cont(f107) / total * _radiance_per_R(GRID_NM)
    return line_nm, line_W, cont_W


class Response:
    """A spectral response: 3 channel sensitivity functions s_c(lambda) and a 3x3 matrix to
    linear sRGB."""

    def __init__(self, lam_nm, sens, to_rgb, name):
        self.lam_nm, self.sens, self.to_rgb, self.name = lam_nm, sens, to_rgb, name

    def weights(self, lam_nm):
        return np.stack([np.interp(lam_nm, self.lam_nm, self.sens[:, k], left=0.0, right=0.0)
                         for k in range(3)], axis=-1)

    def component_rgb(self, comp, transmittance=None, f107=100.0):
        """Linear sRGB per rayleigh of apparent column. transmittance(lam_nm) -> [..., L]
        optional; then the result has the leading shape of the transmittance."""
        line_nm, line_W, cont_W = component_spectrum(comp, f107)
        wl, wc = self.weights(line_nm), self.weights(GRID_NM)
        if transmittance is None:
            raw = line_W @ wl + cont_W @ wc
        else:
            tl, tc = transmittance(line_nm), transmittance(GRID_NM)
            raw = (tl * line_W) @ wl + (tc * cont_W) @ wc
        return raw @ self.to_rgb.T


def cie_response():
    """CIE 1931 2-degree observer -> linear sRGB; Y in cd m^-2 (K_m = 683 lm/W)."""
    lam, xyz = data.cie_cmf_1931()
    return Response(lam, KM_PHOTOPIC * xyz, XYZ_TO_RGB, "CIE 1931")


def camera_response(camera="Nikon D3"):
    """Measured DSLR sensitivity [Jiang2013] with a colour-correction matrix fitted by least
    squares to the CIE 1931 functions over 400-720 nm (maximum-ignorance correction), so
    camera luminance is in cd m^-2 to the extent the camera satisfies the Luther condition."""
    lam_c, rgb = data.camera_sensitivity(camera)
    lam_x, xyz = data.cie_cmf_1931()
    target = np.stack([np.interp(lam_c, lam_x, xyz[:, k]) for k in range(3)], axis=1)
    M, *_ = np.linalg.lstsq(rgb, target, rcond=None)      # rgb @ M ~= xyz
    to_rgb = XYZ_TO_RGB @ (KM_PHOTOPIC * M.T)
    return Response(lam_c, rgb, to_rgb, f"camera {camera}")


def luminances(comp, f107=100.0):
    """(photopic cd m^-2, scotopic cd m^-2) per rayleigh of apparent column."""
    line_nm, line_W, cont_W = component_spectrum(comp, f107)
    out = []
    for lam, V, K in ((*data.cie_v_photopic(), KM_PHOTOPIC), (*data.cie_v_scotopic(), KM_SCOTOPIC)):
        out.append(K * (line_W @ np.interp(line_nm, lam, V, left=0, right=0)
                        + cont_W @ np.interp(GRID_NM, lam, V, left=0, right=0)))
    return tuple(out)


def mesopic_m(L_p, L_s, iterations=40):
    """CIE 191:2010 adaptation coefficient m and mesopic luminance (fixed-point iteration,
    a = 0.7670, b = 0.3334, m0 = 0.5, as reproduced by Gao et al. 2017)."""
    a, b, v0 = 0.7670, 0.3334, KM_PHOTOPIC / KM_SCOTOPIC
    L_p = np.asarray(L_p, float)
    L_s = np.asarray(L_s, float)
    m = np.full_like(L_p, 0.5)
    for _ in range(iterations):
        L_mes = (m * L_p + (1 - m) * L_s * v0) / (m + (1 - m) * v0)
        m = np.clip(a + b * np.log10(np.maximum(L_mes, 1e-30)), 0.0, 1.0)
    L_mes = (m * L_p + (1 - m) * L_s * v0) / (m + (1 - m) * v0)
    return m, L_mes


def eye_display(rgb_photopic, L_p, L_s):
    """Human-eye mode: luminance from the CIE 191 mesopic model; chromaticity from the
    photopic colour desaturated toward white by (1 - m). The desaturation rule is an
    approximation (labelled in docs/PHYSICS.md §12.2); the luminance follows CIE 191."""
    m, L_mes = mesopic_m(L_p, L_s)
    Y = np.maximum(rgb_photopic @ LUMA, 1e-30)[..., None]
    chroma = rgb_photopic / Y
    out = (m[..., None] * chroma + (1 - m[..., None])) * L_mes[..., None]
    return out, m


def gamut_map(rgb):
    """Bring linear sRGB into gamut by mixing toward the same-luminance white just enough to
    remove negative channels. Luminance is preserved exactly; nothing is clipped above."""
    rgb = np.asarray(rgb, float)
    Y = rgb @ LUMA
    mn = rgb.min(axis=-1)
    with np.errstate(divide="ignore", invalid="ignore"):
        t = np.where(mn < 0, mn / (mn - Y), 0.0)
    t = np.clip(np.nan_to_num(t), 0.0, 1.0)[..., None]
    return rgb * (1 - t) + Y[..., None] * t


SCIENCE_COLOURS = {  # false colours for the science mode (display only, not physics)
    "green": (0.1, 1.0, 0.1), "red": (1.0, 0.1, 0.1), "OH": (1.0, 0.6, 0.0),
    "Na": (1.0, 1.0, 0.0), "continuum": (0.3, 0.5, 1.0), "O2": (0.8, 0.2, 1.0),
}
