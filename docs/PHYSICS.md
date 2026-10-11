# RealAirglow physics reference

Status: **Phase 1 (reference model)**, 2026-10-11. This file is the single source of truth for
every physical number the reference model and the shader use. Each value carries a
source key (see [Bibliography](#bibliography)). When the code needs a value that is
not here, add it here first, with a citation or a "tuning parameter" label.

How to read the confidence column:

- **V**: verified in the primary source (text or table read directly).
- **V-fig**: read off a figure in the primary source, approximate.
- **S**: taken from an abstract or a secondary summary of the primary source. Good
  enough for a default, re-check before relying on it for a test.
- **D**: derived by us from verified inputs (calculation shown or scripted).
- **Open**: not confirmed. The model exposes it as a parameter and documents the range.

## 1. Changes against the brief (`docs/SPEC.md`)

These are the places where Phase 0 research disagrees with the starting values in the
brief. Everything not listed here was confirmed (see later sections).

| # | Brief said | Research says | Action |
|---|---|---|---|
| 1 | O₂ atmospheric band at ~94 km, "some tables list ~80 km" | 94 km for b–X ([Yee1997] via [Noll2025]; [Noll2012]; [Parihar2013]). The 80 km figure is the old [Leinert1998] Table 13 value. | Use **94 km**. |
| 2 | O₂ 864.5 nm (0–1) ≈ 1 kR | **340 R** at 100 sfu [Noll2025] (ESO sky model 320 R at 129 sfu [Noll2012]). 1 kR is [Leinert1998]. The (0–0) band at 762 nm is **6.5 kR** above the atmosphere [Noll2025], but almost fully self-absorbed from the ground. | Use PALACE values. The 762 nm band matters for views from orbit only (and mostly falls outside a camera's IR-cut). |
| 3 | "NO₂ continuum ~500–650 nm near ~103 km" | The visible pseudo-continuum is dominated by **FeO-like** emission (Fe + O₃ → FeO*), centroid **88 km**, 2.8 kR integrated over 0.3–2 µm [Noll2025]. NO + O → NO₂* is a minor part, ≤5 % of the 600 nm continuum at low latitudes [Evans2010, S]. A separate NO₂-attributed green continuum at **103 ± 2 km** was seen in limb view from STS-37 [Mende1993, S]. | Model **two** continuum components (FeO-like at 88 km, NO₂ at 103 km). The UI slider becomes "Continuum (FeO + NO₂)". |
| 4 | Solar cycle: green ~20–26 %, O₂ ~23 %, OH ~8 % "over a cycle" | Those three numbers are a **model simulation** for 1980–1991 [Huang2016], not observations. Observed effects are larger and site-dependent: green **+75 % per 100 sfu** at Paranal [Noll2025], +40–60 % per 100 sfu at low latitudes ([Zhu2015] via [Noll2025]), ±20 % amplitude at Kiso 35.8°N [Das2011]; O₂(b) +40 %; OH +12–20 %; Na +24 %; red line **+143 %** [Noll2025]. | Per-layer solar coefficient, default [Noll2025], documented range. See §8. |
| 5 | Green line ~250 R typical | Confirmed as "typical" ([Leinert1998]; [Patat2008]: 40–750 R around a 230 R mean). The modern climatology gives **163 R at 100 sfu** [Noll2025]; with its solar coefficient this becomes ~225 R at 150 sfu, matching Patat's sample (mean ~150 sfu). The two agree once normalised. | Default 163 R at 100 sfu × solar factor. |
| 6 | OH ~4.5 MR total | 4.5 MR is [Leinert1998] and includes the Δv = 1 bands out to 4.5 µm. PALACE has **715 kR** for 0.3–2.5 µm and **3.1 kR** between 642 and 858 nm [Noll2025]. Only the visible tail matters here. | Use the PALACE OH line list for the band spectrum. |
| 7 | Limb/zenith ≈ 35× (Gemini) or ≈ 50× (10 km Gaussian at 97 km) | Gemini S011: "augmented by a factor of approximately 35" confirmed [Koomen1971]. Our integration of a 10 km FWHM Gaussian at 97 km gives **47.5×** for a tangent ray through the peak, and a **maximum of 56.4×** at a tangent height of **93.8 km**. The limb brightness peaks about 3 km *below* the emission peak. Reaching 35× geometrically would need a ~26 km FWHM layer (implausible), so Gemini's figure is most likely reduced by film resolution and blur, and by its filter also passing Na 589 nm. | Tests use the profile-derived ratio (56× for the 10 km test layer) and expect the limb peak below the VER peak. 35× is kept as an observational lower bound. See §3.2. |
| 8 | F-region 557.7 ≈ 20 R in the tropics | Not confirmed as a number. The F-region green/red ratio was measured as **0.31 ± 0.08** at 7.4°S, using plasma-bubble depletions to separate the layers (conference abstract, [Paulino-CISBGf, S]). Laboratory O(¹S) yields from O₂⁺ recombination are smaller, ≤0.06 against 1.17 for O(¹D) [Petrignani2005, S]. | F-region green = ratio × red-line VER. Ratio is a parameter, default 0.31, range 0.05–0.4. **Open.** |
| 9 | Na D ~30–50 R | Confirmed: 36.5 R Paranal annual mean [Noll2025]; 43 R ESO model [Noll2012]; 30 R summer to 100 R winter at mid-latitudes [Leinert1998]. D2/D1 = 1.73 [Noll2025] (1.64 ± 0.08 [Patat2008]; 1.2–1.8 [Slanger2005 via Patat2008]). | Keep. D2/D1 = 1.73, not the statistical 2.0. |
| 10 | Popular captions put green at ~130 km | Confirmed as a caption error: the Earth Observatory caption for ISS072-E-896652 says the green layer comes from "excited oxygen molecules (O₂) at about 130 km". The green line is atomic O(¹S); O₂* is only the Barth intermediate. Peer-reviewed peak heights are 92–97 km. A Pettit post also gives 120 km for green and 400 km for red. | Use peer-reviewed numbers. |

Also confirmed without change: OH 86.8 ± 2.6 km and 8.6 ± 3.1 km [Baker1988 via Wust2022]; the
van Rhijn form; 630 nm decline after sunset with a ~02:00 LT brightening; 0.5–1 h lags between
mesospheric emissions; gravity-wave band statistics; EIA arcs near ±15° dip; OH wavenumber-4.
Sections below give the detail.

## 2. Units and conversions

| Quantity | Value | Conf. | Source |
|---|---|---|---|
| 1 rayleigh (R) | 10¹⁰ photons s⁻¹ m⁻² column emission rate (emitted into 4π) | V | [Baker1976]; [Noll2025] Table 1 notes |
| Apparent radiance of 1 R | 10¹⁰/(4π) = 7.958 × 10⁸ photons s⁻¹ m⁻² sr⁻¹ | D | from the definition |
| Photon energy | E = hc/λ, h = 6.62607015 × 10⁻³⁴ J s, c = 299 792 458 m s⁻¹ (exact SI) | V | SI 2019 |
| Earth mean radius (layer geometry) | 6371 km | V | [Noll2025] (van Rhijn); [Leinert1998] and [Gill2020] use 6378 km |

Per-line radiance and luminance of a 1 R source, computed by `reference/phase0/phase0_numbers.py` with the CIE
photopic V(λ) (K_m = 683 lm W⁻¹) and scotopic V′(λ) (K′_m = 1700 lm W⁻¹) tables [CIE-V]:

| Line | λ (nm, air) | W m⁻² sr⁻¹ per R | V(λ) | V′(λ) | Photopic cd m⁻² per R | Scotopic cd m⁻² per R |
|---|---|---|---|---|---|---|
| OI green | 557.7 | 2.834 × 10⁻¹⁰ | 0.9986 | 0.3613 | 1.93 × 10⁻⁷ | 1.74 × 10⁻⁷ |
| Na D2 | 589.0 | 2.684 × 10⁻¹⁰ | 0.7692 | 0.0699 | 1.41 × 10⁻⁷ | 3.19 × 10⁻⁸ |
| OI red | 630.0 | 2.509 × 10⁻¹⁰ | 0.2650 | 0.0033 | 4.54 × 10⁻⁸ | 1.42 × 10⁻⁹ |
| OI red | 636.4 | 2.484 × 10⁻¹⁰ | 0.2047 | 0.0020 | 3.47 × 10⁻⁸ | 8.44 × 10⁻¹⁰ |

(Conf. D. V(λ) is tabulated against vacuum wavelength, and the 0.17 nm air/vacuum difference
changes these values by < 0.1 %.)

## 3. Geometry

### 3.1 van Rhijn function (ground view, no extinction)

`I(z)/I(0) = [1 − (R/(R+h))² sin² z]^(−1/2)`. Conf. **V**: [Gill2020] Eq. 3 (R = 6378 km);
[Noll2012] Eq. 21; [Noll2025] Eq. 3 (R = 6371 km). [Leinert1998] §6.2 adds that, with
extinction, the observed maximum enhancement is only "about a factor of four" near z ≈ 80°,
falling off toward the horizon.

Expected values for a thin shell (R = 6371 km), conf. D. The reference model must reproduce
these by ray integration, not by evaluating the formula:

| z | h = 87 km | h = 97 km | h = 250 km |
|---|---|---|---|
| 30° | 1.150 | 1.149 | 1.141 |
| 60° | 1.924 | 1.916 | 1.809 |
| 70° | 2.667 | 2.642 | 2.341 |
| 80° | 4.222 | 4.116 | 3.131 |
| 85° | 5.411 | 5.189 | 3.511 |
| 90° | 6.113 | 5.796 | 3.674 |

A finite-thickness layer gives slightly lower values near the horizon than the thin-shell
formula. The tests will state the tolerance against both.

### 3.2 Limb enhancement (orbital view)

- Gemini S011: tangential viewing augments airglow brightness "by a factor of approximately
  35". Photographs through a filter passing 5577 Å and 5893 Å [Koomen1971]. Conf. V (abstract).
- Our integration (conf. D, `reference/phase0/phase0_numbers.py`, `reference/phase0/gemini_fwhm.py`, spherical geometry, no
  extinction, Gaussian volume emission rate, VER):

| Layer | Max limb / zenith | Tangent height of max | Ratio for tangent through the peak |
|---|---|---|---|
| 97 km, FWHM 10 km | 56.4 | 93.8 km | 47.5 |
| 97 km, FWHM 18 km | 42.0 | 91.2 km | 35.4 |
| 87 km, FWHM 8.6 km (OH) | 60.7 | 84.2 km | 51.1 |
| 92 km, FWHM 10.9 km (Na) | 54.0 | 88.5 km | 45.4 |
| 250 km, FWHM 100 km (red, placeholder width) | 18.0 | 217.7 km | 15.2 |

  Analytic check for the tangent-through-peak case:
  `ratio = 2 Γ(5/4) 8^{1/4} / √(2π) · √((R+h)/σ) = 1.2163 √((R+h)/σ)`.
- Consequences: (a) the limb profile peaks about 0.75 σ below the VER peak, so "limb peaks at
  the right tangent altitude" tests must expect that offset, not the VER peak. (b) The
  limb/zenith ratio constrains layer width. Gemini's 35× would need a 26 km FWHM green layer,
  which is far thicker than any measured profile. We treat 35× as a lower bound reduced by
  instrument resolution, not as a width constraint.

## 4. Emission layers

Reference intensities are zenith column emission rates, nocturnal annual means, for a
reference solar radio flux S₀ = 100 sfu, from PALACE [Noll2025]. PALACE is a climatology for
Cerro Paranal (24.6°S, 70.4°W, about 15° south of the magnetic equator, near the southern
EIA crest). It is the most complete modern, internally consistent dataset, but it is a single
low-latitude site. That matters most for the red line (§4.7).

### 4.1 OH Meinel bands

| Parameter | Value | Range / notes | Conf. | Source |
|---|---|---|---|---|
| Mechanism | H + O₃ → OH*(v ≤ 9) + O₂ (Bates–Nicolet). Populations are then redistributed by radiative and collisional cascades. | | V | [Noll2025] §4.1 |
| Peak height | 86.8 km | ± 2.6 km (34 rocket flights). Effective height depends on v and N: line-dependent effective heights of 86–94 km. Peak altitude 84–90 km, mean 87 km (SABER, midnight) | V | [Baker1988] via [Wust2022]; [Noll2025] §6.1; [Xu2010] ¶11 |
| Thickness | FWHM 8.6 km | ± 3.1 km | V | [Baker1988] via [Wust2022] |
| Shape | Asymmetric: wider top, narrower bottom | asymmetric Gaussian. No quantitative asymmetry found, so the model uses a symmetric profile (asymmetry parameter 1.0, **Open**). | V (qual.) | [Wust2022] (Moreels 1977; Khomich 2008) |
| Height vs vibrational level | adjacent v differ by ~0.4–0.7 km | up to ~4 km between OH(3-1) and OH(8-3) | V / S | [Wust2022]; von Savigny 2012 (S) |
| Total emission | 715 kR (0.3–2.5 µm, no extinction) | 4.5 MR all bands incl. Δv = 1 [Leinert1998] | V | [Noll2025] §4.1 |
| Visible-relevant | 3.1 kR between 642 and 858 nm. Bands with Δv = 6 near 520–630 nm are much weaker, e.g. (9-3) and (8-2). | | V | [Noll2025] §4.1 |
| Spectrum | PALACE line list (HITRAN2020-based, 22 058 OH lines) | | V | [Noll2025]; data CC BY 4.0 [PALACE-data] |

### 4.2 Sodium D

| Parameter | Value | Range / notes | Conf. | Source |
|---|---|---|---|---|
| Mechanism | Na + O₃ → NaO + O₂; NaO(*) + O → Na(²P) + O₂ | Chapman 1939; deactivated-NaO path (Slanger 2005) | V | [Noll2025] §4.3 |
| Emission height | 92 km | Limb peak "near 92 km" from STS-37 [Mende1993] | V | [Noll2025]; [Noll2012]; [Mende1993, S] |
| Layer width | Na **density** RMS width 4.62 ± 0.56 km (FWHM ≈ 10.9 km), centroid 91.3 ± 1.0 km | 28-yr Fort Collins/Logan lidar | S | [Yuan2019, S] |
| Intensity | D1 13.4 R + D2 23.1 R = 36.5 R | ESO model 43 R; 40 R (Unterguggenberger 2017); 30 R (summer) to 100 R (winter) mid-latitude | V | [Noll2025]; [Leinert1998] |
| D2/D1 | 1.73 | 1.64 ± 0.08 [Patat2008]; 1.2–1.8 (Slanger 2005) | V | [Noll2025] |
| Seasonal | Mid-latitude winter maximum. Column abundance max Nov ≈ 6.0 × 10¹³ m⁻², min June ≈ 1.7 × 10¹³ m⁻² (Fort Collins). Semiannual at Paranal (Apr/May max). | ×3.3 (Leinert), ×3.5 (column) | V / S | [Leinert1998]; [She2000 via Yuan2019, S]; [Noll2012] Table 2 |
| Column abundance | 3.92 ± 2.14 × 10¹³ m⁻² (28-yr mean) | | S | [Yuan2019, S] |

**Twilight resonance scattering** (separate term): I_tw = g · N_sunlit, where N_sunlit is the
Na column along the line of sight that lies above the Earth's shadow (plus a screening
height), and g is the resonance-scattering g-factor (photons s⁻¹ atom⁻¹) for D2 and D1. g
depends strongly on the residual intensity in the cores of the solar Fraunhofer D lines, so
it must be computed from a high-resolution solar spectrum.

Computed in Phase 1 (`airglow_ref/solar.py`, conf. D): g = π r_e c · f · Φ_ν, with Φ_ν the
solar photon flux per Hz at the rest wavelength from TSIS-1 HSRS v2 [TSIS-HSRS] (0.001 nm
sampling; residual core intensity 4.7 % at D2, 5.5 % at D1). NIST ASD: D2 589.1583 nm
(vacuum) f = 0.641; D1 589.7558 nm f = 0.320.

| Line | g at 1 AU (s⁻¹) |
|---|---|
| D2 | 0.507 |
| D1 | 0.299 |
| Sum | 0.807 |

A fully sunlit mean column (3.92 × 10¹³ m⁻² [Yuan2019]) then gives **3.2 kR** at zenith,
about 85× the Na nightglow, with D2/D1 = 1.69. This agrees with the ~0.75 s⁻¹ implied by
an old dayglow measurement. Not modelled yet: the ±3.3 % annual change in solar distance,
the Doppler shift from Earth's radial velocity (±0.5 km s⁻¹, about ±1 pm, inside the flat
line core) and the D2 polarisation phase function. The day/night machinery that uses the
term is Phase 3.

### 4.3 O₂ atmospheric band (b¹Σg⁺ – X³Σg⁻)

| Parameter | Value | Range / notes | Conf. | Source |
|---|---|---|---|---|
| Mechanism | O + O + M → O₂* + M; collisional transfer to b¹Σg⁺ | Kalogerakis 2019 suggests an additional O(¹D) path | V | [Noll2025] §4.2 |
| Peak height | 94 km | [Yee1997]. OSIRIS 762 nm nightglow peak near 95 km. 89 km for a–X (1.27 µm, irrelevant here) | V / S | [Noll2025]; [Parihar2013]; Wang 2021 (S) |
| Width | FWHM 9.85 km (HWHM 4.56 km below, 5.29 km above) | 8.1–10.9 km across season and latitude | D | §4.9 |
| (0–0) 762 nm | 6.5 kR without self-absorption | Strongly absorbed by O₂ below; invisible from the ground | V | [Noll2025] |
| (0–1) 864.5 nm | 340 R | ESO model 320 R (129 sfu); [Leinert1998] 1 kR | V | [Noll2025] |
| Spectrum | PALACE line list | | V | [PALACE-data] |

Both bands lie at or beyond a camera's IR-cut edge, and V(λ) is ~0 there. The O₂ layer will be
nearly invisible in camera and eye modes. It is kept for science mode and for the phase-offset
structure of gravity waves.

### 4.4 OI 557.7 nm, mesospheric

| Parameter | Value | Range / notes | Conf. | Source |
|---|---|---|---|---|
| Mechanism | Barth two-step: O + O + M → O₂* + M; O₂* + O → O₂ + O(¹S); O(¹S) → O(¹D) + hν(557.7 nm) | | V | [Noll2025] §4.5; [Das2011] |
| VER parameterisation (used) | McDade et al. 1986: V = A₅ k₁[O]³([N₂]+[O₂]) / ((A₆ + k₅[O₂])(C′[O₂] + C″[O])), with A₅ = 1.18 s⁻¹, A₆ = 1.35 s⁻¹, k₁ = 4.7 × 10⁻³³ (300/T)² cm⁶ s⁻¹, k₅ = 4.0 × 10⁻¹² exp(−865/T) cm³ s⁻¹, C′ = 15, C″ = 211 | McDade's constants as given in NCAR glbmean `mkemiss.F` [NCAR-glbmean]. They agree with [Lednytskyy2015] Table 2, which says McDade 1986 used A₅₅₈ = 1.18 and A(¹S) = 1.35 s⁻¹, with C values in its ranges. | V | [NCAR-glbmean]; [Lednytskyy2015] |
| Extended form (not used) | [Lednytskyy2015] Eq. 15 adds O(¹S) quenching by O and N₂ and a third fit constant C(0) = 13. As extracted, the C(0) term is dimensionally ambiguous, so it is not used. NIST A values: A(557.7) = 1.26 s⁻¹, A(¹S) total = 1.336 s⁻¹. | | V | [Lednytskyy2015]; NIST ASD |
| Peak height | 97 km (reference) | 95 km mid-latitudes (Nov 2008); ~92 km at 10–20°N (May 2008); 92–96 km; long-term range 84–104 km. Equatorial layer weaker and higher than mid-latitude. | V | [Noll2025]; [Noll2012]; [Gao2012] |
| Width | FWHM 8.84 km (HWHM 4.29 km below, 4.55 km above) | 7.0–10.3 km across season and latitude | D | §4.9 |
| Intensity | 163 R at 100 sfu | Typical 250 R; 40–750 R around a 230 R mean (Paranal, FORS1); 153 R ESO model at 100 sfu | V | [Noll2025]; [Leinert1998]; [Patat2008] |
| Contribution to V-band sky | ~20 % | | V | [Patat2008] |

### 4.5 Visible pseudo-continuum

| Component | Height | Intensity | Conf. | Source |
|---|---|---|---|---|
| FeO-like (FeO "orange arc" bands + other species, possibly NiO or OFeOH) | centroid 88 km (WACCM) | 2.8 kR integrated 0.3–2.0 µm. Spectral shape from the PALACE continuum file. | V | [Noll2025] §5.2 |
| NO₂ air afterglow (NO + O → NO₂ + hν) | 103 ± 2 km at low latitude (STS-37). Satellites usually report 90–95 km, rockets 98–110 km. | ≤ 5 % of the 600 nm pseudo-continuum at low latitudes | S | [Mende1993, S]; [Evans2010, S] |
| Older aggregate values | "90 km" | 0.3 R Å⁻¹ = 3 R nm⁻¹ (400–700 nm) [Leinert1998]; 4.27 R nm⁻¹ at 543 nm with tabulated spectral shape [Noll2012] Table 3 | V | |

Model choices (tuning parameters, conf. D): the FeO-like layer uses the Na layer width
(HWHM 5.44 km) because both come from metal-atom chemistry; no FeO width was found. The NO₂
layer uses HWHM 2.5 km and, lacking a spectrum, the FeO-like spectral shape, at the 5 %
upper limit.

Of the PALACE continuum file's 2.78 kR, 1.03 kR falls between 380 and 780 nm, peaking at
597 nm. Its luminance is 2.25 × 10⁻⁸ cd m⁻² per rayleigh of component, i.e. 6.2 × 10⁻⁵ cd m⁻²
at zenith. That is about **2.6× the green line**, so the continuum is the brightest visible
component in photopic terms. It sits ~9 km below the green layer, which is why the
renderings show an orange-yellow band under the green one. This matches the yellowish lower
fringe often seen in ISS photographs, but is a model result to check against photos.

### 4.6 OI 557.7 nm, F region

| Parameter | Value | Conf. | Source |
|---|---|---|---|
| Mechanism | O₂⁺ + e⁻ → O + O(¹S), same chain as the red line (§4.7) | V | [Noll2025] §4.5; [Das2011] intro |
| Strength | "usually much weaker" than the mesospheric part. Can exceed it at the equator after midnight at high solar activity (Rajesh 2007, via [Das2011]). | V / S | |
| Green/red ratio in the F region | 0.31 ± 0.08 (7.4°S) | S | [Paulino-CISBGf] |
| Profile | Same as red-line VER (shares the O₂⁺ source; O(¹S) is not quenched at these heights) | D | assumption, flagged |

### 4.7 OI 630.0 / 636.4 nm (red line)

| Parameter | Value | Range / notes | Conf. | Source |
|---|---|---|---|---|
| Mechanism | O⁺ + O₂ → O₂⁺ + O; O₂⁺ + e⁻ → O + O(¹D). O(¹D) lifetime ~110 s, so the line is quenched in the mesopause and confined to the thermosphere. | | V | [Noll2025] §4.5; [Wiens2004] |
| Peak height | ~250 km | 200–300 km; 270 km [Noll2012]; 280 km [Wiens2004]; ISUAL limb 220–280 km (S) | V | [Noll2025] |
| Width | FWHM 76.8 km (HWHM 34.5 km below, 42.3 km above) | 72.6–77 km across season and latitude | D | §4.9 |
| Intensity, mid-latitude typical | 630.0: 60 R; 636.4: 20 R | | V | [Leinert1998] |
| Model default | 80 R for 630.0 + 636.4 (+ N I 520 scaled with them), split in the PALACE line ratios; equatorial arcs come in Phase 3 | | D | |
| Intensity, Paranal (EIA-adjacent) | 630.0: 123 R; 636.4: 41 R; 639.2: 0.019 R (164 R total at 100 sfu) | Patat: mean 150 R, 10–950 R, < 80 R in > 50 % of nights | V | [Noll2025]; [Patat2008] |
| 630/636 ratio | 2.99 measured; NIST A ratio 3.09 | Patat uses 3 | V | [Noll2025] |
| Intertropical arcs | Column emission rate < 25 R to > 800 R (OGO-4, ~21:30 LT) | | V | [Chandra1972] |
| Scaling | I₆₃₀ ∝ NmF2, decreasing with hmF2 (Barbier formula) | | V / S | [Chandra1972]; Makela 2001 (S) |

Minor ionospheric lines, same variability class: N I 519.8 + 520.0 nm, 2.99 R at 250 km.
O recombination lines (777, 845 nm and others), 72 R at 300 km, mostly near-IR [Noll2025].
Optional, off by default.

### 4.8 Lines not modelled

K D 766.5/769.9 nm (2.5 R; D2 absorbed by O₂), H Balmer (geocoronal fluorescence, not a
layer), O₂ Herzberg bands (UV), HO₂ continuum (near-IR) [Noll2025]. All are invisible or
negligible in camera and eye modes. They can be added later in science mode.

### 4.9 Layer shapes from formation chemistry (Phase 1)

No clean published FWHM statistic was found for the O₂ b, 557.7 and 630 nm layers, so their
shapes are derived from the formation chemistry (`airglow_ref/chemistry.py`, conf. D) and
then fitted with the parametric profile the shader uses.

Inputs: T, [O], [O₂], [N₂] from NRLMSIS 2.1 [MSIS21]. Rate constants:

- **557.7 nm**: McDade et al. 1986, §4.4.
- **O₂ b–X (0–0)**: V = A₁ k₁[O]²([N₂]+[O₂])[O₂] / ((A₂ + k₂ₐ[O₂] + k₂b[N₂] + k₂c[O])(D′[O₂] + D″[O])), with A₁ = 0.079 s⁻¹, A₂ = 0.083 s⁻¹, k₂ₐ = 4.0 × 10⁻¹⁷, k₂b = 2.2 × 10⁻¹⁵, k₂c = 8.0 × 10⁻¹⁴ cm³ s⁻¹, D′ = 6.6, D″ = 19 (McDade, as given in [NCAR-glbmean]). The (0–1) band shares the upper state, so it has the same shape.
- **630.0 nm**: VER ∝ n_e [O₂] · A₆₃₀ / (A(¹D) + k_N₂[N₂] + k_O₂[O₂] + k_O[O]), assuming [O⁺] ≈ n_e and O₂⁺ in photochemical equilibrium.
  - k_N₂ = 2.0 × 10⁻¹¹ exp(107.8/T) and k_O₂ = 2.9 × 10⁻¹¹ exp(67.5/T) (Streit et al. 1976); k_O = 8.0 × 10⁻¹² cm³ s⁻¹ (all as used in [NCAR-glbmean]).
  - NIST ASD: A₆₃₀ = 5.651 × 10⁻³ s⁻¹; A(¹D) total = 7.475 × 10⁻³ s⁻¹ (630.0 + 636.4 + 639.2 nm), i.e. a 134 s lifetime.
  - n_e is a Chapman-α layer with the atomic-oxygen scale height kT/(m_O g) at hmF2. hmF2 is solved so the VER peaks at the 250 km reference.
  - The O⁺ + O₂ rate and n_e scale only the magnitude, which comes from the climatology anyway.

Results for equinox midnight at 40°N, F10.7 = 100, Ap = 7 (the default shapes):

| Layer | Chemistry peak | HWHM below / above | FWHM | Fit L1 error | Reference peak used |
|---|---|---|---|---|---|
| OI 557.7 | 94.9 km | 4.29 / 4.55 km | 8.84 km | 4.4 % | 97 km |
| O₂ b | 92.6 km | 4.56 / 5.29 km | 9.85 km | 4.7 % | 94 km |
| OI 630 | 250 km (imposed) | 34.5 / 42.3 km | 76.8 km | 6.3 % | 250 km |

Variation across conditions (MSIS, midnight):

| Case | 557.7 peak / FWHM | O₂ b peak / FWHM | 630 FWHM |
|---|---|---|---|
| 40°N, June solstice | 94.7 / 6.95 km | 92.8 / 8.10 km | 74.6 km |
| 40°N, Dec solstice | 94.0 / 10.3 km | 91.2 / 10.9 km | 72.6 km |
| Equator, March equinox | 95.5 / 8.23 km | 93.6 / 9.08 km | 75.2 km |
| 20°N, Sept equinox | 95.1 / 8.02 km | 93.2 / 9.05 km | 74.2 km |

Checks and choices:
- **Layer order and spacing:** OH (87, observed) < O₂ < green, with O₂ about 2.4 km below green. The chemistry puts both layers about 2 km below the literature peaks, so the model keeps the literature peak heights and takes only width and asymmetry from the chemistry.
- **F-layer check:** the 630 nm layer peaks at 250 km for hmF2 = 305 km and H = 46 km. The resulting hmF2 − red peak ≈ 55 km independently matches the ~50 km offset in [Noll2025] (from Makela 2001).
- **Red-layer shape:** the asymmetric-Gaussian fit has a shorter upper tail than the chemistry (column centroid 255 vs 259 km). This is acceptable for now; revisit if the limb above 300 km looks wrong against photos.

## 5. Day, night and twilight

| Item | Value / approach | Conf. | Source |
|---|---|---|---|
| Full night | Ground solar zenith angle ≥ 100° means night up to ~200 km altitude | V | [Noll2025] §6.1 |
| Gate | Evaluate solar illumination at the emitting point, using the Earth's shadow height. Do not use the observer's ground SZA. | D | geometry |
| Dayglow | Exists and is often brighter than nightglow (557.7 nm dayglow has E-region ~105 km and F-region ~155 km peaks). The O(¹S) dayglow is driven directly by solar EUV. | S | WINDII, Zhang & Shepherd 2005 (S) |
| Dayglow intensities | **Open**: not yet sourced. Candidates: Zhang & Shepherd 2005 (557.7 nm), WINDII 630 nm model (JGR 2021, e2020JA028715). | Open | |
| Na twilight resonance | §4.2 | Open (g) | |

**Fading in daylight (agreed with the owner 2026-10-11).** Do not hard-cut the emission. In
daylight the airglow is present but invisible because a camera or eye adapted to daylight is
about 10⁵–10⁶× less sensitive. The display exposure applied to airglow follows an adaptation
model: exposure ∝ 1 / L_adapt, with L_adapt estimated from the scene (sun elevation at the
camera, sunlit fraction of view). Night presets then cannot make dayside airglow glow. The
adaptation model and its constants are designed in Phase 3 and documented here. It does not
affect the physical radiance.

## 6. Local time and inter-layer lags

| Item | Value | Conf. | Source |
|---|---|---|---|
| 630 nm decline after dusk | Steady decline in the first hours, interrupted at tropical sites by bursts. Bursts occur through the night, and in short (austral summer) nights mostly in the second half. | V | [Patat2008] §6.1.1 |
| Midnight brightening | Brightness maximum at 02:00 LT, Asmara (15.4°N, 39.9°E, 7°N dip). Attributed to the midnight pressure bulge, which is stronger near solstice. | V | [Wiens2004] |
| Same at Paranal | Local maxima at about 02:30 LT in March and October | V | [Noll2025] §6.2 |
| Mesospheric lags | OI 557.7 leads O₂(0,1) by 15–30 min; O₂ leads OH(6,2) by 45–60 min (Kolhapur, 17°N) | V | [Parihar2013] |
| OH nocturnal | Systematic decrease during the night is common | V | [Leinert1998] §6.3 |

## 7. Ionospheric structure (red line and F-region green)

| Item | Value | Conf. | Source |
|---|---|---|---|
| Arcs | Two bands of enhanced red line paralleling the magnetic equator at nominal dip latitudes of ±15°. OGO-4: maxima 15–20° either side of the geomagnetic equator, minimum at the equator. | V | [Wiens2004]; [Chandra1972] |
| Asymmetry | Strongly asymmetric between hemispheres over wide longitude ranges. Example: Oct 1967 post-sunset, the southern arc appeared only in the African sector. | V | [Chandra1972] |
| Arc motion | Arc moves equatorward before midnight, then brightens to the 02:00 LT maximum (Asmara) | V | [Wiens2004] |
| Dip latitude | From IGRF-14 (offline, baked to a texture). Dip latitude φ_d = atan(½ tan I), where I is the inclination. | D | standard dipole relation |
| Plasma bubbles | North–south aligned depletions in 630 nm images | V / S | EPB imager studies (S) |
| Drift | Eastward. Peak ~100 m s⁻¹ near 21–22 LT, slowing to 50–70 m s⁻¹ toward sunrise (China). 70–200 m s⁻¹ (Kolhapur, 2012). Faster at higher F10.7 and at equinox. | S | [Sun2016, S]; Kolhapur studies (S) |
| Spacing | Groups of 2–6 depletions, 100–700 km apart (mean 200–300 km). Group extent < 1500 km, up to 3000 km. | S | [Sun2016, S] |
| Width | ~400 km E–W cited secondhand (Clemesha 1964). **Open**. | Open | |
| Lifetime | Almost all disappear after ~02:00 LT (Brazil) | S | Cariri 2000–2007 (S) |

## 8. Solar cycle

Model (PALACE Eq. 1): `I = I₀ · (1 + m_SCE · (S − 100 sfu)/100 sfu)`, with S the 27-day mean
F10.7 in sfu and m_SCE the relative change per 100 sfu.

| Emission | m_SCE default [Noll2025] | Other values (range for the parameter) | Notes |
|---|---|---|---|
| OI 557.7 mesospheric | +0.754 | +0.87 [Noll2012]; log-slope 0.27 dex/MJy = ×1.86 per 100 sfu [Patat2008]; +0.4 to +0.6 low latitudes ([Zhu2015] via [Noll2025]); ±20 % amplitude at Kiso 35.8°N, largest at midnight [Das2011]; ~26 % over 1980–91 (model) [Huang2016] | Three O atoms in the Barth chain, hence strong |
| O₂ b | +0.399 | +0.63 [Noll2012]; ~23 % over a cycle (model) [Huang2016] | |
| OH | +0.12 to +0.20 (higher for higher v, N) | ~0 [Noll2012]; 30–40 %/100 sfu at Zvenigorod 56°N (Pertsev & Perminov 2008, S); ~8 % (model) [Huang2016] | |
| Na D | +0.235 | +0.11 [Noll2012]; log-slope 0.04 dex/MJy [Patat2008] | |
| FeO continuum | +0.086 | | |
| OI 630/636 | +1.432 | +0.68 [Noll2012]; 0.25 dex/MJy [Patat2008]. May flatten at very high S. | Ionospheric (EUV) |
| N 520 | +1.570 | | |

Data coverage: X-shooter 27-day means 67–166 sfu [Noll2025]; ESO model data 95–228 sfu
[Noll2012]. The UI should warn outside ~65–230 sfu. Default S = 100 sfu, the PALACE
reference (a tuning choice; can be set to current conditions). Owner confirmed the 100 sfu
default on 2026-10-11.

## 9. Gravity waves

| Item | Value | Conf. | Source |
|---|---|---|---|
| Bands | λh 10–45 km (mean 20 km, by definition 10–60 km), periods 5–35 min, phase speeds 1–80 m s⁻¹ (mean 36 m s⁻¹). Preferential propagation direction set by sources. | V | [Medeiros2007] (7°S) |
| Bands, mid-latitude | λh 10–35 km, periods 4–14 min, speeds 30–60 m s⁻¹. Mainly meridional: SW in winter, NE in summer. More frequent at solstices. | V | [Li2011] (40°N) |
| Ripples | Short-lived (< 45 min), λh 5–20 km (mean 14 km), periods 4–12 min, mean speed 29 m s⁻¹ | V | [Medeiros2007] |
| Amplitude | Relative intensity perturbation typically 5–10 % in OH images. Exceptional events up to 40 % (photometer). Bores > 50 %. No statistic found for 557.7 nm. | S | case studies (S) |
| Concentric rings | 9 of 723 clear nights (Fort Collins 2003–2008), visible ~1.5 h, need weak background winds. λh 15–40 km, speeds 40–60 m s⁻¹, periods 20–30 min. Space view: λh 40–250 km, radii 200–3000 km. | V / S | [Yue2009, S]; [Vadas2012, S]; IMAP/VISI (S) |
| Vertical structure | Same pattern in OH, O₂ and OI with altitude-dependent phase: Δφ = 2π Δz / λz, λz from the GW dispersion relation (Brunt–Väisälä frequency from the background atmosphere). Intensity response per layer via a cancellation factor (can be in or out of phase). | V (qual.) | [Parihar2013]; [Wust2022]; Krassovsky-ratio literature |

## 10. Tides and longitude

| Item | Value | Conf. | Source |
|---|---|---|---|
| Equatorial equinox maximum (OH, midnight) | Equatorial stripe in Mar/Sep is a factor of 2–2.5 above ±35°. Jan/Jul equatorial brightness about half of March. | V | [Xu2010] ¶9 |
| Longitude, equator | Wavenumber 1 dominates (minimum near 180°). Wavenumber 4 also present, especially Jul and Sep. | V | [Xu2010] ¶10 |
| Longitude, ±20° | Wavenumber 4 dominant. Hemispheres out of phase. Wave-3 at 20°N in May (DE2). | V | [Xu2010] |
| Amplitude | Wave-4 about ±10–15 % at ±20° and wave-1 about ±15 % at the equator (2.0 µm OH brightness, midnight). Read from Fig. 1; the text gives no number. | V-fig | [Xu2010] Fig. 1 |
| Cause | DE3 (wave-4 in fixed local time), DW2 + D0 (wave-1). DE3 max near Sept equinox, min near Dec solstice; phase ~65° at 87 km at the equator. | V | [Xu2010] ¶17–18 |
| Emission–temperature sign | OH VER positively correlated with T below ~94 km, negatively above | V | [Xu2010] |
| Green line SAO | Semiannual at low latitudes (equinox maxima); annual at higher latitudes | V | [Das2011] intro (Fukuyama 1977; Liu & Shepherd 2008) |
| Climatologies | PALACE provides f₀(month, LT) and m_SCE(month, LT) per variability class for Paranal | V | [PALACE-data] |

## 11. Lower-atmosphere transmission (ground view)

Implemented in Phase 1 (`airglow_ref/atmosphere.py`, `geometry.py`):

| Term | Model | Conf. | Source |
|---|---|---|---|
| Rayleigh | σ(λ) = (1.0455996 − 341.29061 λ⁻² − 0.90230850 λ²) / (1 + 0.0027059889 λ⁻² − 85.968563 λ²) × 10⁻²⁸ cm², λ in µm (dry air, 360 ppm CO₂; < 0.01 % error 250–850 nm), times the MSIS air column along the path. Check: vertical τ(550 nm) from the MSIS column = 0.0973 ± 3 % (test). | V | [Bodhaine1999] Eq. 29, Table 3 |
| Ozone | Serdyuchenko et al. cross-sections at 223 K (5.09 × 10⁻²¹ cm² at 600 nm) on a Gaussian layer at 22 km, FWHM 15 km, 300 DU | V (σ), tuning (layer) | [Serdyuchenko2014] |
| Aerosol | τ(550 nm) = 0.05, Ångström exponent 1.3, exponential scale height 1.2 km | tuning | |
| Geometry | Emission behind a lower-atmosphere crossing is attenuated by the slant columns along the actual ray; near-side emission seen from orbit is not | D | |
| In-game spectral treatment | The shader cannot carry each layer's full spectrum, so the visible range is split into 11 fixed bins (360, 420, 460, 490, 520, 545, 570, 595, 620, 650, 690, 830 nm). Each layer carries per-bin colour weights and radiance-weighted mean cross-sections, and gets one transmittance per bin. Against exact per-line transmittance (sea level, default absorbers): luminance error < 1 % down to 1° elevation (O₂ bands 3 %, negligible light), largest single-channel error ~7 % at 1° and ≤ 2 % from 5° up. A single mean cross-section per colour channel was tried first and is not good enough: up to 21 % too dark at 1° (Jensen bias on broad spectra). | D | `reference/export_ksp_config.py` |

- O₂ A-band and B-band self-absorption make O₂(b) (0–0) and (1–0) invisible from the ground
  [Noll2012]. The K D2 zenith transmission is 0.0026 [Noll2025] (illustrative). The reference
  model does not yet include O₂ line absorption, which only affects the O₂ and K bands.
- [Noll2012]: airglow extinction is weak at typical observing zenith angles. Their fitted
  τ₀ = 0.27 (Rayleigh) is a parameterisation for their reduction factor, not a measured
  optical depth to reuse.
- Direct airglow with extinction, sea-level observer (camera luminance, relative to zenith
  without extinction) peaks at about 2× near z = 75–78° and falls to 0 at the horizon.
  [Leinert1998] §6.2 says "a factor of about four" may be expected around z ≈ 80°; that figure
  is a 1950s prediction and probably assumes a cleaner, higher site. Not resolved; see the
  scattering term below.

**Scattered airglow** (estimate, `airglow_ref/scattering.py`, conf. D). Single scattering,
Rayleigh plus aerosol (Henyey–Greenstein g = 0.7, tuning), black ground, sea level, with the
default absorbers. Values are relative to the zenith radiance above the atmosphere:

| Zenith angle | Green direct | Green scattered | Red direct | Red scattered |
|---|---|---|---|---|
| 0° | 0.84 | 0.11 | 0.88 | 0.08 |
| 60° | 1.37 | 0.24 | 1.41 | 0.17 |
| 75° | 1.71 | 0.39 | 1.69 | 0.28 |
| 80° | 1.61 | 0.49 | 1.57 | 0.37 |
| 85° | 0.93 | 0.64 | 0.99 | 0.52 |
| 88° | 0.21 | 0.71 | 0.32 | 0.65 |

So scattered airglow adds ~10–15 % everywhere, ~30 % at 80°, and dominates within ~5° of the
horizon. The ground-view shader therefore needs it; it depends only on view zenith angle and
observer height for a horizontally uniform layer, so a small LUT will do (design in Phase 2/4).
Multiple scattering and ground albedo would add more and are not estimated yet.

## 12. Colour, spectral response and exposure

### 12.1 Colorimetry

- CIE 1931 2° colour-matching functions and the photopic and scotopic luminous efficiency
  tables come from CIE datasets, **licensed CC BY-SA 4.0** (checked in the dataset metadata)
  [CIE-CMF], [CIE-V].
- Pipeline: line or band spectrum (photons) → spectral radiance (E = hc/λ) → XYZ with
  K_m = 683 lm W⁻¹, so Y is luminance in cd m⁻² → linear sRGB. The RGB↔XYZ matrices are
  derived from the Rec. 709 primaries and the D65 white point (IEC 61966-2-1).
- Out of gamut (monochromatic 557.7, 589 and 630 nm all are): mix toward the white of the same
  luminance by the smallest amount that removes negative channels. Luminance is preserved
  exactly and nothing is clipped from above (test).
- PALACE wavelengths are vacuum; the CIE tables are used as-is (air/vacuum differ by
  ~0.16 nm, a < 0.1 % effect).

### 12.2 Spectral response modes

1. **Camera (default).** ISS night imagery is shot on Nikon DSLRs and mirrorless bodies:
   D3S, D4, D5, Z9 (NASA photo metadata, S). Examples: D4 at 1.3 s, f/1.4, ISO 12800, 28 mm
   (Exp. 47 starry-night limb pass, S). Z9 at 5 s, 24 mm, handheld, for the Earth Observatory
   reference ISS072-E-896652 (2025-03-23 09:37:19 GMT; camera and focal length confirmed in
   the Gateway metadata, exposure from the EO caption). Measured spectral sensitivities exist
   for the Nikon D3 and D700 (stock, with IR-cut) in [Jiang2013], **licence CC BY-NC-SA 4.0**
   (checked on the Zenodo record). No measured D5 or Z9 curves were found.
   Implemented with the Nikon D3 curves (400–720 nm, the closest relative of the ISS D3S).
   Camera RGB → XYZ uses a 3×3 matrix fitted by least squares to the CIE 1931 functions
   over 400–720 nm (maximum-ignorance colour correction), so camera "luminance" is in
   cd m⁻² as far as the camera meets the Luther condition. Result: the camera reports
   557.7 nm at 0.75× its true luminance and 630 nm at 1.18×.
2. **Human eye.** Computed luminances (§2, §3.2): green limb from orbit ≈ 163–250 R × ~56 ≈
   9–14 kR, i.e. ≈ 1.8–2.7 × 10⁻³ cd m⁻² photopic and about the same scotopic. That is below
   the CIE 191:2010 mesopic lower bound of 0.005 cd m⁻², i.e. in the scotopic (rod) regime
   [CIE191]. So the model predicts the green band is seen as a faint, nearly colourless glow.
   The red layer (~1–3 kR on the limb) is ≈ 5 × 10⁻⁵ to 1.4 × 10⁻⁴ cd m⁻² photopic and
   ~10⁻⁶ scotopic, so effectively invisible. No first-hand astronaut description of the
   naked-eye appearance was found.
   Implemented: luminance from the CIE 191 mesopic system, with the adaptation coefficient m
   from the fixed-point iteration m = 0.7670 + 0.3334 log₁₀ L_mes, m₀ = 0.5, clamped to
   [0, 1], and L_mes = (m L_p + (1 − m) V′(λ₀) L_s) / (m + (1 − m) V′(λ₀)) with
   V′(λ₀) = 683/1700 [CIE191] as reproduced by [Gao2017]. Colour: the photopic chromaticity
   mixed toward white by (1 − m). That desaturation rule is our approximation, not CIE 191;
   [Kirk2011] remains the candidate for a fuller rod/cone model. On the 200 mm limb render
   the maximum m is 0.05, so the eye view is essentially grey and the red layer disappears.
3. **Science.** Each emission in its own false-colour channel, linear.

### 12.3 Exposure

- Display exposure for airglow follows the camera equation `H = q L t / N²` (lx s) with
  q = (π/4) T v cos⁴θ = 0.65 (ISO 12232:1998 typical value), and saturation-based speed
  `S = 78 lx s / H_sat` (ISO 12232:2006). Both are read from a secondary summary of the
  standards (conf. S); the standards themselves are paywalled. Pixel value 1.0 = saturation.
- "Realistic photographic" preset (master 100 %): Nikon D4, 1.3 s, f/1.4, ISO 12800, i.e.
  0.0141 cd m⁻² saturates. **Confirmed from the EXIF** of ISS047-E-1114 (2016-03-02
  20:44:08 GMT, 28 mm, manual exposure, auto white balance), an Expedition 47 limb photo.
- The Earth Observatory reference ISS072-E-896652 has, per its EXIF, 30 s at f/1.4 and
  ISO 500 (Nikon Z9, 24 mm). Its caption says "five-second exposure", which the EXIF
  contradicts. Its exposure scale (fraction of saturation per cd m⁻², q t S / (78 N²)) is
  63.8, within 10 % of the D4 frame's 70.7, so two independent ISS night photographs used
  nearly the same effective exposure.
- The ground all-sky render uses an illustrative 20 s, f/2.8, ISO 6400 exposure, not tied to
  any photograph.
- Owner default: master 30 % = log₂ 0.3 = **−1.74 EV**.

## 13. Disagreements between sources and choices made

| Topic | Sources | Choice | Why |
|---|---|---|---|
| Reference intensities | [Leinert1998] "typical" (1960s–70s compilations) vs [Noll2025] (10 yr X-shooter, solar-normalised) | [Noll2025] for lines and bands, [Leinert1998] for mid-latitude red and seasonal Na | Modern, calibrated, normalised to 100 sfu. Red line at Paranal is EIA-enhanced, so mid-latitude red comes from Leinert. |
| O₂ b peak | 80 km [Leinert1998] vs 94 km (others) | 94 km | Multiple modern measurements |
| Green peak | 90 km [Leinert1998], 92–96 km [Gao2012], 97 km [Noll2012, Noll2025] | 97 km reference, latitude/season variation per [Gao2012] | Reference height consistent with the intensity reference |
| Red peak | 250 [Noll2025], 270 [Noll2012], 280 [Wiens2004] | 250 km reference, driven by hmF2 in Phase 3 | Varies with the ionosphere anyway |
| Solar coefficients | see §8 | [Noll2025] defaults, ranges exposed | Same dataset as the reference intensities |
| McDade constants | McDade 1986 set (A₅₅₈ 1.18 s⁻¹) vs [Lednytskyy2015] extended set (1.16) vs NIST (1.26) | McDade 1986 set | The C′/C″ fit constants were derived with it; only the shape is used, so the A values hardly matter |
| Limb ratio | 35× (Gemini) vs 47–56× geometry | Geometry from profiles; 35× as lower bound | Gemini values are resolution-limited |
| Continuum origin | NO₂ ([Noll2012], [Gill2020], brief) vs FeO-like ([Noll2025], [Evans2010]) | FeO-like dominant, NO₂ minor separate layer | Newest decomposition with WACCM support. [Mende1993] still shows a 103 km NO₂ layer. |

## 14. Data licences relevant to the build

| Data | Licence | Planned use |
|---|---|---|
| PALACE model data (line list, continuum, variability) [PALACE-data] | CC BY 4.0 (data); code is GPL-3.0 | Offline in the reference model, with attribution. Code is not copied. |
| CIE CMFs, V(λ), V′(λ) | CC BY-SA 4.0 | Downloaded at run time into the uncommitted cache; only derived coefficients are committed, with attribution |
| Jiang 2013 camera sensitivities | CC BY-NC-SA 4.0 | Same: run-time download, derived coefficients committed with attribution |
| NRLMSIS 2.1 (via pymsis, MIT wrapper) | MSIS2 code: free, not for commercial use without contacting NRL | Offline layer-shape derivation only |
| TSIS-1 HSRS v2 | NASA/LASP public data; no licence text in the file | Run-time download, offline g-factor |
| Serdyuchenko O₃ cross-sections (IUP Bremen) | no licence text on the page | Run-time download, offline extinction |
| IGRF-14 coefficients | to be checked | Offline bake of dip latitude (Phase 3) |

Owner decision 2026-10-11: raw third-party tables are fetched at run time into
`reference/data/cache/` (gitignored) and never committed; derived numbers are committed with
attribution; the NC/SA camera data is acceptable for this free mod.

## Bibliography

- **[Baker1976]** Baker, D. J., Romick, G. J. (1976). The rayleigh: interpretation of the unit in
  terms of column emission rate or apparent radiance expressed in SI units. *Appl. Opt.* 15(8),
  1966–1968.
- **[Baker1988]** Baker, D. J., Stair, A. T. (1988). Rocket measurements of the altitude
  distributions of the hydroxyl airglow. *Physica Scripta* 37, 611–622. (Read via [Wust2022].)
- **[Bodhaine1999]** Bodhaine, B. A., Wood, N. B., Dutton, E. G., Slusser, J. R. (1999). On
  Rayleigh optical depth calculations. *J. Atmos. Oceanic Technol.* 16, 1854–1861.
- **[Chandra1972]** Chandra, S., et al. (1972). The equatorial airglow and the ionospheric
  geomagnetic anomaly. NASA TM-X-65978.
  https://ntrs.nasa.gov/api/citations/19720021779/downloads/19720021779.pdf
- **[CIE-CMF]** CIE (2019). Colour-matching functions of CIE 1931 standard colorimetric
  observer. doi:10.25039/CIE.DS.xvudnb9b (CC BY-SA 4.0).
- **[CIE-V]** CIE photopic and scotopic luminous efficiency datasets (files.cie.co.at,
  CC BY-SA 4.0).
- **[CIE191]** CIE 191:2010, Recommended system for mesopic photometry based on visual
  performance. Range as stated by CIE/Electropedia (IEV 845-21-035).
- **[Das2011]** Das, U., Pan, C. J., Sinha, H. S. S. (2011). Effects of solar cycle variations on
  oxygen green line emission rate over Kiso, Japan. *Earth Planets Space* 63, 941–948.
  doi:10.5047/eps.2011.04.006
- **[Evans2010]** Evans, W. F. J., Gattinger, R. L., Slanger, T. G., Saran, D. V., Degenstein,
  D. A., Llewellyn, E. J. (2010). Discovery of the FeO orange bands in the terrestrial night
  airglow spectrum obtained with OSIRIS on the Odin spacecraft. *GRL* 37, L22105.
  doi:10.1029/2010GL045310. The ≤ 5 % NO₂ upper limit is from a summary (S), not yet read in
  the paper.
- **[Gao2012]** Gao, H., Nee, J.-B., Xu, J. (2012). The emission of oxygen green line and density
  of O atom determined by using ISUAL and SABER measurements. *Ann. Geophys.* 30, 695–701.
- **[Gao2017]** Gao, C., et al. (2017). Improved computation of the adaptation coefficient in
  the CIE system of mesopic photometry. *Optics Express* 25(15), 18365 (reproduces the
  CIE 191:2010 equations and constants; S).
- **[Gill2020]** Gill, A., et al. (2020). Optical night sky brightness measurements from the
  stratosphere. arXiv:2010.05145.
- **[Huang2016]** Huang, T.-Y. (2016). Simulations of airglow variations induced by the CO₂
  increase and solar cycle variation from 1980 to 1991. *JASTP* 147, 138–147.
  doi:10.1016/j.jastp.2016.07.014 (S, abstract).
- **[Jiang2013]** Jiang, J., Liu, D., Gu, J., Süsstrunk, S. (2013). What is the space of spectral
  sensitivity functions for digital color cameras? *WACV 2013*. Data: Zenodo 3245883
  (CC BY-NC-SA 4.0).
- **[Killen2022]** Killen, R. M., Vervack, R. J., Burger, M. H. (2022). Updated photon scattering
  coefficients (g-values) for Mercury's exospheric species. *ApJS* 263, 37.
- **[Kirk2011]** Kirk, A. G., O'Brien, J. F. (2011). Perceptually based tone mapping for low-light
  conditions. *ACM TOG* 30(4), 42.
- **[Koomen1971]** Koomen, M. J., Seal, R. T., Jr., Lintott, J., et al. (1971). Experiment S011:
  Airglow horizon photography. NASA Gemini Program Phys. Sci. Expt. Summary. NTRS 19720003209.
- **[Lednytskyy2015]** Lednyts'kyy, O., von Savigny, C., Eichmann, K.-U., Mlynczak, M. G. (2015).
  Atomic oxygen retrievals in the MLT region from SCIAMACHY nightglow limb measurements.
  *AMT* 8, 1021–1041. doi:10.5194/amt-8-1021-2015
- **[Leinert1998]** Leinert, C., et al. (1998). The 1997 reference of diffuse night sky
  brightness. *A&AS* 127, 1–99, §6 and Table 13.
- **[Li2011]** Li, Q., Xu, J., Yue, J., Yuan, W., Liu, X. (2011). Statistical characteristics of
  gravity wave activities observed by an OH airglow imager at Xinglong, in northern China.
  *Ann. Geophys.* 29, 1401–1410.
- **[Medeiros2007]** Medeiros, A. F., et al. (2007). MLT gravity wave climatology in the South
  America equatorial region observed by airglow imager. *Ann. Geophys.* 25, 399–406.
- **[Mende1993]** Mende, S. B., et al. (1993). Limb view spectrum of the Earth's airglow. *JGR* 98.
  doi:10.1029/93JA02282 (S, abstract).
- **[MSIS21]** Emmert, J. T., et al. NRLMSIS 2.1 empirical atmosphere model (US Naval Research
  Laboratory), used through pymsis 0.13 (Lucas et al., MIT licence).
- **[NCAR-glbmean]** NCAR HAO global-mean model source `mkemiss.F`
  (https://download.hao.ucar.edu/pub/lqian/glbmean/src/mkemiss.F). Its comments give the
  McDade parameterisations "from Ian McDade, Mar 7, 1994 mail communication to Roble". Used
  only as a source of the published constants; no code is copied.
- **[NIST-ASD]** Kramida, A., Ralchenko, Yu., Reader, J., NIST ASD Team. NIST Atomic Spectra
  Database (queried 2026-10-11): O I and Na I transition probabilities and oscillator strengths.
- **[Noll2012]** Noll, S., et al. (2012). An atmospheric radiation model for Cerro Paranal. I. The
  optical spectral range. *A&A* 543, A92. arXiv:1205.2003.
- **[Noll2025]** Noll, S., Schmidt, C., Hannawald, P., Kausch, W., Kimeswenger, S. (2025). PALACE
  v1.0: Paranal Airglow Line And Continuum Emission model. *Geosci. Model Dev.* 18, 4353–4398.
  doi:10.5194/gmd-18-4353-2025
- **[PALACE-data]** Noll, S., et al. (2024). PALACE model: data and code of v1.0. Zenodo
  doi:10.5281/zenodo.14064022 (data CC BY 4.0, code GPL-3.0).
- **[Parihar2013]** Parihar, N., Taori, A., Gurubaran, S., Mukherjee, G. K. (2013). Simultaneous
  measurement of OI 557.7 nm, O₂(0,1) Atmospheric Band and OH(6,2) Meinel Band nightglow at
  Kolhapur (17°N), India. *Ann. Geophys.* 31, 197–208.
- **[Patat2008]** Patat, F. (2008). The dancing sky: 6 years of night-sky observations at Cerro
  Paranal. *A&A* 481, 575–591. arXiv:0801.2270.
- **[Paulino-CISBGf]** OI 557.7 nm F-region nightglow intensity at 7.4°S, 8th CISBGf expanded
  abstract (arq_607). Conference paper, not peer-reviewed (S).
- **[Petrignani2005]** Storage-ring O₂⁺ dissociative recombination yields (AMOLF), (S).
- **[Serdyuchenko2014]** Serdyuchenko, A., Gorshelev, V., Weber, M., Chehade, W., Burrows,
  J. P. (2014). High spectral resolution ozone absorption cross-sections – Part 2:
  Temperature dependence. *AMT* 7, 625–636. Data: IUP Bremen (version 2012).
- **[Sun2016]** Sun, L., et al. (2016). A statistical analysis of equatorial plasma bubble
  structures based on an all-sky airglow imager network in China. *JGR Space Phys.* (S).
- **[TSIS-HSRS]** Coddington, O. M., et al. (2021). The TSIS-1 Hybrid Solar Reference Spectrum.
  *GRL* 48, e2020GL091709. Version 2 file `hybrid_reference_spectrum_c2022-11-30_with_unc.nc`
  (LASP LISIRD).
- **[Vadas2012]** Vadas, S. L., Yue, J., Nakamura, T. (2012). Mesospheric concentric gravity waves
  generated by multiple convective storms over the North American Great Plain. *JGR* (S).
- **[Wiens2004]** Wiens, R. H., et al. (2004). Brightness variations of the northern 630 nm
  intertropical arc and the midnight pressure bulge over Eritrea. *Ann. Geophys.* 22,
  3251–3259.
- **[Wust2022]** Wüst, S., Bittner, M., Espy, P. J., French, W. J. R., Mulligan, F. J. (2022).
  Hydroxyl airglow observations for investigating atmospheric dynamics: results and challenges.
  *ACP Discuss.* doi:10.5194/acp-2022-528.
- **[Xu2010]** Xu, J., et al. (2010). Strong longitudinal variations in the OH nightglow. *GRL* 37,
  L21801. doi:10.1029/2010GL043972
- **[Yee1997]** Yee, J.-H., Crowley, G., Roble, R. G., Skinner, W. R., Burrage, M. D., Hays,
  P. B. (1997). Global simulations and observations of O(¹S), O₂(¹Σ) and OH mesospheric
  nightglow emissions. *JGR* 102, 19949–19968. doi:10.1029/96JA01833 (cited via [Noll2025]).
- **[Zhu2015]** Zhu, Y., Kaufmann, M., Ern, M., Riese, M. (2015). Nighttime atomic oxygen in the
  mesopause region retrieved from SCIAMACHY O(¹S) green line measurements and its response to
  solar cycle variation. *JGR Space Phys.* 120, 9057–9073. doi:10.1002/2015JA021405 (cited via
  [Noll2025]).
- **[Yuan2019]** Yuan, T., She, C.-Y., et al. (2019). Climatology, long-term trend and solar
  response of Na density based on 28 years (1990–2017) of midlatitude mesopause Na lidar
  observation. *JGR Space Phys.* (S, abstract).
- **[Yue2009]** Yue, J., Vadas, S. L., She, C.-Y., et al. (2009). Concentric gravity waves in the
  mesosphere generated by deep convective plumes in the lower atmosphere near Fort Collins,
  Colorado. *JGR* 114, D06104 (S).
