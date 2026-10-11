# Phase 0 calculation scripts

These scripts produce the "conf. D" numbers quoted in `docs/PHYSICS.md` §2–§3. They need
Python 3.12 with NumPy (`np.trapezoid` requires NumPy 2.x).

- `phase0_numbers.py <cie_dir>`: per-rayleigh radiance and luminance for the main lines, the
  limb/zenith ratio of Gaussian layers, and van Rhijn values. `<cie_dir>` must contain
  `CIE_sle_photopic.csv` and `CIE_sle_scotopic.csv` from https://files.cie.co.at/ (CC BY-SA 4.0).
  These files are not committed.
- `gemini_fwhm.py`: the 97 km layer FWHM that would reproduce Gemini S011's 35× limb ratio.
