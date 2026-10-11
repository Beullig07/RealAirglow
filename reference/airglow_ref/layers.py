"""Default emission components (docs/PHYSICS.md §4).

A component is one emitting process with one vertical profile and one spectrum. Components
are grouped for the user-facing intensity sliders (``group``). Intensities are zenith column
emission rates in rayleighs at the reference solar radio flux of 100 sfu, without extinction.
"""
from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np

from . import data
from .profiles import AsymGaussian

F107_REF = 100.0  # sfu, PALACE reference [Noll2025]

# Relative solar cycle effect per 100 sfu by PALACE variability class, [Noll2025] Table 4.
M_SCE = {
    "OH3a": 0.116, "OH5a": 0.131, "OH7a": 0.143, "OH9a": 0.160,
    "OH3b": 0.179, "OH5b": 0.183, "OH7b": 0.189, "OH9b": 0.192,
    "OH4c": 0.191, "OH8c": 0.198, "OH6d": 0.187,
    "O2a": 0.298, "O2b": 0.399, "O2Ac": 0.395, "HO2": 0.042, "FeO": 0.086,
    "Na": 0.235, "K": -0.108, "Og": 0.754, "Or": 1.432, "Orc": 2.679, "N": 1.570, "H": 0.172,
}

# Layer shapes. Peak heights are the literature references; widths and asymmetry of the
# 557.7 nm, O2(b) and 630 nm layers come from chemistry.derive_shapes() for equinox midnight
# at 40 N, F10.7 = 100 (see docs/PHYSICS.md §4.9 for the derivation and fit errors).
PROFILE_OH = AsymGaussian(86.8, 4.3, 4.3)        # [Baker1988]: 86.8 km, FWHM 8.6 km
PROFILE_NA = AsymGaussian(92.0, 5.44, 5.44)      # [Noll2025] 92 km; Na density RMS 4.62 km [Yuan2019]
PROFILE_O2 = AsymGaussian(94.0, 4.56, 5.29)      # [Noll2025] 94 km; shape: McDade O2(b)
PROFILE_GREEN = AsymGaussian(97.0, 4.29, 4.55)   # [Noll2025] 97 km; shape: McDade 557.7 nm
PROFILE_RED = AsymGaussian(250.0, 34.49, 42.29)  # [Noll2025] 250 km; shape: O2+ recombination
PROFILE_FEO = AsymGaussian(88.0, 5.44, 5.44)     # [Noll2025] 88 km; width: tuning (Na-like)
PROFILE_NO2 = AsymGaussian(103.0, 2.5, 2.5)      # [Mende1993] 103 km; width: tuning

# Mid-latitude red-line total (630.0 + 636.4 nm) from [Leinert1998]; PALACE's 164 R is an
# EIA-adjacent site. The equatorial arcs are added in Phase 3.
RED_MIDLAT_R = 60.0 + 20.0
GREEN_F_TO_RED = 0.31  # F-region green / 630.0 nm ratio [Paulino-CISBGf], tuning


@dataclass
class Component:
    name: str
    group: str
    profile: AsymGaussian
    line_nm: np.ndarray = field(default_factory=lambda: np.zeros(0))   # vacuum wavelengths
    line_R: np.ndarray = field(default_factory=lambda: np.zeros(0))    # zenith rayleighs
    line_msce: np.ndarray = field(default_factory=lambda: np.zeros(0))  # per 100 sfu
    cont_nm: np.ndarray | None = None                                 # vacuum wavelengths
    cont_R_per_nm: np.ndarray | None = None
    cont_msce: float = 0.0
    enabled: bool = True

    @property
    def intensity_R(self) -> float:
        """Zenith column emission rate at the reference solar flux, rayleighs."""
        total = float(self.line_R.sum())
        if self.cont_nm is not None:
            total += float(np.trapezoid(self.cont_R_per_nm, self.cont_nm))
        return total

    def solar_factor_lines(self, f107: float):
        return 1.0 + self.line_msce * (f107 - F107_REF) / 100.0

    def solar_factor_cont(self, f107: float):
        return 1.0 + self.cont_msce * (f107 - F107_REF) / 100.0

    def intensity_at(self, f107: float) -> float:
        total = float((self.line_R * self.solar_factor_lines(f107)).sum())
        if self.cont_nm is not None:
            total += float(np.trapezoid(self.cont_R_per_nm, self.cont_nm)) * self.solar_factor_cont(f107)
        return total


