# Changelog

## 1.0.4 (2026-10-12)

### Added
- macOS (Metal or OpenGL) and Linux (OpenGL) support. The release carries one shader bundle
  per OS and the plugin loads the right one. Windows also works with `-force-glcore`.

### Changed
- Faster, with identical output. In flight the airglow is drawn in two passes (in front of
  and behind the clouds), and each pass used to compute all of it and then throw half away.
  Each pass now computes only its own half. The in-front pass also skips the extinction
  columns it never uses. In a software-OpenGL test the flight view rendered about twice as
  fast, and all 288 test renders matched 1.0.3 bit for bit.
- No per-frame memory allocations in the plugin.
- If the shader cannot run on a graphics card, the log says why and the airglow is turned
  off.

## 1.0.3 (2026-10-11)

### Changed
- Sunlight outshines the airglow again, as in the first build, but without the hard edge:
  - full brightness on the night side;
  - a smooth fade through twilight over about 500 km, following measured twilight sky
    brightness (Patat et al. 2006);
  - hidden on the day side.
  It works per location, so sunlit Earth in view does not dim the night side. Toggle:
  "Outshined by sunlight" in the main window, on by default.
- The old hard-edged night gate (debug option) is removed.
- The comparison harness has a new terminator view checking the fade against the reference
  model.

## 1.0.2 (2026-10-11)

### Changed
- The red line defaults to 500 % of the master (was 6667 %). At the 1.5 % default master
  its final brightness is 7.5 %.
- A red value saved by 1.0.0 or 1.0.1 is replaced by the new default; other settings are
  kept.

## 1.0.1 (2026-10-11)

### Changed
- The airglow now shows over the whole planet, day and night side, at all times. Exposure
  defaults to Fixed (ISS night). The metered auto-exposure, which faded all of it when sunlit
  ground was in view, is still in the debug window.
- Default master intensity is 1.5 % (was 30 %).
- The red line defaults to 6667 % of the master, so at the 1.5 % default it shows at its full
  photographic (100 %) brightness.
  - Layer sliders now span 0–10000 % on a log scale and show each layer's final brightness.
- Master slider range is 0.1–300 % (log), so values near the default get more travel.
- Presets set the layer sliders too:
  - Realistic photographic: every layer 100 %.
  - My default: master 1.5 %, red at 100 %.
  - Subtle: every layer 15 %.
- Settings from 1.0.0 are migrated: their saved exposure mode and red-layer value are replaced
  by the new defaults; everything else is kept.

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
