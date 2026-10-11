"""Formation-chemistry VER profiles used to derive layer shapes (docs/PHYSICS.md §4.9).

These are only used offline to set the width and asymmetry of the parametric profiles; the
column intensity of each layer comes from the observed climatology, not from this module.
All densities here are in cm^-3 and rates in cm^3 s^-1 or cm^6 s^-1 (the units of the
source parameterisations); inputs from ``atmosphere.msis_profile`` are converted.
"""
from __future__ import annotations

import numpy as np
from scipy.optimize import brentq

from . import atmosphere
from .constants import K_BOLTZMANN, AVOGADRO
from .profiles import fit_asym_gaussian

# --- OI 557.7 nm, Barth mechanism, McDade et al. (1986) parameterisation -------------
# Constants as given by I. McDade (1994) in NCAR glbmean mkemiss.F, consistent with
# [Lednytskyy2015] Table 2: A5 = A(557.7), A6 = A(1S) total, k1 = O+O+M, k5 = O(1S)+O2.
GREEN_A5, GREEN_A6 = 1.18, 1.35            # s^-1
GREEN_C1, GREEN_C2 = 15.0, 211.0           # C'[O2] + C''[O], dimensionless


def k_recomb_O(T):
    """O + O + M -> O2 + M, cm^6 s^-1 (McDade 1986)."""
    return 4.7e-33 * (300.0 / T) ** 2


def ver_green_mcdade(T, nO, nO2, nN2):
    """OI 557.7 nm VER, photons cm^-3 s^-1."""
    k5 = 4.0e-12 * np.exp(-865.0 / T)
    return (GREEN_A5 * k_recomb_O(T) * nO ** 3 * (nN2 + nO2)
            / ((GREEN_A6 + k5 * nO2) * (GREEN_C1 * nO2 + GREEN_C2 * nO)))


# --- O2 atmospheric (0-0) band, McDade parameterisation (same source) -----------------
def ver_o2_atm_mcdade(T, nO, nO2, nN2):
    """O2 b-X (0-0) VER, photons cm^-3 s^-1. The (0-1) band shares the upper state, so it
    has the same profile shape."""
    A1, A2 = 0.079, 0.083                    # s^-1
    k2a, k2b, k2c = 4.0e-17, 2.2e-15, 8.0e-14  # quenching by O2, N2, O; cm^3 s^-1
    Dp, Dpp = 6.6, 19.0
    return (A1 * k_recomb_O(T) * nO ** 2 * (nN2 + nO2) * nO2
            / ((A2 + k2a * nO2 + k2b * nN2 + k2c * nO) * (Dp * nO2 + Dpp * nO)))


# --- OI 630.0 nm, dissociative recombination of O2+ ----------------------------------
# Quenching of O(1D) by N2 and O2 from Streit et al. (1976) and by O, as in NCAR glbmean
# mkemiss.F; transition probabilities from NIST ASD (2023): A(630.0) = 5.63e-3 + 2.11e-5,
# A(636.4) = 1.82e-3 + 3.39e-6, A(639.2) = 8.6e-7 s^-1.
A_630 = 5.63e-3 + 2.11e-5
A_1D = A_630 + (1.82e-3 + 3.39e-6) + 8.6e-7
M_OXYGEN_KG = 15.999e-3 / AVOGADRO
G0 = 9.80665


def chapman_ne(z_km, hmF2_km, H_km):
    """Chapman-alpha electron density shape (peak 1 at hmF2)."""
    y = (np.asarray(z_km, float) - hmF2_km) / H_km
    return np.exp(0.5 * (1.0 - y - np.exp(-y)))


def ver_red_630(z_km, T, nO, nO2, nN2, hmF2_km, H_km):
    """Relative OI 630.0 nm VER (arbitrary units: n_e and the O+ + O2 rate are not modelled
    in absolute terms, so only the shape is meaningful).

    VER ∝ n_e [O2] * A630 / (A1D + kN2[N2] + kO2[O2] + kO[O]), with [O+] ≈ n_e in the F
    region and O2+ in photochemical equilibrium.
    """
    kN2 = 2.0e-11 * np.exp(107.8 / T)
    kO2 = 2.9e-11 * np.exp(67.5 / T)
    kO = 8.0e-12
    q = A_630 / (A_1D + kN2 * nN2 + kO2 * nO2 + kO * nO)
    return chapman_ne(z_km, hmF2_km, H_km) * nO2 * q


def oxygen_scale_height_km(T, z_km):
    """Scale height of atomic oxygen, k T / (m_O g(z)), km."""
    from .constants import R_EARTH
    g = G0 * (R_EARTH / (R_EARTH + z_km * 1e3)) ** 2
    return K_BOLTZMANN * T / (M_OXYGEN_KG * g) / 1e3


def derive_shapes(cond: atmosphere.MsisConditions = atmosphere.MsisConditions(),
                  red_peak_km: float = 250.0):
    """Compute chemistry VER profiles and fit asymmetric Gaussians.

    The red-line Chapman layer peak hmF2 is solved so that the 630 nm VER peaks at
    red_peak_km (the [Noll2025] reference height); its scale height is the O scale height.
    Returns dict name -> (fit, z_km, ver) plus 'hmF2_km' and 'H_km'.
    """
    z_m = np.arange(70.0, 130.0, 0.05)
    a = atmosphere.msis_profile(z_m, cond)
    cm = 1e-6
    T, nO, nO2, nN2 = a["T"], a["O"] * cm, a["O2"] * cm, a["N2"] * cm
    green = ver_green_mcdade(T, nO, nO2, nN2)
    o2 = ver_o2_atm_mcdade(T, nO, nO2, nN2)
    out = {"green": (fit_asym_gaussian(z_m, green), z_m, green),
           "o2_atm": (fit_asym_gaussian(z_m, o2), z_m, o2)}

    z_f = np.arange(120.0, 600.0, 0.25)
    b = atmosphere.msis_profile(z_f, cond)
    Tf, nOf, nO2f, nN2f = b["T"], b["O"] * cm, b["O2"] * cm, b["N2"] * cm

    def red_for(hm):
        H = oxygen_scale_height_km(np.interp(hm, z_f, Tf), hm)
        return ver_red_630(z_f, Tf, nOf, nO2f, nN2f, hm, H), H

    def peak_minus_target(hm):
        v, _ = red_for(hm)
        return z_f[np.argmax(v)] - red_peak_km

    hm = brentq(peak_minus_target, red_peak_km, red_peak_km + 200.0, xtol=0.05)
    red, H = red_for(hm)
    out["red"] = (fit_asym_gaussian(z_f, red), z_f, red)
    out["hmF2_km"], out["H_km"] = hm, H
    return out
