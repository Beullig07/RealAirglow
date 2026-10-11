# In-game test checklist

## Retest for 1.0.1 (always-on airglow, new defaults)

1. **Day and night side:** with the sunlit half of Earth in view (map view or orbit), the airglow
   stays visible on the whole planet. It no longer fades out.
   - Debug… → Exposure shows "Fixed (ISS night)" selected.
2. **Defaults:** the window shows master 1.5 %, and the red line at 6667 % (final 100 %). These
   apply even with a settings file from 1.0.0, which is migrated.
3. **Red line:** from orbit at the limb, the red 630 nm glow above the green line looks as bright
   as it did at master 100 %, while the green and orange bands are faint.

## Retest after the 2026-10-11 fixes (clouds, terminator)

1. **Clouds from orbit** (night side, ~400–500 km, same view as before): cloud tops should no
   longer show as black patches through the airglow; the glow should lie evenly over them.
   - If they are still black, open Debug… and press "Next front event" until they look right.
   - Note the event name and send it.
2. **Clouds from the ground** (night, under clouds): clouds should still hide the airglow.
   - If the glow is drawn over them, press "Next local event" instead and note which one works.
3. **Terminator** (map view, the same half-lit Earth as before): the hard edge is gone.
   - With sunlit Earth in frame, the airglow should fade out completely: the camera exposes for
     daylight. It appears when you look at the night side only.
   - Debug… shows the metered luminance and the exposure as a % of the ISS night exposure.
   - "Fixed (ISS night)" brings back a constant night exposure for comparison. In Fixed mode the
     airglow also covers the day side, which is expected because it is not physical.
4. Old saved settings: `nightGate` from the previous build is ignored. The night gate is now a
   debug toggle that is off by default.

## Phase 2: static layers, intensity window

Before you start:
- KSP closed, then the build deployed (`GameData/RealAirglow` with `Plugins/RealAirglow.dll`,
  `Shaders/realairglow-windows.bundle`, `Config/Earth.cfg`, `Icons/toolbar.png`).
- After loading, `KSP.log` / `Player.log` should contain
  `[RealAirglow] 9 layers for Earth, R = … km, colour space …`. Note the colour space it
  reports (Gamma or Linear).
- Any `[RealAirglow]` error line means stop and send me the log.

Use a vessel in low Earth orbit (about 400 km) and one on the ground away from city lights. Use
time warp to put each on the night side, with the Sun at least ~20° below the horizon at the
vessel. Screenshots: F1. Send them with the debug window open where it says so.

### A. Window and controls
1. Toolbar shows the RealAirglow icon (green arc) in flight, in map view and in the Tracking
   Station. Click it: the window opens. Alt+G toggles it too.
2. Default master reads **1.5 % (−6.1 EV)**.
   - The red line slider reads 6667 % (final 100 %).
   - The other five read 100 % (final 1.5 %).
3. Drag the master slider. The airglow brightness follows smoothly, with no flicker, and the
   rest of the scene (planet, sky, stars, clouds, vessel) does not change.
4. Master 0 % or the "Airglow on" toggle off: the airglow disappears completely. Toggle back on.
5. Presets:
   - Realistic photographic: every layer final 100 %.
   - My default: master 1.5 %, red final 100 %.
   - Subtle: every layer final 15 %.
6. Each layer slider only changes its own layer. Easiest check: Debug… → Science mode, where
   each layer has its own false colour.
7. Clicking or dragging inside the window does not move the camera, stage or throttle.
8. Change some values, exit to the space centre and return (or restart KSP): the values persist.
   `GameData/RealAirglow/PluginData/settings.cfg` exists.

### B. Orbit view (night side, ~400 km, camera looking at the horizon)
1. A thin **green** band hugs the limb, a slightly wider **orange-yellow** band sits just below
   it (FeO-like continuum + OH + Na), and a faint **reddish** glow sits higher up (630 nm,
   ~250 km).
   - Compare with `reference/output/compare_iss047.png` and `iss_limb_wide_100pct.png`.
   - Screenshot at master 100 % with no UI.
2. The green band sits about 2–3° above the hard horizon from 400 km (tangent ~94–97 km).
3. Look down at the night side: a faint olive glow over the planet, brighter toward the limb.
4. Open map view (M): the same bands appear around Earth in map view, with no doubling or jump
   when switching back and forth.
5. Cross the terminator toward day: as sunlit ground comes into view the airglow fades with the
   metered exposure (no hard edge).

### C. Ground view (night, clear sky)
1. Faint olive/greenish sky glow, brighter toward the horizon (strongest ~10–15° above it),
   then dimming right at the horizon (extinction).
2. Terrain and the vessel block the glow (no airglow drawn over mountains).
3. Clouds (EVE): low clouds in front of the sky should hide the airglow behind them. If the
   airglow is drawn on top of clouds, open Debug… and step "Next local event" until it is hidden
   behind them. Tell me which event worked.

### D. Debug window readouts
Point the camera at the limb from orbit with Debug… open and screenshot the window. The
readout lists the apparent column per layer (R) at the screen centre. Expected order of
magnitude at the green layer's limb: green ~10⁴ R, continuum ~10⁵ R, OH ~10⁷ R (mostly infrared).

### E. What to send
Screenshots from B1, B4, C1, C3 and D, the reported colour space line from the log, and any
frame-rate change you notice with airglow on vs off (Alt+F12 does not show GPU time; just give
the FPS).
