# RealAirglow architecture (Phase 2)

How the in-game renderer is put together and why. Physics values live in `PHYSICS.md`; this
file covers engineering decisions and the evidence behind them.

## Pieces

| Path | What |
|---|---|
| `reference/` | Python reference model (source of truth for the physics). `export_ksp_config.py` writes the derived constants to `GameData/RealAirglow/Config/Earth.cfg`. |
| `src/Core/` | Unity-free C#: display controls (sliders, presets, smoothing, persistence), config reader, a double-precision CPU port of the ray integrator, the exposure equation. Compiled into the plugin and the unit tests. |
| `tests/RealAirglow.Tests/` | xunit tests: intensity controls (linear, 0 % = exactly zero, only the airglow scale) and the CPU integrator against reference-model numbers. |
| `unity/RealAirglowShaders/` | Unity 2019.4.18f1 project: `Airglow.shader`, the bundle build script, the comparison harness, and `Shared/AirglowMaterialBinder.cs` (also compiled into the plugin so both drive the shader identically). |
| `src/RealAirglow/` | The KSP plugin (net472): addon, renderer, camera hooks, settings and debug windows. |
| `tools/build_shaders.sh` | Builds the DX11 bundle and runs the shader-vs-reference comparison. |

Windows/DX11 only (owner decision 2026-10-11): one bundle, `realairglow-windows.bundle`.

## Data flow

`Earth.cfg` (layer profiles, zenith intensities, solar coefficients, colour weights per response
mode, per-channel extinction coefficients, air-density polynomial) → `ConfigReader` → per frame,
`AirglowMaterialBinder` writes the shader uniforms. A layer's shader scale is
`intensity(F10.7) × display multiplier ÷ profile norm`. The display multiplier
(`DisplayControls.Effective(group)` = master × layer, or 0 when off) multiplies the final
airglow radiance only. It never reaches the physical values or anything other than this
material.

## Cameras and compositing

Evidence from Scatterer's source (`LGhassen/Scatterer`, branch `dev`, version 0.0908, the version
installed here; read only, nothing copied):

- `Scatterer.cs` `SetupMainCameras`: cameras are "Camera ScaledSpace", "Camera 01" (far) and
  "Camera 00" (near). When "Camera 01" is absent Scatterer logs "unified camera mode" (KSP's
  D3D11 single local camera).
- `SkyNode.cs`: `inScaledSpace = !pqsController.isActive`. The scaled sky sphere and scaled
  scattering use render queues 2998/2997 (transparent pass). In local space the sky is drawn by
  a command buffer at `CameraEvent.BeforeImageEffectsOpaque` (`SkySphereLocalCommandBuffer`), and
  screen-space scattering runs at `BeforeForwardAlpha` (`ScreenSpaceScatteringContainer`).
  Scatterer sets `depthTextureMode |= Depth` on local cameras.
- Tonemapping: a global `TONEMAPPING_MODE` (0 none, 1 Bruneton, 2 Uncharted; this install uses
  2) is applied inside Scatterer's own shaders, so its output is already display-referred. The
  current 0.0908 shader sources are not in the public repository.

TUFX in this install: the flight profile is `Default-Empty` (no post-processing); map and
tracking station use `Default-Tracking` (HDR, AutoExposure, Bloom).

Decisions:

1. **One camera per frame draws the airglow.**
   - When the body's PQS is active and the map is closed, it is drawn on the earliest local
     camera ("Camera 01" if present, else "Camera 00"), using its depth texture so terrain and
     vessels occlude it.
   - Otherwise it is drawn on the scaled-space camera (map, tracking station, far from the
     body). Drawing on both would double the sky, because local cameras clear only depth.
2. **Composite events.**
   - Local camera: `AfterImageEffectsOpaque`, i.e. after Scatterer's local sky (so its
     extinction is not applied on top of ours) and before the transparent pass (so EVE's 2D
     clouds alpha-blend over the airglow).
   - Scaled camera: `AfterForwardAlpha`, i.e. after Scatterer's scaled atmosphere and EVE's
     scaled clouds. From orbit the airglow is in front of the clouds.
   - Both events can be changed live in the debug window for in-game testing. Final ordering
     with EVE volumetrics, Deferred and TUFX is Phase 4.
3. **Output encoding.** The shader applies the photographic exposure (ISS D4 frame,
   `PHYSICS.md` §12.3) and, in gamma colour space, the sRGB transfer curve, then adds with
   `Blend One One`.
   - Values are not clamped; an LDR target clamps on write, an HDR target keeps values above 1.
   - Matching Scatterer's in-shader tonemapper and TUFX's HDR chain is Phase 4.
4. **Precision.** Camera position relative to the body centre is computed in double precision on
   the CPU (`Vector3d`), from the local camera or from scaled space × `ScaledSpace.ScaleFactor`,
   then passed in km. The shader intersects spheres about the point of closest approach, which
   is stable at grazing incidence.
5. **Day/night (temporary).** Phase 2 weights emission by the fraction of the solar disc hidden by
   the planet at each sample point (solar angular radius from the IAU nominal radius and 1 au).
   Phase 3 replaces this with the agreed adaptive exposure.

## Shader vs reference (deliverable 5)

`tools/build_shaders.sh` renders three views with the real shader and binder in the Unity editor
(exposure 1, linear output) and compares them with the reference model on the same per-pixel
rays (`reference/compare_shader.py`). Tolerance on pixels brighter than 1 % of the view's
maximum: median luminance error ≤ 1 %, 95th percentile ≤ 3 %.

Results on 2026-10-11 (all pass):

| View | Median | p95 | Max |
|---|---|---|---|
| ISS limb, 200 mm, extinction on | 0.07 % | 1.27 % | 1.43 % |
| ISS wide, 24 mm, extinction on | 0.02 % | 0.22 % | 1.43 % |
| Ground, 20° elevation, 50° FOV, extinction on | 0.07 % | 0.85 % | 3.96 % (at the horizon) |

The remaining error comes from 16-point quadrature (≤ 2 % on rays crossing a layer below its
peak) and from the 11-bin extinction (`PHYSICS.md` §11).

## Performance (Phase 2, unoptimised)

Per pixel: up to 9 layers × 2 path intervals × 16 Gauss–Legendre nodes, plus 2 × 16 nodes for
the absorber columns. Phase 4 moves the horizontally uniform part into (r, μ) lookup tables and
adds quality presets.

## Known gaps after Phase 2

Scattered airglow near the ground horizon (`PHYSICS.md` §11) is not rendered yet. The colour
space is logged at startup (`[RealAirglow] … colour space …`) so the encoding assumption can be
checked.
