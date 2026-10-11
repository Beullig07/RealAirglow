# Data and documentation licence

The code in this repository is MIT-licensed (see `LICENSE`). The following are licensed under
**Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0)**,
<https://creativecommons.org/licenses/by-nc-sa/4.0/>:

- `GameData/RealAirglow/Config/` (derived layer, colour and extinction coefficients)
- `reference/output/` (reference model images)
- `docs/`

These are covered by the non-commercial share-alike licence because some values are derived from
the Jiang et al. (2013) camera sensitivities, which are CC BY-NC-SA 4.0.

## Third-party sources

Only derived numbers are included in this repository. The raw tables are downloaded at run time
by the reference model and are not redistributed.

| Source | Licence | Used for |
|---|---|---|
| PALACE v1.0 model data, Noll et al. (2025) | CC BY 4.0 | Line and band intensities, solar-cycle coefficients, continuum spectrum |
| CIE 1931 2° colour-matching functions, V(λ), V′(λ) (CIE) | CC BY-SA 4.0 | Eye-mode colour weights, scotopic weights, exposure meter |
| Camera spectral sensitivities, Jiang, Liu, Gu & Süsstrunk (2013) | CC BY-NC-SA 4.0 | Camera-mode colour weights (Nikon D3) |
| NRLMSIS 2.1 (via pymsis) | NRL terms (no commercial use without contacting NRL) | Layer shapes, air density |
| TSIS-1 Hybrid Solar Reference Spectrum v2 (NASA/LASP) | Public data | Sodium g-factor, solar illuminance |
| O₃ cross-sections, Serdyuchenko et al. (2014), IUP Bremen | No licence text published | Ozone extinction |
| Rayleigh optical depth, Bodhaine et al. (1999) | Published formula | Rayleigh extinction |
| ISS047-E-1114 photo, NASA JSC Earth Science and Remote Sensing Unit | NASA media use policy (credit required) | Left half of `reference/output/compare_iss047.png` |

Full citations are in the bibliography of `docs/PHYSICS.md`.
