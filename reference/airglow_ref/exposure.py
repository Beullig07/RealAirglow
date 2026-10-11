"""Photographic exposure and display (docs/PHYSICS.md §12.3).

Focal-plane exposure H = q L t / N^2 (lx s) with q = 0.65 (ISO 12232:1998 value via the
"Film speed" literature summary) and saturation-based ISO speed S = 78 lx s / H_sat
(ISO 12232:2006). A pixel value of 1.0 means saturation.
"""
from __future__ import annotations

from dataclasses import dataclass

import numpy as np

Q_LENS = 0.65


@dataclass(frozen=True)
class CameraExposure:
    iso: float
    f_number: float
    seconds: float
    label: str = ""

    def scale(self) -> float:
        """Multiply luminance (cd m^-2) by this to get the fraction of saturation."""
        return Q_LENS * self.seconds / self.f_number ** 2 * self.iso / 78.0


# Nikon D4 frame from an Expedition 47 starry-night limb pass: 1.3 s, f/1.4, ISO 12800,
# 28 mm (NASA image metadata via search summary, conf. S; to be confirmed from EXIF).
ISS_D4_LIMB = CameraExposure(12800, 1.4, 1.3, "ISS D4 1.3 s f/1.4 ISO 12800")
# Illustrative ground all-sky night exposure (not tied to a specific photograph).
GROUND_ALLSKY = CameraExposure(6400, 2.8, 20.0, "all-sky 20 s f/2.8 ISO 6400 (illustrative)")


def apply_display_multipliers(layer_rgb: dict, master: float, per_group: dict) -> np.ndarray:
    """Sum per-group linear radiance with the user's display multipliers.

    The multipliers scale airglow only and are linear; master = 0 gives exactly zero.
    Physical values (rayleighs) are untouched: this acts on the summed display radiance.
    """
    total = None
    for group, rgb in layer_rgb.items():
        term = rgb * (master * per_group.get(group, 1.0))
        total = term if total is None else total + term
    return total


def srgb_encode(linear):
    """IEC 61966-2-1 sRGB transfer function, input clipped to [0, 1]."""
    x = np.clip(linear, 0.0, 1.0)
    return np.where(x <= 0.0031308, 12.92 * x, 1.055 * np.power(x, 1 / 2.4) - 0.055)


# --- Local outshining (docs/PHYSICS.md §5, owner request for 1.0.3) ---------------------------
# The metered exposure of §5 applied per emitting point: airglow keeps the night preset's
# exposure where the local background (twilight sky or sunlit ground) is darker than what that
# preset meters, and is scaled down by the ratio where it is brighter.
METER_K = 12.5                  # cd s m^-2, ISO 2720 reflected-light meter constant
EARTH_ALBEDO = 0.29             # [Stephens2015]
SOLAR_ILLUMINANCE_LX = 134634   # TSIS-1 HSRS x CIE V(lambda) x 683 lm/W at 1 au
MAG0_LUMINANCE = 1.08e5         # cd m^-2 for 0 mag arcsec^-2 [Bara2020]


def metered_luminance(e: CameraExposure) -> float:
    """Luminance an averaging meter calls correct at these settings: K N^2 / (t S)."""
    return METER_K * e.f_number ** 2 / (e.seconds * e.iso)


def twilight_sky_luminance(zenith_deg):
    """[Patat2006] Table 1 fit, V band, held at its 95 and 105 deg ends."""
    x = np.clip(zenith_deg, 95.0, 105.0) - 95.0
    mag = 11.84 + 1.518 * x - 0.057 * x ** 2
    return MAG0_LUMINANCE * 10.0 ** (-0.4 * mag)


def background_luminance(zenith_deg):
    zenith_deg = np.asarray(zenith_deg, float)
    sky = twilight_sky_luminance(zenith_deg)
    sunlit = EARTH_ALBEDO * SOLAR_ILLUMINANCE_LX * np.maximum(np.cos(np.radians(zenith_deg)), 0.0) / np.pi
    return np.where(zenith_deg < 95.0, np.maximum(sky, sunlit), sky)


def outshine_weight(zenith_deg, e: CameraExposure = ISS_D4_LIMB):
    return np.minimum(1.0, metered_luminance(e) / background_luminance(zenith_deg))


def outshine_modulation(sun_dir, e: CameraExposure = ISS_D4_LIMB):
    """render(..., modulation=...) hook: weight each emitting point by its outshine factor."""
    s = np.asarray(sun_dir, float)
    s = s / np.linalg.norm(s)

    def mod(points, comp):
        cz = np.clip((points @ s) / np.linalg.norm(points, axis=-1), -1.0, 1.0)
        return outshine_weight(np.degrees(np.arccos(cz)), e)
    return mod
