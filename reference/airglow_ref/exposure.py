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
