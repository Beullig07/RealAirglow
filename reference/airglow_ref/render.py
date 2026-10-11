"""Simple renderer: camera models and image formation from the 3D emission field."""
from __future__ import annotations

import numpy as np

from . import atmosphere, colour, geometry
from .constants import R_EARTH


def pinhole_dirs(forward, up, hfov_deg, width, height):
    """Unit ray directions (H, W, 3) for a pinhole camera."""
    f = np.asarray(forward, float); f /= np.linalg.norm(f)
    r = np.cross(f, up); r /= np.linalg.norm(r)
    u = np.cross(r, f)
    tx = np.tan(np.radians(hfov_deg) / 2)
    ty = tx * height / width
    x = (2 * (np.arange(width) + 0.5) / width - 1) * tx
    y = (1 - 2 * (np.arange(height) + 0.5) / height) * ty
    X, Y = np.meshgrid(x, y)
    d = f + X[..., None] * r + Y[..., None] * u
    return d / np.linalg.norm(d, axis=-1, keepdims=True)


def fisheye_dirs(zenith, north, size):
    """Equidistant all-sky fisheye (180 deg), north up, east left (as seen from below).
    Returns (dirs (S, S, 3), valid mask)."""
    z = np.asarray(zenith, float); z /= np.linalg.norm(z)
    n = np.asarray(north, float); n -= z * (n @ z); n /= np.linalg.norm(n)
    e = np.cross(n, z)
    c = (np.arange(size) + 0.5) / size * 2 - 1
    X, Y = np.meshgrid(c, -c)
    rho = np.hypot(X, Y)
    theta = rho * np.pi / 2
    phi = np.arctan2(-X, Y)  # azimuth from north, east to the left
    d = (np.cos(theta)[..., None] * z + np.sin(theta)[..., None]
         * (np.cos(phi)[..., None] * n + np.sin(phi)[..., None] * e))
    return d, rho <= 1.0


def transmittance_fn(N_air, N_O3, aer, lower):
    """lambda -> T(lambda) for rays with given slant columns (shape [M])."""
    lam_o3, sig_o3 = _o3_cache(lower.ozone_temperature_K)

    def T(lam_nm):
        lam_nm = np.atleast_1d(lam_nm)
        tau = (np.outer(N_air, atmosphere.rayleigh_cross_section(lam_nm))
               + np.outer(N_O3, np.interp(lam_nm, lam_o3, sig_o3))
               + np.outer(aer, lower.aerosol_spectral_factor(lam_nm)))
        return np.exp(-tau)
    return T


_O3 = {}


def _o3_cache(T):
    if T not in _O3:
        from . import data
        _O3[T] = data.ozone_cross_section(T)
    return _O3[T]


def render(obs, dirs, components, response, *, lower=None, cond=atmosphere.MsisConditions(),
           f107=100.0, chunk=4000, modulation=None):
    """Render per-group linear sRGB radiance (cd m^-2 luminance units).

    obs: (3,) observer position (m, planet-centred). dirs: (..., 3) unit directions.
    Returns dict group -> array (..., 3).
    """
    shape = dirs.shape[:-1]
    D = dirs.reshape(-1, 3)
    O = np.broadcast_to(np.asarray(obs, float), D.shape)
    air = atmosphere.AirColumnTable.build(cond, lower.top_km) if lower is not None else None
    base = {id(c): response.component_rgb(c, f107=f107) for c in components if c.enabled}
    out = {}
    for start in range(0, D.shape[0], chunk):
        o, d = O[start:start + chunk], D[start:start + chunk]
        s_end = geometry.planet_hit(o, d)
        for c in components:
            if not c.enabled:
                continue
            res = geometry.integrate_component(o, d, c, s_end=s_end, lower=lower, air=air,
                                               modulation=modulation, f107=f107)
            rgb = res.column_R.sum(1)[:, None] * base[id(c)]
            if lower is not None:
                for k in range(2):
                    absorbed = (res.N_air[:, k] > 0) & (res.column_R[:, k] > 0)
                    if absorbed.any():
                        idx = np.where(absorbed)[0]
                        T = transmittance_fn(res.N_air[idx, k], res.N_O3[idx, k], res.aer[idx, k], lower)
                        w = response.component_rgb(c, transmittance=T, f107=f107)
                        rgb[idx] += res.column_R[idx, k][:, None] * (w - base[id(c)])
            acc = out.setdefault(c.group, np.zeros((D.shape[0], 3)))
            acc[start:start + chunk] += rgb
    return {g: v.reshape(*shape, 3) for g, v in out.items()}


def limb_profile(obs_alt_km, components, tangent_km, f107=100.0):
    """Apparent column (R) per component vs tangent altitude, observer at obs_alt_km looking
    horizontally through the limb (no extinction)."""
    r0 = R_EARTH + obs_alt_km * 1e3
    rt = R_EARTH + np.asarray(tangent_km, float) * 1e3
    # Observer on the +x axis; the ray (-cos a, sin a, 0) has impact parameter r0 sin a = rt
    # and reaches its tangent point at s = r0 cos a.
    sin_a = rt / r0
    d = np.stack([-np.sqrt(1 - sin_a ** 2), sin_a, np.zeros_like(sin_a)], axis=1)
    o = np.tile([r0, 0.0, 0.0], (len(rt), 1))
    return {c.name: geometry.integrate_component(o, d, c, f107=f107).column_R.sum(1)
            for c in components if c.enabled}
