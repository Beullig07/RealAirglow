"""Phase-0 sanity numbers for docs/PHYSICS.md (scratch, not part of the mod)."""
import sys, numpy as np
cie = sys.argv[1]
h = 6.62607015e-34; c = 2.99792458e8
def load(fn):
    d = np.loadtxt(f"{cie}/{fn}", delimiter=",")
    return d[:, 0], d[:, 1:]
lp, Vp = load("CIE_sle_photopic.csv"); ls, Vs = load("CIE_sle_scotopic.csv")
V  = lambda l: np.interp(l, lp, Vp[:, 0])
Vs_ = lambda l: np.interp(l, ls, Vs[:, 0])
# 1 R = 1e10 photons s^-1 m^-2 (column, 4pi) -> radiance 1e10/(4 pi) photons s^-1 m^-2 sr^-1
for name, lam_nm in [("OI 557.7", 557.7), ("Na D2 589.0", 588.995), ("OI 630.0", 630.0), ("OI 636.4", 636.4)]:
    lam = lam_nm * 1e-9 ; E = h * c / lam
    L_W = 1e10 / (4 * np.pi) * E               # W m^-2 sr^-1 per R
    Lv  = 683.0 * V(lam_nm) * L_W              # photopic cd m^-2 per R  (Km = 683 lm/W)
    Lvs = 1700.0 * Vs_(lam_nm) * L_W           # scotopic cd m^-2 per R  (K'm = 1700 lm/W)
    print(f"{name:12s} E={E:.4e} J  L={L_W:.4e} W/m2/sr/R  V={V(lam_nm):.4f} V'={Vs_(lam_nm):.4f}  "
          f"Lv={Lv:.3e} cd/m2/R  Lv'={Lvs:.3e} scot.cd/m2/R")

# Limb/zenith ratio for a Gaussian VER layer, spherical geometry, no extinction
R = 6371.0e3
def ratio(h0, fwhm, hobs=420e3):
    sig = fwhm / 2.3548200450309493
    zen = sig * np.sqrt(2 * np.pi)      # column per unit eps0
    best = (0, None)
    for ht in np.arange(h0 - 3 * sig, h0 + 3 * sig, 50.0):
        rt = R + ht
        s = np.linspace(-1500e3, 1500e3, 600001)    # path half-length 1500 km either side of tangent
        z = np.sqrt(rt**2 + s**2) - R
        col = np.trapezoid(np.exp(-0.5 * ((z - h0) / sig) ** 2), s)
        if col > best[0]: best = (col, ht)
    return best[0] / zen, best[1]
for h0, fw in [(97e3, 10e3), (97e3, 18e3), (87e3, 8.6e3), (92e3, 10.9e3), (250e3, 100e3)]:
    r, ht = ratio(h0, fw)
    an = 1.2162706 * np.sqrt((R + h0) / (fw / 2.35482))
    print(f"layer h0={h0/1e3:.0f} km FWHM={fw/1e3:.1f} km: max limb/zenith = {r:.1f} at tangent {ht/1e3:.1f} km (analytic @peak {an:.1f})")

def vrh(z_deg, hkm, Rkm=6371.0):
    s = np.sin(np.radians(z_deg)); return 1 / np.sqrt(1 - (Rkm / (Rkm + hkm)) ** 2 * s ** 2)
for zd in (0, 30, 60, 70, 80, 85, 90):
    print(f"van Rhijn z={zd:2d}: h=87 {vrh(zd,87):.3f}  h=97 {vrh(zd,97):.3f}  h=250 {vrh(zd,250):.3f}")
