"""Render the Phase 1 reference images into reference/output/.

    python make_images.py            # all images
    python make_images.py --quick    # quarter resolution for a fast check

Renders are cached as .npz so display settings can be changed without re-integrating.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

from airglow_ref import atmosphere, colour, exposure, layers, render
from airglow_ref.constants import R_EARTH

OUT = Path(__file__).resolve().parent / "output"
ISS_ALT_KM = 420.0
MASTER_DEFAULT = 0.30  # owner default: 30 % of photographic brightness


def cached(name, fn):
    path = OUT / f"{name}.npz"
    if path.exists():
        d = np.load(path)
        return {k: d[k] for k in d.files}
    res = fn()
    np.savez_compressed(path, **res)
    return res


def to_image(rgb_lin, scale, master=1.0):
    return exposure.srgb_encode(colour.gamut_map(rgb_lin * master) * scale)


def iss_view(hfov, width, height, horizon_frac, centre_tangent_km=None):
    """Observer at ISS altitude over (0 N, 0 E), looking east along the limb."""
    r0 = R_EARTH + ISS_ALT_KM * 1e3
    obs = np.array([r0, 0.0, 0.0])
    if centre_tangent_km is not None:
        el = -np.arccos((R_EARTH + centre_tangent_km * 1e3) / r0)
    else:
        dip = np.arccos(R_EARTH / r0)
        vfov = 2 * np.arctan(np.tan(np.radians(hfov) / 2) * height / width)
        el = -dip - np.arctan(horizon_frac * np.tan(vfov / 2))
    fwd = np.array([np.sin(el), np.cos(el), 0.0])
    up = np.array([1.0, 0.0, 0.0])
    return obs, render.pinhole_dirs(fwd, up, hfov, width, height)


def main(quick=False):
    OUT.mkdir(exist_ok=True)
    k = 4 if quick else 1
    sfx = "_quick" if quick else ""
    comps = layers.default_components()
    cam, cie = colour.camera_response(), colour.cie_response()
    lower = atmosphere.LowerAtmosphere()
    groups = list(layers.GROUPS)

    def run(name, obs, dirs, resp, lower_atm):
        return cached(name + sfx, lambda: render.render(obs, dirs, comps, resp, lower=lower_atm))

    # 1. ISS wide view, 24 mm full frame (HFOV 73.7 deg), horizon 30 % above centre.
    obs, dirs = iss_view(73.7, 1500 // k, 1000 // k, horizon_frac=0.3)
    wide = run("iss_wide_camera", obs, dirs, cam, lower)
    tot = sum(wide[g] for g in groups if g in wide)
    s = exposure.ISS_D4_LIMB.scale()
    plt.imsave(OUT / f"iss_limb_wide_100pct{sfx}.png", to_image(tot, s))
    plt.imsave(OUT / f"iss_limb_wide_30pct{sfx}.png", to_image(tot, s, MASTER_DEFAULT))

    # 2. ISS 200 mm view (HFOV 10.3 deg) centred on the 150 km tangent height:
    #    camera / human eye / science panels.
    obs, dirs = iss_view(10.3, 900 // k, 600 // k, 0, centre_tangent_km=150.0)
    zc = run("iss_zoom_camera", obs, dirs, cam, lower)
    zp = run("iss_zoom_cie", obs, dirs, cie, lower)
    scot = colour.Response(*_scotopic(), np.eye(3), "scotopic")
    zs = run("iss_zoom_scotopic", obs, dirs, scot, lower)
    cam_img = to_image(sum(zc.values()), s)
    rgb_p = sum(zp.values())
    eye, m = colour.eye_display(rgb_p, rgb_p @ colour.LUMA, sum(zs.values())[..., 0])
    eye_img = to_image(eye, s)
    sci = sum((zc[g] @ colour.LUMA)[..., None] * np.array(colour.SCIENCE_COLOURS[g]) for g in zc)
    sci_img = to_image(sci, s)
    fig, ax = plt.subplots(1, 3, figsize=(18, 4.6), dpi=100)
    for a, img, title in zip(ax, (cam_img, eye_img, sci_img),
                             ("Camera (Nikon D3 response), ISS D4 exposure",
                              f"Human eye (CIE 191 mesopic, max m = {m.max():.2f}), same exposure",
                              "Science: green=557.7 red=630 orange=OH yellow=Na blue=continuum")):
        a.imshow(img); a.set_title(title, fontsize=10); a.axis("off")
    fig.suptitle("Limb from 420 km, 200 mm lens, centred on 150 km tangent height (master 100 %)")
    fig.tight_layout(); fig.savefig(OUT / f"iss_limb_200mm_modes{sfx}.png"); plt.close(fig)

    # 3. Ground all-sky fisheye, 40 N, with extinction.
    size = 800 // k
    dirs, valid = render.fisheye_dirs([0, 0, 1], [1, 0, 0], size)
    g_obs = np.array([0.0, 0.0, R_EARTH + 1.0])
    ground = cached("ground_allsky_camera" + sfx,
                    lambda: render.render(g_obs, dirs, comps, cam, lower=lower))
    gtot = sum(ground.values()) * valid[..., None]
    gs = exposure.GROUND_ALLSKY.scale()
    plt.imsave(OUT / f"ground_allsky_100pct{sfx}.png", to_image(gtot, gs))
    plt.imsave(OUT / f"ground_allsky_30pct{sfx}.png", to_image(gtot, gs, MASTER_DEFAULT))

    # 4. Profiles: limb columns vs tangent height, and ground zenith-angle curve.
    t = np.concatenate([np.arange(60, 130, 0.25), np.arange(130, 420, 2.0)])
    lp = render.limb_profile(ISS_ALT_KM, comps, t)
    fig, ax = plt.subplots(1, 2, figsize=(13, 5.5), dpi=100)
    for c in comps:
        if c.enabled:
            ax[0].semilogx(lp[c.name], t, label=f"{c.name} (zenith {c.intensity_R:.3g} R)")
    ax[0].set_xlim(1e1, 1e8); ax[0].set_xlabel("Apparent column emission rate (R)")
    ax[0].set_ylabel("Tangent height (km)"); ax[0].legend(fontsize=7); ax[0].grid(alpha=0.3)
    ax[0].set_title("Limb from 420 km, no extinction")
    zen = np.radians(np.arange(0, 90.1, 0.5))
    d = np.stack([np.sin(zen), np.zeros_like(zen), np.cos(zen)], axis=1)
    for low, ls, lab in ((None, "--", "no extinction"), (lower, "-", "with extinction")):
        rgb = render.render(g_obs, d, comps, cam, lower=low)
        Y = {g: rgb[g] @ colour.LUMA for g in rgb}
        for g, col in (("green", "g"), ("red", "r"), ("continuum", "b"), ("OH", "orange")):
            ax[1].plot(np.degrees(zen), Y[g] / Y[g][0], ls, color=col, label=f"{g} {lab}")
    ax[1].set_xlabel("Zenith angle (deg)"); ax[1].set_ylabel("Brightness / zenith (camera luminance)")
    ax[1].set_title("Ground, 40 N: van Rhijn brightening and extinction"); ax[1].legend(fontsize=7)
    ax[1].grid(alpha=0.3)
    fig.tight_layout(); fig.savefig(OUT / f"profiles{sfx}.png"); plt.close(fig)
    print("done")


def _scotopic():
    from airglow_ref import data
    from airglow_ref.constants import KM_SCOTOPIC
    lam, v = data.cie_v_scotopic()
    return lam, KM_SCOTOPIC * np.repeat(v[:, None], 3, axis=1)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--quick", action="store_true")
    main(ap.parse_args().quick)
