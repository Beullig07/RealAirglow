# Changelog

## 1.0.0 (2026-10-11)

First public release. AI-assisted (see README). Windows/DX11 only.

### Added
- Static airglow layers for Earth from PALACE v1.0 at F10.7 = 100 sfu:
  - OI 557.7 nm (mesospheric and F-region)
  - OI 630.0/636.4 nm
  - OH Meinel bands
  - Na D
  - O₂ atmospheric band
  - visible continuum (FeO + NO₂)
- Layer shapes from formation chemistry with NRLMSIS 2.1 (`docs/PHYSICS.md` §4.9).
- Ray-integrated shader (16-point Gauss–Legendre per layer interval), giving van Rhijn
  brightening and limb enhancement from geometry. Ground view includes Rayleigh, ozone and
  aerosol extinction in 11 wavelength bins.
- Colour modes:
  - camera: Nikon D3 response with a fitted colour matrix;
  - eye: CIE 191 mesopic;
  - science: false colour per layer.
- Photographic exposure from the ISS night photo ISS047-E-1114 (Nikon D4, 1.3 s, f/1.4,
  ISO 12800).
- Metered auto-exposure (ISO 2720 averaging meter over Earth albedo and twilight sky
  brightness). Airglow fades when sunlit Earth or twilight sky is in view.
- Intensity window:
  - master 0–300 % on a log slider, with numeric entry and the EV shown;
  - one slider per layer, plus an on/off toggle;
  - presets 100 / 30 / 15 % and a reset;
  - 0.2 s smoothing;
  - settings saved in `PluginData/settings.cfg`;
  - Alt+G toggles the window.
- Debug window: colour mode, extinction, exposure mode, render-event selectors and a
  per-layer readout in rayleighs.
- Clouds: per-pixel front/back split at the 60 km lower-atmosphere shell.
  - From orbit the airglow is drawn over EVE clouds.
  - From the ground clouds hide it.
- Python reference model with tests, reference images and a shader-vs-reference comparison
  harness.
  - Every view passes: median error ≤ 0.07 %, p95 ≤ 1.3 %.

### Fixed since the first in-game test
- Night-side clouds were drawn black over the airglow from orbit.
- Hard edge at the terminator. It came from a temporary night gate, which is now a debug
  option and off by default.

### Known issues
- Not yet re-tested in game after the cloud and terminator fixes. If clouds look wrong, try the
  "Next front event" / "Next local event" buttons in the debug window.
- No variability yet: no gravity waves, solar cycle, tides or ionospheric structure.
- Scattered airglow near the ground horizon is not rendered.
- Not yet matched to Scatterer's tonemapper or TUFX's HDR chain.
- Unoptimised: every pixel integrates every layer. Lookup tables and quality presets are
  planned.
