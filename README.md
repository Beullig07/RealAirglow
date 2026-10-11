# RealAirglow

A Kerbal Space Program 1.12 visual mod that renders Earth's nightglow (airglow) from cited
physics. It draws the thin green 557.7 nm layer near 97 km, the red 630 nm layer near 250 km,
and the OH, Na, O₂ and continuum emission. The layers look right from orbit (bright limb bands)
and from the ground (van Rhijn brightening toward the horizon, dimmed by extinction).

> **AI-assisted plugin.** This mod was written with an AI coding assistant (Claude Code by
> Anthropic) under the author's direction. That covers the physics research, the reference
> model, the shader, the plugin and the docs. Every physical number traces to a cited source in
> [`docs/PHYSICS.md`](docs/PHYSICS.md). The shader is checked automatically against an
> independent Python reference model. The author tested it in their own KSP install.

**Status: 1.0.0.** The static layers, the intensity window and metered
exposure work. Variability (gravity waves, solar cycle, tides) and further integration with
Scatterer, EVE and TUFX are not done yet. See [`CHANGELOG.md`](CHANGELOG.md).

## Requirements

- KSP 1.12.x on **Windows with DirectX 11**. macOS, Linux and OpenGL are not supported.
- A real-Earth planet pack with a body named `Earth` (Real Solar System or Sol). Stock Kerbin
  gets no airglow.
- No other mods are needed.
- Tested with Sol, Scatterer 0.0908 and EVE 3.2.2 (volumetric clouds), TUFX and Parallax.

## Install

1. Download `RealAirglow-1.0.0.zip` from the
   [Releases](../../releases) page.
2. Copy its `GameData/RealAirglow` folder into your KSP `GameData` folder.

## Use

- Click the toolbar button (green arc) or press **Alt+G** to open the window. The button is in
  flight, map view and the Tracking Station.
- **Master intensity:** 0–300 %, with the equivalent EV shown. 100 % is a physically exposed
  photograph, using the settings of the ISS night photo ISS047-E-1114.
- **Presets:** Realistic photographic (100 %), My default (30 %, the default) and Subtle (15 %).
- **Layer sliders:** one per layer (green, red, OH, Na, continuum, O₂), plus an on/off toggle.
- **Debug…** opens a second window with:
  - colour modes: camera, eye and science false colour;
  - extinction on or off;
  - exposure: auto (metered) or fixed ISS night;
  - render-event selectors for cloud ordering;
  - a per-layer readout in rayleighs.

Settings are saved in `GameData/RealAirglow/PluginData/settings.cfg`.

**Daylight.** The airglow is never cut at the terminator. Instead the exposure follows what a
camera's light meter would choose. With sunlit Earth or twilight sky in view, the airglow fades
out, just as it does in real photos. On the night side you get the full ISS night exposure.

## Building from source

- **Plugin:** `dotnet build -c Release` in `src/RealAirglow`. Add `-p:Deploy=true` to copy it
  into KSP. The project expects KSP at the default Steam path; override it with `-p:KSPRoot=…`.
- **Core unit tests:** `dotnet test` in `tests/RealAirglow.Tests`.
- **Shader bundle:** `bash tools/build_shaders.sh`. This needs Unity 2019.4.18f1. It builds the
  DX11 bundle, renders test views with the real shader and compares them with the reference
  model.
- **Reference model:** Python 3.12. Setup is in [`reference/README.md`](reference/README.md).
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

- **Code** (C#, HLSL, Python, scripts): MIT, see [`LICENSE`](LICENSE).
- **Data and documentation** (`GameData/RealAirglow/Config`, `reference/output` images and
  `docs/`): CC BY-NC-SA 4.0, because some values are derived from non-commercial share-alike
  camera data. See [`LICENSE-DATA.md`](LICENSE-DATA.md) for the sources and their licences.

## Credits

- PALACE airglow model data: Noll et al. (2025), CC BY 4.0.
- CIE 1931 colour-matching functions and V′(λ): CIE, CC BY-SA 4.0.
- Nikon D3 spectral sensitivities: Jiang et al. (2013), CC BY-NC-SA 4.0.
- NRLMSIS 2.1 (via pymsis), TSIS-1 HSRS solar spectrum (NASA/LASP), Serdyuchenko et al. (2014)
  ozone cross-sections, and Bodhaine et al. (1999) Rayleigh optical depth.
- Photo ISS047-E-1114: courtesy of the Earth Science and Remote Sensing Unit, NASA Johnson Space
  Center. It is used in `reference/output/compare_iss047.png`.

Full references are in the bibliography of [`docs/PHYSICS.md`](docs/PHYSICS.md).
