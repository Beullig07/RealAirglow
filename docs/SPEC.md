# Spec: physically based Earth airglow mod for KSP

This is the project brief as supplied by the project owner on 2026-10-11. It is
kept verbatim so later sessions work from the same requirements. Where Phase 0
research changed a number, `docs/PHYSICS.md` is authoritative and lists the
change; this file is not edited to match.

---

## Goal

Build a Kerbal Space Program 1.12.x visual mod that renders Earth's **nightglow (airglow)** physically and photorealistically, so that:

- From low Earth orbit (~400 km) on the night side, the limb shows the thin green layer near ~97 km, with the fainter, more diffuse red layer above it near ~250 km, matching astronaut photography.
- From the ground under a dark sky, the airglow brightens toward the horizon by the correct amount (van Rhijn effect) and shows moving gravity-wave bands.
- Every number in the shader traces to a cited source. Accuracy beats "looks cool." If a value is uncertain, expose it as a config parameter with the cited range, not a guess.

You are writing real code against a real game. Work in phases, verify as you go, and do not invent physics constants, API names, or KSP/Scatterer internals. When you don't know something, look it up (primary sources, actual source code) or ask me.

## My setup

- KSP 1.12.x, heavily modded realistic install: real-scale Earth (RSS or BallisticFox's Sol planet pack, so make the target body name configurable; both use `Earth`), Principia, Parallax, Scatterer, EVE with Blackrack's volumetric clouds, possibly Deferred and TUFX.
- KSP 1.12 runs on **Unity 2019.4.18f1**. Build shaders with exactly that editor version into asset bundles for **DX11, OpenGL Core, and Metal**.
- C# plugin targeting KSP 1.12's .NET Framework (confirm the exact target from KSP's `Managed` assemblies; 4.7.2 is common).
- Scatterer's plugin source is **GPLv3** (github.com/LGhassen/Scatterer). Read it to learn its render order, camera hooks, and exposure handling. Do not copy code from it unless we agree on a GPL-compatible license first. Blackrack's volumetric clouds are closed source: do not decompile them; handle compatibility through render ordering and depth only.
- You cannot run KSP. Whenever something needs in-game verification, give me a short, precise test checklist (what to look at, from where, at what time of day), and build debug views so my screenshots tell you what's wrong.

## Physics specification (verify each item before using it)

Treat these as starting values from the literature. Your first job is to confirm or correct them against primary sources and record the result in `docs/PHYSICS.md` with citations.

### Formation mechanisms (drive the model from these, not from "glow" heuristics)
- Daytime solar UV photodissociates O₂ and ionizes the upper atmosphere; the stored chemical energy is released at night through chemiluminescence and recombination.
- **OI 557.7 nm (green line):** O(¹S) → O(¹D) + hν. Mesospheric component mainly via the two-step Barth mechanism (O + O + M → O₂*; O₂* + O → O₂ + O(¹S)). A weaker F-region component (~250 km) also exists, mostly in the tropics.
- **OH Meinel bands:** H + O₃ → OH* + O₂. Vibrationally excited OH emits from ~600 nm deep into the near-IR (most of its energy is invisible to the eye).
- **O₂ atmospheric band (b–X):** 761.9 nm (0–0) and 864.5 nm (0–1), recombination-driven like the green line.
- **Na D (589.0/589.6 nm):** nightglow via Na + O₃ → NaO + O₂; NaO + O → Na(²P) + O₂. At twilight, the sodium layer is sunlit while the ground is dark, and **resonance scattering of sunlight** makes it much brighter.
- **OI 630.0/636.4 nm (red line):** O(¹D) from dissociative recombination of O₂⁺ in the ionospheric F region. Depends on electron density and neutral O₂, so it follows ionospheric structure.
- **NO₂ continuum** (~500–650 nm) from a layer near ~103 km.

### Layers: typical night zenith values (column emission rate in Rayleighs)
| Emission | Peak altitude | Thickness | Typical zenith intensity | Notes |
|---|---|---|---|---|
| OH Meinel (all bands) | ~87 km | FWHM ~8–9 km (86.8±2.6 km, 8.6±3.1 km) | ~4.5 MR total, mostly NIR | Visible red tail only; peak height moves seasonally |
| Na D | ~92 km | ~10 km | ~30–50 R | Strong seasonal variation (mid-latitude winter max) |
| O₂ atmospheric | ~94 km (some tables list ~80 km — resolve this) | ~10 km | ~1 kR at 864.5 nm | 761.9 nm band strongly absorbed by the lower atmosphere when viewed from below |
| OI 557.7 mesospheric | ~97 km | ~10 km | ~250 R typical, swings ~40–750 R | Brightest visible nightglow line; equatorial layer weaker and higher |
| NO₂ continuum | ~103 km | thin | weak continuum | |
| OI 557.7 F-region | 250–300 km | broad | ~20 R | Tropics, during 630 nm enhancement |
| OI 630.0 / 636.4 | ~250 km (200–300) | tens of km, broad | ~60 R / ~20 R mid-latitude; >800 R in intertropical arcs | See ionospheric structure below |

Model each layer as a vertical volume emission rate profile (Gaussian or Chapman-like, slightly asymmetric for OH) whose column integral equals the zenith intensity. Use real units internally: 1 R = 10¹⁰ photons s⁻¹ m⁻² (column), i.e. radiance 10¹⁰/(4π) photons s⁻¹ m⁻² sr⁻¹; convert to W m⁻² sr⁻¹ per wavelength with E = hc/λ.

### Geometry (this is what makes it look right)
- From the ground, brightness vs zenith angle z follows the van Rhijn function for a thin shell at height h:
  `I(z)/I(0) = 1 / sqrt(1 − (R/(R+h))² sin² z)`, then reduced near the horizon by lower-atmosphere extinction. Don't hardcode this; it should fall out of integrating the 3D profile along view rays.
- From orbit, looking tangentially through a thin shell multiplies brightness by tens of times. Gemini-era photography measured roughly **35×** relative to looking up through the layer; a quick integration of a 10 km Gaussian layer at 97 km gives about **50×**. Your renderer must reproduce this ratio from the profiles, not from a fudge factor.
- Earth's limb from ISS altitude: green band low, diffuse orange-red band higher, as in NASA Earth Observatory photos. Note that some popular captions put the green layer at ~130 km; peer-reviewed measurements put it near 97 km. Use the peer-reviewed numbers.

### Spatial and temporal variability (each needs a parameter and a source)
- **Day/night:** emissions apply on the night side, gated by solar zenith angle at the layer altitude (the layer stays sunlit after ground sunset). Add sodium twilight resonance scattering as a separate term. Daytime dayglow exists but is swamped by Rayleigh-scattered sunlight; fade it physically, not with a hard cut.
- **Local time:** the 630 nm emission declines through the first hours after sunset, with tropical bursts; the intertropical arcs brighten again near 02:00 local time (midnight temperature maximum / pressure bulge). Different mesospheric emissions are correlated with time lags of ~0.5–1 h, consistent with their altitude separation.
- **Ionospheric structure (630 nm):** two intertropical arcs paralleling the **magnetic** dip equator at roughly ±15° dip latitude (equatorial ionization anomaly). Compute dip latitude from a real geomagnetic field model (IGRF or WMM) offline and bake it into a texture. Include equatorial plasma bubbles as north–south aligned, field-aligned **dark depletion bands** that drift eastward after sunset.
- **Gravity waves:** modulate the mesospheric layers with wave fields.
  - Bands: horizontal wavelength ~10–60 km, periods ~5–35 min, phase speeds mostly ~30–60 m/s (range up to ~80–100 m/s), long-lived.
  - Ripples: horizontal wavelength < ~15 km, short-lived (< ~45 min), local instabilities.
  - Occasional concentric rings from deep convection.
  - Because waves propagate vertically, OH (87 km), O₂ (94 km), and OI (97 km) should show the same pattern with phase offsets, not identical copies.
- **Tides and longitude:** OH shows strong longitudinal structure (wavenumber-4 from the DE3 nonmigrating tide) and an equatorial semiannual maximum.
- **Solar cycle:** expose an F10.7 parameter. Literature suggests roughly ~20–26% green-line variation over a cycle, ~23% for O₂, ~8% for OH; 630 nm is strongly solar dependent. Verify these.
- **Season and latitude:** green-line layer peaks around 95 km at mid-latitudes, weaker and higher at the equator; sodium has a winter maximum; OH peak height shifts seasonally.

### Color and exposure: do not eyeball colors
- Compute each line's color from its spectrum using CIE 1931 color-matching functions → XYZ → linear sRGB, with proper handling of out-of-gamut monochromatic lines. Band emissions (OH, O₂) need their actual band spectra, not a single wavelength.
- Provide three spectral response modes:
  1. **Camera (default, photorealistic):** a DSLR-like spectral sensitivity with an IR-cut filter, like astronaut photography. Research what cameras the ISS crew use and their approximate spectral response.
  2. **Human eye:** at nightglow radiance levels human vision is largely scotopic (rod-dominated) so color is barely perceived. Verify this and implement it honestly.
  3. **Science / false color:** each emission in its own channel, for debugging.
- Render in **linear HDR physical radiance**, then apply an exposure factor (configurable, with presets that mimic long-exposure night photography) before Scatterer/TUFX tonemapping. Airglow must not be pre-tonemapped or clamped.
- **Brightness:** full photographic exposure looks too bright to me. The airglow intensity must be adjustable by me in game (see "Airglow intensity controls" below), with a default of **30% of photographic brightness** (dimmed by 70%, about −1.7 EV).

## Rendering architecture (proposal; improve it if you find better)

- Planet-centered, double-precision math on the CPU; pass camera-relative positions to the GPU to avoid float precision artifacts at real scale. Handle KSP's floating origin and the **scaled space** camera (1:6000 scale) as well as the local/PQS camera, with a seamless transition.
- Per pixel: analytic ray–sphere intersection with each shell (mesospheric ~75–115 km, thermospheric ~150–400 km), then integrate emission × horizontal modulation × day/night gate along the ray, occluded by the planet and by the depth buffer in local space. Consider precomputed per-layer LUTs in (r, μ) for the vertical integral (in the spirit of Bruneton's precomputed atmospheric scattering), with ray-marching only where horizontal structure matters.
- Attenuate by the lower atmosphere (Rayleigh, ozone, aerosol transmittance) along the path when viewing from inside or below. Reuse Scatterer's transmittance if it is cleanly accessible; otherwise compute your own consistent with Earth values.
- Composite additively in HDR at the correct point in the camera pipeline (likely a CommandBuffer on the relevant cameras). Determine the correct ordering relative to Scatterer's sky/atmosphere, EVE/volumetric clouds (clouds below the observer's line of sight must occlude airglow when viewed from the ground), Deferred, and TUFX by reading their code/docs and testing.
- Performance budget: under ~1 ms at 1440p on a mid-range GPU. Use half-resolution rendering with depth-aware upsampling if needed. Provide quality presets.

## Airglow intensity controls (required, user-facing)

I want to tune how bright the airglow looks while playing, without editing files or restarting.

- **Settings window** in Flight, Map view, and Tracking Station, opened from a stock ApplicationLauncher (toolbar) button. Use stock KSP UI. Ask me before adding a dependency such as ToolbarController or ClickThroughBlocker.
- **Master intensity:** a slider from 0% to 300% with a numeric entry box. Default **30%**. Show the equivalent EV next to it (30% ≈ −1.7 EV). Use a logarithmic slider or fine steps so values below 50% are easy to set.
- **Per-layer intensity:** separate sliders, 0–300%, default 100% each, for: green line 557.7 nm (mesospheric and F-region parts together), red line 630/636 nm, OH Meinel, sodium D, NO₂ continuum. Final layer brightness = master × layer.
- **Quick actions:** an on/off toggle for all airglow, "Reset to defaults", and three presets: *Realistic photographic* (master 100%), *My default* (30%), *Subtle* (15%).
- **Live updates:** every change applies the next frame, with no scene reload. Smooth changes over ~0.2 s so dragging a slider doesn't flicker.
- **What the controls affect:** they are display multipliers applied to the final airglow radiance only, after the physical integration and before compositing. They must not change Scatterer's sky, the planet, stars, or clouds, and must not change the physical model values in rayleighs. Relative brightness between altitudes, limb-vs-zenith ratios, colors, and all variability (day/night, arcs, gravity waves, solar cycle) stay physically correct at every setting.
- **Persistence:** save the settings to `GameData/<ModName>/PluginData/settings.cfg` and load them at startup. Do not write to `.cfg` files outside `PluginData`, because ModuleManager rebuilds its cache whenever a watched config changes. Settings are global, not per save. A body's `AIRGLOW` config node may set its own default master intensity, which the saved user setting overrides.
- **Optional hotkey** (configurable, e.g. Alt+G) to open or close the window.
- Add unit tests proving the multipliers scale only airglow, linearly, and that 0% outputs exactly zero airglow.

## Configuration

- ModuleManager-patchable config: one `AIRGLOW` node per body, a `LAYER` node per emission (wavelengths or band spectrum file, peak altitude, profile shape, zenith intensity, variability parameters, source citation as a comment).
- Global settings: F10.7, exposure, spectral mode, quality, per-layer toggles. The intensity settings live in the settings window above.
- In-game debug window (separate from the settings window): false-color mode, exposure, time scrub, gravity-wave amplitude, readout of computed rayleighs and on-screen radiance at screen center.

## Deliverables

1. `docs/PHYSICS.md`: every parameter, value, range, and citation; list any disagreements between sources and which one you chose and why.
2. `reference/`: an offline Python reference model (NumPy) of the full 3D emission field and a simple renderer that produces (a) an ISS-altitude limb view and (b) a ground all-sky view.
3. Unit tests that the reference model reproduces: each layer's zenith intensity in Rayleighs; the van Rhijn curve from the ground; the limb-to-zenith enhancement (~35–50×); limb profile peaks at the right tangent altitudes when viewed from 400 km.
4. Unity 2019.4.18f1 shader project + asset bundles (DX11 / OpenGL Core / Metal).
5. C# plugin, KSP 1.12, with the config system and debug UI. The shader output must match the Python reference within a stated tolerance for the same inputs (write a comparison harness).
6. `GameData/<ModName>/` ready to drop in, README, CHANGELOG, LICENSE (ask me before choosing one).
7. An in-game test checklist plus reference photos to compare against (NASA Gateway to Astronaut Photography, Earth Observatory).

## How to work

1. **Phase 0, research.** Verify the physics spec above against primary sources, then write `docs/PHYSICS.md`. Report back with a summary of anything you changed or couldn't confirm before writing renderer code.
2. **Phase 1, reference model.** Python model, tests, and rendered limb + ground images. Show me the images.
3. **Phase 2, in-game skeleton.** Plugin + shader rendering a static, correctly scaled set of layers in both scaled and local space, with the intensity settings window working from day one so I can tune brightness while testing. Give me the test checklist.
4. **Phase 3, variability.** Day/night gating, twilight sodium, local time, magnetic-dip arcs, plasma bubbles, gravity waves, tides, solar cycle.
5. **Phase 4, integration and performance.** Scatterer, EVE/volumetric clouds, Parallax, Deferred, TUFX ordering; profiling; quality presets.

Rules:
- Never guess a KSP, Unity, or Scatterer API or render order. Read the actual source, decompile KSP's own `Assembly-CSharp.dll` only for API discovery (no redistribution), or ask me.
- Keep physical units in the code and comments. No magic numbers without a citation or a clearly labeled tuning parameter.
- Commit after each phase with a clear message.
- Ask me before any decision that is hard to undo (license, dependencies on another mod's internals, breaking config format).

## Sources to start from (verify and expand)
- Leinert et al. 1998, "The 1997 reference of diffuse night sky brightness," A&AS — Table 13 zenith brightness of nightglow emissions: https://aas.aanda.org/articles/aas/full/1998/01/ds1449/node6.html
- Noll et al. 2012, Cerro Paranal sky model (layer altitudes, mechanisms, variability classes): https://arxiv.org/abs/1205.2003
- PALACE v1.0 Paranal airglow model, 2025 (reference heights incl. F-region lines): https://arxiv.org/abs/2504.10683
- Patat 2008, "The Dancing Sky" (green line statistics, 630 nm behavior): https://arxiv.org/abs/0801.2270
- Mende et al. 1993, limb view spectrum of Earth's airglow from STS-37: https://earthref.org/ERR/33081
- OH layer height/thickness review: https://acp.copernicus.org/preprints/acp-2022-528/acp-2022-528.pdf
- Parihar et al. 2013, OH/O₂/OI emission altitudes: https://angeo.copernicus.org/articles/31/197/2013/
- Gao et al. 2012, green-line layer vs latitude (ISUAL/SABER): https://angeo.copernicus.org/articles/30/695/2012/
- Xu et al. 2010, longitudinal (tidal) structure of OH nightglow: https://impacts.ucar.edu/en/publications/strong-longitudinal-variations-in-the-oh-nightglow/
- Wiens et al. 2004, 630 nm intertropical arc and midnight brightening: https://angeo.copernicus.org/articles/22/3251/2004/
- OGO-4 global 630 nm intertropical arcs: https://ntrs.nasa.gov/api/citations/19720021779/downloads/19720021779.pdf
- Medeiros et al. 2007 and Li et al. 2011, gravity-wave statistics in airglow imagers: https://angeo.copernicus.org/articles/25/399/2007/ , https://angeo.copernicus.org/articles/29/1401/2011/
- Solar cycle effect on green line (Kiso): https://earth-planets-space.springeropen.com/articles/10.5047/eps.2011.04.006
- Gemini S011 airglow horizon photography (limb augmentation): https://ntrs.nasa.gov/citations/19720003209
- van Rhijn function (form used in astronomy): https://arxiv.org/abs/2010.05145
- NASA Earth Observatory ISS limb airglow photo (visual reference): https://earthobservatory.nasa.gov/images/154648/nighttime-over-the-eastern-pacific
- Scatterer source (GPLv3): https://github.com/LGhassen/Scatterer
- KSP 1.12 Unity version (2019.4.18f1): https://gist.github.com/gotmachine/d973adcb9ae413386291170fa346d043
- RSS scaled space scale factor (1:6000): https://github.com/NathanKell/RealSolarSystem/wiki/Scaled-Space
