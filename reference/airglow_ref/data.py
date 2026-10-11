"""Third-party data, downloaded on first use into reference/data/cache/ (gitignored).

Raw tables are never committed (owner decision 2026-10-11, docs/PHYSICS.md §14). Each loader
names the dataset, its licence and its citation key.
"""
from __future__ import annotations

import functools
import io
import urllib.request
import zipfile
from pathlib import Path

import numpy as np

CACHE = Path(__file__).resolve().parent.parent / "data" / "cache"

_SOURCES = {
    # CIE datasets, CC BY-SA 4.0 [CIE-CMF], [CIE-V].
    "CIE_xyz_1931_2deg.csv": "https://files.cie.co.at/Publications-datasets/CIE_xyz_1931_2deg.csv",
    "CIE_sle_photopic.csv": "https://files.cie.co.at/Publications-datasets/CIE_sle_photopic.csv",
    "CIE_sle_scotopic.csv": "https://files.cie.co.at/Publications-datasets/CIE_sle_scotopic.csv",
    # PALACE v1.0 data, CC BY 4.0 [PALACE-data]. Only the FITS data files are extracted;
    # the GPL-3.0 code in the same archive is not used.
    "PALACE.zip": "https://zenodo.org/records/14064023/files/PALACE.zip?download=1",
    # Jiang et al. 2013 camera spectral sensitivities, CC BY-NC-SA 4.0 [Jiang2013].
    "camspec_database.txt": "https://zenodo.org/records/3245883/files/camspec_database.txt?download=1",
    # TSIS-1 Hybrid Solar Reference Spectrum v2 (Coddington et al. 2021, 2023 update).
    "tsis1_hsrs_v2.nc": "https://lasp.colorado.edu/lisird/resources/lasp/hsrs/v2/"
                        "hybrid_reference_spectrum_c2022-11-30_with_unc.nc",
    # O3 absorption cross-sections, Serdyuchenko et al. 2014 / Gorshelev et al. 2014 (IUP Bremen).
    "serdyuchenko_o3.dat": "https://www.iup.uni-bremen.de/gruppen/molspec/downloads/"
                           "serdyuchenkogorshelev5digits.dat",
    # NASA astronaut photographs (public domain), Gateway to Astronaut Photography of Earth.
    "ISS047-E-1114.JPG": "https://eol.jsc.nasa.gov/DatabaseImages/ESC/large/ISS047/ISS047-E-1114.JPG",
    "ISS072-E-896652.JPG": "https://eol.jsc.nasa.gov/DatabaseImages/ESC/large/ISS072/ISS072-E-896652.JPG",
}


def fetch(name: str) -> Path:
    """Return the cached path of a dataset, downloading it on first use."""
    CACHE.mkdir(parents=True, exist_ok=True)
    path = CACHE / name
    if not path.exists():
        url = _SOURCES[name]
        req = urllib.request.Request(url, headers={"User-Agent": "RealAirglow-reference/0.1"})
        with urllib.request.urlopen(req, timeout=600) as resp:
            data = resp.read()
        tmp = path.with_suffix(path.suffix + ".part")
        tmp.write_bytes(data)
        tmp.replace(path)
    return path


@functools.cache
def cie_cmf_1931():
    """CIE 1931 2-degree colour-matching functions: (lambda_nm, xyz[N, 3]), 360-830 nm."""
    d = np.loadtxt(fetch("CIE_xyz_1931_2deg.csv"), delimiter=",")
    return d[:, 0], d[:, 1:4]


@functools.cache
def cie_v_photopic():
    """CIE photopic luminous efficiency V(lambda): (lambda_nm, V)."""
    d = np.loadtxt(fetch("CIE_sle_photopic.csv"), delimiter=",")
    return d[:, 0], d[:, 1]


@functools.cache
def cie_v_scotopic():
    """CIE scotopic luminous efficiency V'(lambda): (lambda_nm, V')."""
    d = np.loadtxt(fetch("CIE_sle_scotopic.csv"), delimiter=",")
    return d[:, 0], d[:, 1]


