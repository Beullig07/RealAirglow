"""Physical constants and unit conversions (docs/PHYSICS.md §2)."""
import numpy as np

# Exact SI defining constants (SI brochure, 2019).
H_PLANCK = 6.62607015e-34      # J s
C_LIGHT = 299_792_458.0        # m s^-1
K_BOLTZMANN = 1.380649e-23     # J K^-1
AVOGADRO = 6.02214076e23       # mol^-1

# Mean Earth radius used for layer geometry, as in PALACE [Noll2025] Eq. 3.
R_EARTH = 6371.0e3             # m

# 1 rayleigh = 1e10 photons s^-1 m^-2 column emission rate into 4 pi [Baker1976].
PHOTONS_PER_RAYLEIGH = 1.0e10  # photons s^-1 m^-2

# Luminous efficacy maxima. CIE photopic K_m = 683 lm/W; scotopic K'_m = 1700 lm/W
# (CIE 191:2010 value; often quoted as 1699 lm/W).
KM_PHOTOPIC = 683.0
KM_SCOTOPIC = 1700.0


def photon_energy(wavelength_m):
    """Photon energy E = h c / lambda in J."""
    return H_PLANCK * C_LIGHT / np.asarray(wavelength_m, dtype=float)


def rayleigh_to_photon_radiance(intensity_R):
    """Apparent radiance in photons s^-1 m^-2 sr^-1 for a column emission rate in R."""
    return np.asarray(intensity_R, dtype=float) * PHOTONS_PER_RAYLEIGH / (4.0 * np.pi)


def rayleigh_to_radiance(intensity_R, wavelength_m):
    """Apparent radiance in W m^-2 sr^-1 of a monochromatic line of intensity_R rayleighs."""
    return rayleigh_to_photon_radiance(intensity_R) * photon_energy(wavelength_m)
