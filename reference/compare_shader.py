"""Compare the Unity shader's output (written by the ComparisonHarness editor script) with the
reference model for the same rays (docs/SPEC.md deliverable 5).

The harness renders in camera mode with exposure 1 and no sRGB encoding, so each pixel is
linear sRGB with luminance in cd m^-2 after gamut mapping. The reference renders the same
per-pixel rays (rebuilt from the frustum corners the harness used) with its full spectral
model, then applies the same gamut mapping.

Expected differences, all documented in docs/PHYSICS.md: 16-point vs 128-point quadrature
(up to ~2 % on rays crossing a layer below its peak), per-channel effective extinction
coefficients vs per-line transmittance, and float32 arithmetic on the GPU.
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np

from airglow_ref import atmosphere, colour, layers, render
from airglow_ref.constants import R_EARTH

HARNESS = Path(__file__).resolve().parent / "output" / "harness"
# Tolerance on pixels brighter than 1 % of the view's maximum luminance.
TOL_MEDIAN = 0.01
TOL_P95 = 0.03


def rays(meta):
    W, H = meta["width"], meta["height"]
    bl, tl, tr, br = (np.array(r, float) for r in meta["frustum_rays"])
    u = (np.arange(W) + 0.5) / W
    v = (np.arange(H) + 0.5) / H          # row 0 = bottom
    U, V = np.meshgrid(u, v)
    bottom = bl + U[..., None] * (br - bl)
    top = tl + U[..., None] * (tr - tl)
    d = bottom + V[..., None] * (top - bottom)
    return d / np.linalg.norm(d, axis=-1, keepdims=True)


def main():
    comps = layers.default_components()
    cam = colour.camera_response()
    ok = True
    for meta_path in sorted(HARNESS.glob("*.json")):
        meta = json.loads(meta_path.read_text())
        W, H = meta["width"], meta["height"]
        shader = np.fromfile(meta_path.with_suffix(".f32"), dtype=np.float32).reshape(H, W, 4)[..., :3]
        obs = np.array([R_EARTH + meta["alt_km"] * 1e3, 0.0, 0.0])
        lower = atmosphere.LowerAtmosphere() if meta["extinction"] else None
        ref = colour.gamut_map(sum(render.render(obs, rays(meta), comps, cam, lower=lower).values()))
        Ys, Yr = shader @ colour.LUMA, ref @ colour.LUMA
        m = Yr > 0.01 * Yr.max()
        rel = np.abs(Ys[m] / Yr[m] - 1)
        rgb_rel = np.abs(shader[m] - ref[m]).max(axis=-1) / Yr[m]
        med, p95, mx = np.median(rel), np.percentile(rel, 95), rel.max()
        split = meta.get("split_error", float("nan"))
        # Each pass is gamut-mapped on its own (a nonlinear step), so front + back differs from
        # the single pass by a tiny amount where a pixel gets both and has negative channels.
        passed = med <= TOL_MEDIAN and p95 <= TOL_P95 and split <= 1e-3
        ok &= passed
        print(f"{meta_path.stem:16s} pixels {m.sum():6d}  luminance rel. error median {med:.2%}  p95 {p95:.2%}  "
              f"max {mx:.2%}  |  colour error (max channel / Y) p95 {np.percentile(rgb_rel, 95):.2%}  "
              f"front+back split error {split:.1e}  "
              f"{'PASS' if passed else 'FAIL'}")
        np.save(HARNESS / f"{meta_path.stem}_reference.npy", ref)
    print("overall:", "PASS" if ok else "FAIL", f"(tolerance: median <= {TOL_MEDIAN:.0%}, p95 <= {TOL_P95:.0%})")


if __name__ == "__main__":
    main()
