"""Side-by-side: ISS047-E-1114 (NASA) and the reference model with the photo's settings.

Photo EXIF: Nikon D4, 28 mm, 1.3 s, f/1.4, ISO 12800, 2016-03-02 20:44:08 GMT, ISS at
~415 km (caption: 258 miles) over the Indian Ocean. The model has no stars, moonlight, city
lights or clouds; compare the airglow bands above the hard limb only.
"""
from __future__ import annotations

from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from PIL import Image

from airglow_ref import atmosphere, colour, data, exposure, layers, render
from airglow_ref.constants import R_EARTH

OUT = Path(__file__).resolve().parent / "output"
ALT_KM = 415.0


def main():
    OUT.mkdir(exist_ok=True)
    photo = Image.open(data.fetch("ISS047-E-1114.JPG"))
    photo.thumbnail((1200, 1200))
    W, H = photo.size
    # Full-frame 28 mm: horizontal FOV 2 atan(18/28) = 65.5 deg. The limb's crown sits about
    # 76 % of the frame height from the top in the photo; place the horizon there.
    hfov = 2 * np.degrees(np.arctan(18.0 / 28.0))
    r0 = R_EARTH + ALT_KM * 1e3
    dip = np.arccos(R_EARTH / r0)
    vfov = 2 * np.arctan(np.tan(np.radians(hfov) / 2) * H / W)
    el = -dip + np.arctan(0.52 * np.tan(vfov / 2))
    obs = np.array([r0, 0.0, 0.0])
    dirs = render.pinhole_dirs([np.sin(el), np.cos(el), 0.0], [1.0, 0.0, 0.0], hfov, W, H)
    path = OUT / "compare_iss047.npz"
    if path.exists():
        rgb = np.load(path)["rgb"]
    else:
        out = render.render(obs, dirs, layers.default_components(), colour.camera_response(),
                            lower=atmosphere.LowerAtmosphere())
        rgb = sum(out.values())
        np.savez_compressed(path, rgb=rgb)
    s = exposure.CameraExposure(12800, 1.4, 1.3).scale()
    img = exposure.srgb_encode(colour.gamut_map(rgb) * s)
    fig, ax = plt.subplots(1, 2, figsize=(16, 5.6), dpi=100)
    ax[0].imshow(photo); ax[0].set_title("ISS047-E-1114 (NASA), Nikon D4 28 mm, 1.3 s f/1.4 ISO 12800")
    ax[1].imshow(img); ax[1].set_title("Reference model, same camera settings, 415 km, F10.7 = 100, mid-latitude red line")
    for a in ax:
        a.axis("off")
    fig.tight_layout()
    fig.savefig(OUT / "compare_iss047.png")
    print("done")


if __name__ == "__main__":
    main()
