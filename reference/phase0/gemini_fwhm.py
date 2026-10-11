import numpy as np
R = 6371.0e3
def ratio(h0, fwhm):
    sig = fwhm / 2.3548200450309493; zen = sig * np.sqrt(2 * np.pi); best = 0
    s = np.linspace(-2000e3, 2000e3, 400001)
    for ht in np.arange(h0 - 3 * sig, h0 + 1 * sig, 100.0):
        z = np.sqrt((R + ht)**2 + s**2) - R
        best = max(best, np.trapezoid(np.exp(-0.5 * ((z - h0) / sig) ** 2), s))
    return best / zen
for fw in (10e3, 15e3, 20e3, 25e3, 30e3):
    print(f"FWHM {fw/1e3:.0f} km -> max limb/zenith {ratio(97e3, fw):.1f}")
