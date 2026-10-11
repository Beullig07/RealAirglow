"""Vertical volume-emission-rate (VER) profile shapes, normalised to a unit column.

The shader will evaluate the same parametric forms, so the reference model uses them too.
A profile p(z) has units of m^-1 and integrates to 1 over altitude; the layer VER is
I_R * 1e10 * p(z) photons m^-3 s^-1.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np

LN2 = np.log(2.0)


@dataclass(frozen=True)
class AsymGaussian:
    """exp(-ln2 * (z - z0)^2 / h^2), with half width at half maximum h_lo below z0 and
    h_hi above z0. All lengths in km."""
    peak_km: float
    hwhm_lo_km: float
    hwhm_hi_km: float

    @property
    def fwhm_km(self) -> float:
        return self.hwhm_lo_km + self.hwhm_hi_km

    def _norm(self) -> float:
        # Integral over z (m) of the unnormalised shape: sqrt(pi/ln2)/2 * (h_lo + h_hi).
        return 0.5 * np.sqrt(np.pi / LN2) * (self.hwhm_lo_km + self.hwhm_hi_km) * 1e3

    def __call__(self, z_km):
        z = np.asarray(z_km, float)
        h = np.where(z < self.peak_km, self.hwhm_lo_km, self.hwhm_hi_km)
        return np.exp(-LN2 * ((z - self.peak_km) / h) ** 2) / self._norm()

    def support_km(self, n_hwhm: float = 4.5):
        """Altitude band outside which the profile is below exp(-ln2 * n^2) of its peak."""
        return (self.peak_km - n_hwhm * self.hwhm_lo_km, self.peak_km + n_hwhm * self.hwhm_hi_km)


def fit_asym_gaussian(z_km, ver):
    """Fit an AsymGaussian to a sampled VER profile.

    The peak is located by a parabola through the three samples around the maximum and the
    half-widths by linear interpolation of the half-maximum crossings. This reproduces the
    peak height and FWHM of the input exactly (to sampling accuracy) and its asymmetry.
    """
    z = np.asarray(z_km, float)
    v = np.asarray(ver, float)
    i = int(np.argmax(v))
    if 0 < i < len(z) - 1:
        y0, y1, y2 = v[i - 1], v[i], v[i + 1]
        dz = z[i + 1] - z[i]
        denom = y0 - 2 * y1 + y2
        off = 0.5 * (y0 - y2) / denom if denom != 0 else 0.0
        z0 = z[i] + off * dz
        vmax = y1 - 0.25 * (y0 - y2) * off
    else:
        z0, vmax = z[i], v[i]
    half = 0.5 * vmax
    lo = np.where(v[:i] < half)[0]
    hi = np.where(v[i:] < half)[0]
    if len(lo) == 0 or len(hi) == 0:
        raise ValueError("profile does not fall below half maximum on both sides")
    j = lo[-1]
    z_lo = z[j] + (half - v[j]) * (z[j + 1] - z[j]) / (v[j + 1] - v[j])
    k = i + hi[0]
    z_hi = z[k - 1] + (half - v[k - 1]) * (z[k] - z[k - 1]) / (v[k] - v[k - 1])
    return AsymGaussian(float(z0), float(z0 - z_lo), float(z_hi - z0))
