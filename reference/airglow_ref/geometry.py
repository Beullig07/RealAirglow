"""Ray integration through spherical emission shells.

Positions are planet-centred, in metres, double precision. For each ray the altitude band
that holds a layer is a set of at most two path intervals; each interval is integrated with
Gauss-Legendre quadrature, so thin layers are resolved exactly wherever the ray is.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from .constants import R_EARTH, PHOTONS_PER_RAYLEIGH

GL_NODES, GL_WEIGHTS = np.polynomial.legendre.leggauss(128)
GL_NODES_ABS, GL_WEIGHTS_ABS = np.polynomial.legendre.leggauss(96)


def _sphere(o, d, r):
    """Entry/exit distances of rays o + s d with the sphere |x| = r (NaN if missed)."""
    b = np.einsum("ij,ij->i", o, d)
    c = np.einsum("ij,ij->i", o, o) - r * r
    disc = b * b - c
    sq = np.sqrt(np.where(disc > 0, disc, np.nan))
    return -b - sq, -b + sq


def planet_hit(o, d, radius=R_EARTH):
    """Distance to the first intersection with the planet surface, inf if none ahead."""
    s0, s1 = _sphere(o, d, radius)
    hit = np.isfinite(s0) & (s1 > 0) & (s0 > 0)
    return np.where(hit, s0, np.inf)


def band_intervals(o, d, r_lo, r_hi, s_end):
    """Path intervals where r_lo <= |o + s d| <= r_hi and 0 <= s <= s_end.

    Returns arrays a (N, 2), b (N, 2); an interval is empty when b <= a.
    """
    s2m, s2p = _sphere(o, d, r_hi)
    s1m, s1p = _sphere(o, d, r_lo)
    inner = np.isfinite(s1m)
    a = np.stack([s2m, np.where(inner, s1p, np.nan)], axis=1)
    b = np.stack([np.where(inner, s1m, s2p), np.where(inner, s2p, np.nan)], axis=1)
    a = np.maximum(a, 0.0)
    b = np.minimum(b, s_end[:, None])
    bad = ~np.isfinite(a) | ~np.isfinite(b) | (b <= a)
    a = np.where(bad, 0.0, a)
    b = np.where(bad, 0.0, b)
    return a, b


@dataclass
class RayResult:
    """Per-ray, per-interval results for one component.

    column_R[N, 2]: apparent column emission rate (rayleighs) collected on each interval.
    N_air, N_O3[N, 2]: slant columns (m^-2) of air and ozone between the observer and the
    interval; aer[N, 2]: slant aerosol optical depth at 550 nm.
    """
    column_R: np.ndarray
    N_air: np.ndarray
    N_O3: np.ndarray
    aer: np.ndarray


def _absorbers(o, d, s_end, lower, air):
    """Slant absorber columns over the absorbing shell intervals (radius R..R+top)."""
    a, b = band_intervals(o, d, R_EARTH - 1.0, R_EARTH + lower.top_km * 1e3, s_end)
    half = 0.5 * (b - a)
    mid = 0.5 * (b + a)
    s = mid[..., None] + half[..., None] * GL_NODES_ABS            # (N, 2, n)
    pts = o[:, None, None, :] + s[..., None] * d[:, None, None, :]
    z_km = (np.linalg.norm(pts, axis=-1) - R_EARTH) / 1e3
    w = half[..., None] * GL_WEIGHTS_ABS
    n_air = (air.density(z_km) * w).sum(-1)
    n_o3 = (lower.ozone_density(z_km) * w).sum(-1)
    aer = (lower.aerosol_extinction_550(z_km) * w).sum(-1)
    return a, b, n_air, n_o3, aer


def integrate_component(o, d, comp, *, s_end=None, lower=None, air=None, modulation=None,
                        f107=100.0):
    """Integrate one component's VER along rays.

    o, d: (N, 3) ray origins (m, planet-centred) and unit directions.
    modulation(points[..., 3], comp) -> multiplicative factor (horizontal structure), or None.
    Returns RayResult.
    """
    o = np.asarray(o, float)
    d = np.asarray(d, float)
    if s_end is None:
        s_end = planet_hit(o, d)
    z_lo, z_hi = comp.profile.support_km()
    a, b = band_intervals(o, d, R_EARTH + max(z_lo, 0.0) * 1e3, R_EARTH + z_hi * 1e3, s_end)
    half = 0.5 * (b - a)
    s = 0.5 * (b + a)[..., None] + half[..., None] * GL_NODES                # (N, 2, n)
    pts = o[:, None, None, :] + s[..., None] * d[:, None, None, :]
    z_km = (np.linalg.norm(pts, axis=-1) - R_EARTH) / 1e3
    ver_per_R = comp.profile(z_km)                                           # m^-1 per R
    if modulation is not None:
        ver_per_R = ver_per_R * modulation(pts, comp)
    column = (ver_per_R * half[..., None] * GL_WEIGHTS).sum(-1) * comp.intensity_at(f107)

    zeros = np.zeros_like(column)
    res = RayResult(column, zeros.copy(), zeros.copy(), zeros.copy())
    if lower is not None:
        aa, ab, n_air, n_o3, aer = _absorbers(o, d, s_end, lower, air)
        for k in range(2):           # emission interval k
            for j in range(2):       # absorbing interval j lies before emission interval k
                before = (ab[:, j] > aa[:, j]) & (ab[:, j] <= a[:, k] + 1.0) & (b[:, k] > a[:, k])
                res.N_air[:, k] += np.where(before, n_air[:, j], 0.0)
                res.N_O3[:, k] += np.where(before, n_o3[:, j], 0.0)
                res.aer[:, k] += np.where(before, aer[:, j], 0.0)
    return res


def zenith_column_R(comp, f107=100.0):
    """Column seen looking straight up from the ground with no extinction (sanity check)."""
    o = np.array([[0.0, 0.0, R_EARTH + 1.0]])
    d = np.array([[0.0, 0.0, 1.0]])
    return float(integrate_component(o, d, comp, f107=f107).column_R.sum())


def column_to_photon_radiance(column_R):
    """Apparent radiance (photons s^-1 m^-2 sr^-1) for an apparent column in rayleighs."""
    return np.asarray(column_R) * PHOTONS_PER_RAYLEIGH / (4.0 * np.pi)