def _lines(mask, scale=1.0):
    p = data.palace_lines()
    msce = np.array([M_SCE.get(v, 0.0) for v in p["varID"][mask]])
    return p["lam_nm"][mask], p["I_R"][mask] * scale, msce


def default_components() -> list[Component]:
    p = data.palace_lines()
    chem, h = p["chem"], p["hlayer_km"]
    comps = []

    lam, I, m = _lines(chem == "OH")
    comps.append(Component("OH Meinel", "OH", PROFILE_OH, lam, I, m))

    lam, I, m = _lines(chem == "Na")
    comps.append(Component("Na D", "Na", PROFILE_NA, lam, I, m))

    # O2 bands with the 94 km reference height (b-X and the UV systems); the a-X bands
    # (89 km, 1.27/1.58 um) are outside the visible and are not modelled.
    lam, I, m = _lines((chem == "O2") & (h == 94.0))
    cl, cc = data.palace_continuum()
    comps.append(Component("O2 atmospheric + UV bands", "O2", PROFILE_O2, lam, I, m,
                           cont_nm=cl, cont_R_per_nm=cc["O2Ac"], cont_msce=M_SCE["O2Ac"]))

    lam, I, m = _lines((chem == "O") & (h == 97.0))
    comps.append(Component("OI 557.7 mesospheric", "green", PROFILE_GREEN, lam, I, m))

    red_mask = ((chem == "O") & (h == 250.0)) | (chem == "N")
    red_palace = p["I_R"][(chem == "O") & (h == 250.0)].sum()
    lam, I, m = _lines(red_mask, scale=RED_MIDLAT_R / red_palace)
    comps.append(Component("OI 630/636 + NI 520", "red", PROFILE_RED, lam, I, m))

    green_line = p["lam_nm"][(chem == "O") & (h == 97.0)][np.argmax(p["I_R"][(chem == "O") & (h == 97.0)])]
    i630 = I[np.argmin(np.abs(lam - 630.2))]  # 630.0 nm air = 630.20 nm vacuum
    comps.append(Component("OI 557.7 F-region", "green", PROFILE_RED,
                           np.array([green_line]), np.array([GREEN_F_TO_RED * i630]),
                           np.array([M_SCE["Or"]])))

    comps.append(Component("FeO-like continuum", "continuum", PROFILE_FEO,
                           cont_nm=cl, cont_R_per_nm=cc["FeO"], cont_msce=M_SCE["FeO"]))
    # NO2: <= 5 % of the 600 nm pseudo-continuum at low latitudes [Evans2010] (upper limit).
    # Spectral shape unknown here; the FeO-like shape is used as a stand-in (tuning).
    comps.append(Component("NO2 continuum", "continuum", PROFILE_NO2,
                           cont_nm=cl, cont_R_per_nm=0.05 * cc["FeO"], cont_msce=M_SCE["FeO"]))

    # Radiative recombination O+ + e- scales with n_e^2 [Noll2025] §4.5: the square of the
    # Chapman layer from chemistry.derive_shapes (hmF2 = 305 km, H = 46 km) has half-maximum
    # points at y = -0.987 and +1.462 scale heights. Optional, off by default.
    lam, I, m = _lines((chem == "O") & (h == 300.0))
    comps.append(Component("O recombination lines", "red",
                           AsymGaussian(305.0, 0.987 * 46.0, 1.462 * 46.0),
                           lam, I * RED_MIDLAT_R / red_palace, m, enabled=False))
    return comps


GROUPS = ("green", "red", "OH", "Na", "continuum", "O2")
