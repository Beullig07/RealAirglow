"""Neutral atmosphere and lower-atmosphere extinction (docs/PHYSICS.md §4.9, §11).

* Neutral densities and temperature: NRLMSIS 2.1 via pymsis (offline use only; MSIS2 is free
  for non-commercial use).
* Rayleigh cross-section: Bodhaine et al. 1999, Eq. 29 (dry air, 360 ppm CO2).
* Ozone: Serdyuchenko et al. 2014 cross-sections on a parametric layer (tuning parameters).
* Aerosol: Angstrom power law on an exponential layer (tuning parameters).
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np

from . import data

DOBSON_UNIT = 2.686763e20  # molecules m^-2 (0.01 mm of O3 at STP)


@dataclass(frozen=True)
class MsisConditions:
    """Inputs for NRLMSIS. Defaults: equinox midnight at 40 N, F10.7 = 100 sfu, quiet Ap."""
    date: str = "2020-03-20T00:00"
    lat_deg: float = 40.0
    lon_deg: float = 0.0
    f107: float = 100.0
    f107a: float = 100.0
    ap: float = 7.0


def msis_profile(alt_km, cond: MsisConditions = MsisConditions()):
    """Return dict with T (K) and number densities N2, O2, O, Ar, total (m^-3) at alt_km."""
    import pymsis
    alt_km = np.atleast_1d(np.asarray(alt_km, float))
    out = pymsis.calculate(np.datetime64(cond.date), cond.lon_deg, cond.lat_deg, alt_km,
                           f107s=[cond.f107], f107as=[cond.f107a], aps=[[cond.ap] * 7],
                           version=2.1)
    o = out.reshape(-1, out.shape[-1])
    v = pymsis.Variable
    species = [v.N2, v.O2, v.O, v.HE, v.H, v.AR, v.N]
    total = np.nansum(np.stack([o[:, s] for s in species]), axis=0)
    return {"T": o[:, v.TEMPERATURE], "N2": o[:, v.N2], "O2": o[:, v.O2], "O": o[:, v.O],
            "Ar": o[:, v.AR], "total": total}


def rayleigh_cross_section(wavelength_nm):
    """Rayleigh scattering cross-section per molecule of dry air, m^2 [Bodhaine1999] Eq. 29.

    Accurate to better than 0.01 % from 250 to 850 nm for 360 ppm CO2.
    """
    lam_um = np.asarray(wavelength_nm, float) * 1e-3
    l2 = lam_um ** 2
    num = 1.0455996 - 341.29061 / l2 - 0.90230850 * l2
    den = 1.0 + 0.0027059889 / l2 - 85.968563 * l2
    return num / den * 1e-28 * 1e-4  # 1e-28 cm^2 -> m^2


@dataclass(frozen=True)
class LowerAtmosphere:
    """Absorber layer parameters. All are tuning parameters (docs/PHYSICS.md §11)."""
    ozone_column_DU: float = 300.0
    ozone_peak_km: float = 22.0
    ozone_fwhm_km: float = 15.0
    ozone_temperature_K: float = 223.0
    aerosol_tau_550: float = 0.05
    aerosol_angstrom: float = 1.3
    aerosol_scale_height_km: float = 1.2
    top_km: float = 60.0          # extinction above this height is neglected

    def ozone_density(self, z_km):
        """O3 number density, m^-3, Gaussian layer normalised to the column."""
        sig = self.ozone_fwhm_km / 2.354820045
        col = self.ozone_column_DU * DOBSON_UNIT
        return col / (sig * 1e3 * np.sqrt(2 * np.pi)) * np.exp(-0.5 * ((z_km - self.ozone_peak_km) / sig) ** 2)

    def aerosol_extinction_550(self, z_km):
        """Aerosol extinction coefficient at 550 nm, m^-1 (exponential layer)."""
        H = self.aerosol_scale_height_km * 1e3
        return self.aerosol_tau_550 / H * np.exp(-np.maximum(z_km, 0.0) * 1e3 / H)

    def aerosol_spectral_factor(self, wavelength_nm):
        return (np.asarray(wavelength_nm, float) / 550.0) ** (-self.aerosol_angstrom)


@dataclass
class AirColumnTable:
    """Total air number density vs altitude from MSIS, for slant-column integration."""
    z_km: np.ndarray
    n_air: np.ndarray

    @classmethod
    def build(cls, cond: MsisConditions = MsisConditions(), top_km: float = 60.0):
        z = np.linspace(0.0, top_km, 601)
        return cls(z, msis_profile(z, cond)["total"])

    def density(self, z_km):
        return np.exp(np.interp(z_km, self.z_km, np.log(self.n_air)))