def _palace_member(member: str) -> Path:
    out = CACHE / "palace" / Path(member).name
    if not out.exists():
        out.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(fetch("PALACE.zip")) as zf:
            out.write_bytes(zf.read(member))
    return out


@functools.cache
def palace_lines():
    """PALACE line list [Noll2025]: dict of arrays.

    Keys: lam_nm (vacuum), I_R (zenith, annual nocturnal mean, 100 sfu, no extinction),
    chem, hlayer_km (reference layer height), varID (variability class), ID.
    """
    from astropy.io import fits
    d = fits.getdata(_palace_member("PALACE/src/palace/data/palace_lines.fits"), 1)
    return {
        "lam_nm": np.asarray(d["lam"], float) * 1e3,
        "I_R": np.asarray(d["I"], float),
        "chem": np.char.strip(np.asarray(d["chem"]).astype(str)),
        "hlayer_km": np.asarray(d["hlayer"], float),
        "varID": np.char.strip(np.asarray(d["varID"]).astype(str)),
        "ID": np.char.strip(np.asarray(d["ID"]).astype(str)),
    }


@functools.cache
def palace_continuum():
    """PALACE continuum components in R nm^-1 (zenith, 100 sfu) [Noll2025] §5.

    Returns (lambda_nm vacuum, {"HO2": ..., "FeO": ..., "O2Ac": ...}). The column order
    fcont1/2/3 = HO2 / FeO-like / O2 (UVB) was identified from the spectral peaks
    (1510 nm, 597 nm, 322 nm), matching Fig. 8 of the paper.
    """
    from astropy.io import fits
    d = fits.getdata(_palace_member("PALACE/src/palace/data/palace_cont.fits"), 1)
    lam = np.asarray(d["lam"], float) * 1e3
    return lam, {"HO2": np.asarray(d["fcont1"], float),
                 "FeO": np.asarray(d["fcont2"], float),
                 "O2Ac": np.asarray(d["fcont3"], float)}


@functools.cache
def camera_sensitivity(camera: str = "Nikon D3"):
    """Measured RGB spectral sensitivity [Jiang2013]: (lambda_nm, rgb[N, 3]).

    400-720 nm in 10 nm steps; each channel is normalised to a peak of 1 in the source,
    so absolute channel gains (white balance) are not meaningful.
    """
    lines = fetch("camspec_database.txt").read_text().splitlines()
    for i, line in enumerate(lines):
        if line.strip() == camera:
            rgb = np.array([[float(v) for v in lines[i + k].split()] for k in (1, 2, 3)]).T
            lam = 400.0 + 10.0 * np.arange(rgb.shape[0])
            return lam, rgb
    raise KeyError(camera)


@functools.cache
def solar_spectrum():
    """TSIS-1 HSRS v2 solar spectral irradiance at 1 AU: (lambda_nm vacuum, W m^-2 nm^-1)."""
    import netCDF4
    with netCDF4.Dataset(fetch("tsis1_hsrs_v2.nc")) as ds:
        lam = np.asarray(ds["Vacuum Wavelength"][:], float)
        ssi = np.asarray(ds["SSI"][:], float)
    return lam, ssi


@functools.cache
def ozone_cross_section(temperature_K: float = 223.0):
    """O3 absorption cross-section, Serdyuchenko et al. 2014: (lambda_nm vacuum, sigma m^2).

    Columns are 293 K down to 193 K in 10 K steps; the nearest tabulated temperature is used.
    """
    raw = fetch("serdyuchenko_o3.dat").read_text().splitlines()
    rows = [r for r in raw[45:] if r.strip()]
    d = np.loadtxt(io.StringIO("\n".join(rows)))
    temps = 293.0 - 10.0 * np.arange(11)
    col = 1 + int(np.argmin(np.abs(temps - temperature_K)))
    return d[:, 0], d[:, col] * 1e-4
