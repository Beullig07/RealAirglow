# RealAirglow details

The full settings, how to build from source, the repository layout and the credits.

## Install

1. Download `RealAirglow-1.0.3.zip` from the
   [Releases](../../../releases) page.
2. Copy its `GameData/RealAirglow` folder into your KSP `GameData` folder.

## Settings

- Click the toolbar button (green arc) or press **Alt+G** to open the window. The button is in
  flight, map view and the Tracking Station.
- **Master intensity:** 0–300 % on a log slider, with the equivalent EV shown. 100 % is a
  physically exposed photograph, using the settings of the ISS night photo ISS047-E-1114. The
  default is **1.5 %**.
- **Layer sliders:** one per layer (green, red, OH, Na, continuum, O₂), 0–10000 %, each
  multiplying the master. The window shows the final brightness of each layer next to its
  slider. By default the red line is at 500 % (final 7.5 % at the 1.5 % master) and every
  other layer is at 100 %.
- **Presets:**
  - Realistic photographic: everything at 100 %.
  - My default: the defaults above.
  - Subtle: everything at 15 %.
- An on/off toggle.
- **Debug…** opens a second window with:
  - colour modes: camera, eye and science false colour;
  - extinction on or off;
  - exposure: fixed (the default) or auto-metered;
  - render-event selectors for cloud ordering;
  - a per-layer readout in rayleighs.

Settings are saved in `GameData/RealAirglow/PluginData/settings.cfg`.

**Day and night side.** Sunlight outshines the airglow where the Sun is up.
- On the night side the airglow is at full brightness.
- Through twilight it fades out smoothly over about 500 km, following measured twilight sky
  brightness.
- On the day side it is hidden.
- Turn off "Outshined by sunlight" in the window to show it on the whole planet.
- The exposure is fixed, so sunlit Earth in view does not dim the night side.
- Debug… also has an auto (metered) exposure that dims the whole frame, the way a real camera
  would.

## Building from source

- **Plugin:** `dotnet build -c Release` in `src/RealAirglow`. Add `-p:Deploy=true` to copy it
  into KSP. The project expects KSP at the default Steam path; override it with `-p:KSPRoot=…`.
- **Core unit tests:** `dotnet test` in `tests/RealAirglow.Tests`.
- **Shader bundle:** `bash tools/build_shaders.sh`. This needs Unity 2019.4.18f1. It builds the
  DX11 bundle, renders test views with the real shader and compares them with the reference
  model.
- **Reference model:** Python 3.12. Setup is in [`reference/README.md`](../reference/README.md).
  Third-party data tables are downloaded at run time into `reference/data/cache/` and are not
  part of this repository.

## Layout

| Path | What |
|---|---|
| `docs/PHYSICS.md` | Every physical value, its source and confidence |
| `docs/ARCHITECTURE.md` | Renderer design and camera/compositing decisions |
| `docs/TESTING.md` | In-game test checklist |
| `reference/` | Python reference model, tests, images, config exporter, shader comparison |
| `src/Core/` | Unity-free C# shared by the plugin and the tests |
| `src/RealAirglow/` | The KSP plugin |
| `unity/RealAirglowShaders/` | Unity project with the shader, bundle build and comparison harness |
| `GameData/RealAirglow/` | Config and icon; release builds add `Plugins/` and `Shaders/` |

## Licence

- **Code** (C#, HLSL, Python, scripts): MIT, see [`LICENSE`](../LICENSE).
- **Data and documentation** (`GameData/RealAirglow/Config`, `reference/output` images and
  `docs/`): CC BY-NC-SA 4.0, because some values are derived from non-commercial share-alike
  camera data. See [`LICENSE-DATA.md`](../LICENSE-DATA.md) for the sources and their licences.

## Credits

- PALACE airglow model data: Noll et al. (2025), CC BY 4.0.
- CIE 1931 colour-matching functions and V′(λ): CIE, CC BY-SA 4.0.
- Nikon D3 spectral sensitivities: Jiang et al. (2013), CC BY-NC-SA 4.0.
- NRLMSIS 2.1 (via pymsis), TSIS-1 HSRS solar spectrum (NASA/LASP), Serdyuchenko et al. (2014)
  ozone cross-sections, and Bodhaine et al. (1999) Rayleigh optical depth.
- Photo ISS047-E-1114: courtesy of the Earth Science and Remote Sensing Unit, NASA Johnson Space
  Center. It is used in `reference/output/compare_iss047.png`.

Full references are in the bibliography of [`docs/PHYSICS.md`](PHYSICS.md).
