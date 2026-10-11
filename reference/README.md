# Reference model

Offline Python (NumPy) model of Earth's nightglow, used to define and check what the shader
must produce. Every value it uses is documented in `../docs/PHYSICS.md`.

## Setup

```bash
py -3.12 -m venv .venv
.venv/Scripts/python -m pip install numpy scipy matplotlib pytest astropy netCDF4 pymsis
```

Third-party data (PALACE, CIE, camera sensitivities, TSIS-1 solar spectrum, ozone
cross-sections) downloads into `data/cache/` on first use (about 90 MB) and is not committed.

## Use

```bash
.venv/Scripts/python -m pytest
.venv/Scripts/python make_images.py
```

`make_images.py` writes PNGs to `output/` (about 10 minutes at full resolution; `--quick`
renders at quarter resolution).

## Layout

- `airglow_ref/constants.py`: SI constants, rayleigh conversions.
- `airglow_ref/data.py`: cached downloads and loaders, with licences.
- `airglow_ref/atmosphere.py`: NRLMSIS 2.1 neutrals, Rayleigh/ozone/aerosol extinction.
- `airglow_ref/chemistry.py`: formation-chemistry VERs used to derive layer shapes.
- `airglow_ref/profiles.py`: parametric VER profiles (the shader's forms).
- `airglow_ref/layers.py`: the default emission components and their sources.
- `airglow_ref/geometry.py`: exact ray integration through spherical shells.
- `airglow_ref/colour.py`: CIE, camera and human-eye responses, gamut mapping.
- `airglow_ref/exposure.py`: camera equation, display multipliers, sRGB encoding.
- `airglow_ref/render.py`: pinhole and fisheye cameras, image formation.
- `airglow_ref/solar.py`: Na D resonance g-factors from the solar spectrum.
- `airglow_ref/scattering.py`: single-scattering estimate of scattered airglow (ground view).
- `phase0/`: the Phase 0 calculation scripts.
