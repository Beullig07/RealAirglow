"""Single-scattering estimate of airglow light scattered by the lower atmosphere into a
ground observer's line of sight (docs/PHYSICS.md §11).

Assumptions (estimate only, not used by the renderer yet): horizontally uniform airglow;
incident radiance at a scattering height z equals the direct airglow sky seen from the ground
(the layers are ~90 km up, scattering happens below ~10 km) attenuated from z to the top;
black ground; Rayleigh phase function and a Henyey-Greenstein aerosol phase function.
"""
from __future__ import annotations

import numpy as np

from . import atmosphere
from .constants import R_EARTH


def slant_path(z_km, cos_zen, top_km=60.0, n=400):
    """Sample points along a path from height z_km toward zenith angle acos(cos_zen) up to
    top_km. Returns (s m, z_km samples, weights m)."""
    r0 = R_EARTH + z_km * 1e3
    rt = R_EARTH + top_km * 1e3
    b = r0 * cos_zen
    s_top = -b + np.sqrt(b * b - (r0 * r0 - rt * rt))
    x, w = np.polynomial.legendre.leggauss(n)
    s = 0.5 * s_top * (x + 1)
    z = (np.sqrt(r0 * r0 + s * s + 2 * r0 * s * cos_zen) - R_EARTH) / 1e3
    return s, z, 0.5 * s_top * w


def optical_depth(z_km, cos_zen, lam_nm, lower, air):
    """Optical depth from height z along zenith angle acos(cos_zen) to the top."""
    s, z, w = slant_path(z_km, cos_zen, lower.top_km)
    from .render import _o3_cache
    lam_o3, sig = _o3_cache(lower.ozone_temperature_K)
    beta = (air.density(z) * atmosphere.rayleigh_cross_section(lam_nm)
            + lower.ozone_density(z) * np.interp(lam_nm, lam_o3, sig)
            + lower.aerosol_extinction_550(z) * lower.aerosol_spectral_factor(lam_nm))
    return float((beta * w).sum())


def scattered_vs_direct(direct_vs_zenith, lam_nm, view_zen_deg, lower=atmosphere.LowerAtmosphere(),
                        cond=atmosphere.MsisConditions(), hg_g=0.7, n_mu=60, n_phi=48, n_s=120):
    """Return (direct, scattered) radiance in units of the zenith airglow radiance.

    direct_vs_zenith(theta_rad) -> airglow radiance above the atmosphere relative to zenith.
    """
    air = atmosphere.AirColumnTable.build(cond, lower.top_km)
    mu, wmu = np.polynomial.legendre.leggauss(n_mu)
    mu = 0.5 * (mu + 1); wmu = 0.5 * wmu            # upper hemisphere only (black ground)
    phi = (np.arange(n_phi) + 0.5) * 2 * np.pi / n_phi
    src = direct_vs_zenith(np.arccos(mu))
    zg = np.linspace(0.0, lower.top_km, 31)
    tau_up = np.array([[optical_depth(z, m, lam_nm, lower, air) for m in mu] for z in zg])
    sig_R = atmosphere.rayleigh_cross_section(lam_nm)
    aer_f = lower.aerosol_spectral_factor(lam_nm)

    direct, scat = [], []
    for vz in np.radians(view_zen_deg):
        cv = np.cos(vz)
        direct.append(direct_vs_zenith(vz) * np.exp(-optical_depth(0.0, cv, lam_nm, lower, air)))
        v = np.array([np.sin(vz), 0.0, cv])
        sm = np.sqrt(1 - mu ** 2)
        om = np.stack([sm[:, None] * np.cos(phi), sm[:, None] * np.sin(phi),
                       np.broadcast_to(mu[:, None], (n_mu, n_phi))], axis=-1)
        cosT = om @ v
        pR = 3 / (16 * np.pi) * (1 + cosT ** 2)
        pA = (1 - hg_g ** 2) / (4 * np.pi * (1 + hg_g ** 2 - 2 * hg_g * cosT) ** 1.5)
        dw = (wmu[:, None] * (2 * np.pi / n_phi))
        s, z, w = slant_path(0.0, cv, lower.top_km, n_s)
        tau_cum = np.concatenate([[0.0], np.cumsum(
            (air.density(z) * sig_R + lower.aerosol_extinction_550(z) * aer_f) * w)[:-1]])
        total = 0.0
        for zi, wi, tc in zip(z, w, tau_cum):
            Tin = np.exp(-np.array([np.interp(zi, zg, tau_up[:, j]) for j in range(n_mu)]))
            Lin = (src * Tin)[:, None]
            JR = (pR * Lin * dw).sum()
            JA = (pA * Lin * dw).sum()
            bR = air.density(zi) * sig_R
            bA = lower.aerosol_extinction_550(zi) * aer_f
            total += np.exp(-tc) * (bR * JR + bA * JA) * wi
        scat.append(total)
    return np.array(direct), np.array(scat)
