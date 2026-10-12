"""Compare two runs of the Unity comparison harness pixel for pixel, e.g. the 1.0.3 shader
against a new one, or DirectX 11 against OpenGL Core.

    python compare_harness_runs.py <baseline dir> [<new dir>]     # new dir: output/harness

Every *.f32 render present in both folders (full, in-front and behind passes) is compared.
Reports how many renders are bit-identical and the largest difference as a fraction of the
render's brightest channel value; an 8-bit display step is 1/255 = 3.9e-3 of full scale.
"""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np

HARNESS = Path(__file__).resolve().parent / "output" / "harness"


def main():
    base = Path(sys.argv[1])
    new = Path(sys.argv[2]) if len(sys.argv) > 2 else HARNESS
    files = sorted(p.name for p in base.glob("*.f32") if (new / p.name).exists())
    if not files:
        sys.exit(f"no common *.f32 renders in {base} and {new}")
    identical, worst = 0, 0.0
    for name in files:
        a = np.fromfile(base / name, dtype=np.float32)
        b = np.fromfile(new / name, dtype=np.float32)
        if a.shape != b.shape:
            sys.exit(f"{name}: sizes differ")
        peak = float(np.abs(a).max())
        diff = float(np.abs(a - b).max()) / peak if peak > 0 else float(np.abs(b).max())
        same = np.array_equal(a, b)
        identical += same
        worst = max(worst, diff)
        print(f"{name:28s} {'identical' if same else f'max difference {diff:.1e} of peak'}")
    print(f"{identical}/{len(files)} renders bit-identical; largest difference {worst:.1e} of peak "
          f"({'invisible: below 1 % of an 8-bit step' if worst < 3.9e-5 else 'CHECK'})")


if __name__ == "__main__":
    main()
