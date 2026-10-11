"""Resonance-scattering g-factors (docs/PHYSICS.md §4.2).

g = (pi e^2 / (4 pi eps0 m_e c)) f * Phi_nu, with Phi_nu the solar photon flux per unit
frequency at the line centre (photons s^-1 m^-2 Hz^-1) at 1 AU, for an atom at rest
relative to the Sun. The line is narrow (Doppler FWHM ~ 1.7 pm at 190 K) compared with the
solar Fraunhofer core, so the flux at the rest wavelength is used.
"""
from __future__ import annotations

import numpy as np

from . import data
from .constants import C_LIGHT, H_PLANCK

R_E_C = 2.8179403262e-15 * C_LIGHT        # classical electron radius x c, m^2 s^-1
PI_RE_C = np.pi * R_E_C                    # integrated cross-section per unit f, m^2 Hz

# Na D lines, NIST ASD (vacuum wavelength nm, oscillator strength f).
NA_D2 = (589.1583253, 0.641)
NA_D1 = (589.7558147, 0.320)


def photon_flux_per_hz(lam_nm):
    """Solar photon flux per Hz at 1 AU (TSIS-1 HSRS), photons s^-1 m^-2 Hz^-1."""
    lam, ssi = data.solar_spectrum()
    F_lambda = np.interp(lam_nm, lam, ssi) * 1e9          # W m^-2 m^-1
    lam_m = np.asarray(lam_nm) * 1e-9
    return F_lambda * lam_m ** 3 / (H_PLANCK * C_LIGHT ** 2)


def g_factor(lam_nm, f):
    """Photons scattered per atom per second at 1 AU."""
    return PI_RE_C * f * photon_flux_per_hz(lam_nm)


def sodium_g():
    """(g_D2, g_D1) in s^-1."""
    return g_factor(*NA_D2), g_factor(*NA_D1)
