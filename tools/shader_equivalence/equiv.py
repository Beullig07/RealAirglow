"""Check that a shader change leaves the airglow output unchanged, without Unity or a GPU.

Renders the airglow program of a git revision (default: the 1.0.3 release) and of the working
tree on Mesa's llvmpipe (OpenGL 4.5 core), after translating both the way the macOS/Linux path
does in spirit: glslang (HLSL -> SPIR-V), then SPIRV-Cross (-> GLSL 410; Metal MSL is also
generated, as a compile check). Uniforms are built from GameData/RealAirglow/Config/Earth.cfg
the way AirglowMaterialBinder does. Every keyword combination (colour mode x extinction x
outshine), eight views and all three pass modes are rendered and compared pixel for pixel.

    python3 tools/shader_equivalence/equiv.py [--base v1.0.3] [--timing]

Needs glslangValidator, spirv-cross, Mesa EGL (libegl1, libgl1-mesa-dri) and the Python
packages numpy and moderngl. The Unity harness (tools/build_shaders.sh) stays the test of the
real DirectX 11 / OpenGL / Metal compilers.
"""
import argparse, itertools, json, math, subprocess, sys, tempfile, time
from pathlib import Path
import numpy as np
import moderngl

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
SHADERS = 'unity/RealAirglowShaders/Assets/Shaders'
ap = argparse.ArgumentParser()
ap.add_argument('--base', default='v1.0.3', help='git revision to compare against')
ap.add_argument('--timing', action='store_true', help='also time both on llvmpipe (a rough proxy for GPU work)')
args = ap.parse_args()
CFG = REPO / 'GameData/RealAirglow/Config/Earth.cfg'
TMP = Path(tempfile.mkdtemp(prefix='airglow_equiv_'))


def program_at(rev):
    """The airglow HLSL program at a git revision (or the working tree when rev is None)."""
    def read(path):
        if rev is None:
            p = REPO / path
            return p.read_text() if p.exists() else None
        r = subprocess.run(['git', '-C', str(REPO), 'show', f'{rev}:{path}'], capture_output=True, text=True)
        return r.stdout if r.returncode == 0 else None
    cginc = read(f'{SHADERS}/AirglowPass.cginc')
    if cginc is not None:
        return cginc
    shader = read(f'{SHADERS}/Airglow.shader')   # up to 1.0.3 the program was inline
    if shader is None:
        sys.exit(f'no shader at revision {rev!r} (for a release tag, run git fetch --tags)')
    a = shader.index('#include "UnityCG.cginc"'); b = shader.index('ENDCG')
    return shader[a:b]


SOURCES = {'old': TMP / 'old.hlsl', 'new': TMP / 'new.hlsl'}
SOURCES['old'].write_text(program_at(args.base))
SOURCES['new'].write_text(program_at(None))

def parse_cfg(text):
    root = {'values': [], 'nodes': []}; stack = [root]; pending = None
    for raw in text.splitlines():
        line = raw.split('//')[0].strip()
        if not line: continue
        if line == '{':
            n = {'name': pending, 'values': [], 'nodes': []}; stack[-1]['nodes'].append(n); stack.append(n); continue
        if line == '}': stack.pop(); continue
        if '=' in line:
            k, v = line.split('=', 1); stack[-1]['values'].append((k.strip(), v.strip()))
        else: pending = line
    return root

def val(n, k, d=None):
    for kk, v in n['values']:
        if kk == k: return v
    return d
def arr(n, k): return [float(x) for x in val(n, k, '').split(',') if x.strip()]

cfg = parse_cfg(CFG.read_text())
body = cfg['nodes'][0]
lower = [n for n in body['nodes'] if n['name'] == 'LOWER_ATMOSPHERE'][0]
layers = [n for n in body['nodes'] if n['name'] == 'LAYER']
SCI = {'green': (0.1, 1, 0.1), 'red': (1, 0.1, 0.1), 'OH': (1, 0.6, 0), 'Na': (1, 1, 0), 'continuum': (0.3, 0.5, 1), 'O2': (0.8, 0.2, 1)}
LUMA = (0.2126729, 0.7151522, 0.0721750)
MAXL, MAXB = 12, 12

def mode_colour(mode, cam, o, cie, scot, sc):
    if mode == 'EYE': return [cie[o], cie[o+1], cie[o+2], scot]
    if mode == 'SCIENCE':
        y = sum(cam[o+i] * LUMA[i] for i in range(3)); return [sc[0]*y, sc[1]*y, sc[2]*y, 0]
    return [cam[o], cam[o+1], cam[o+2], 0]

def model_uniforms(mode, master=0.015, red=5.0):
    u = {}
    shape = np.zeros((MAXL, 4)); colour = np.zeros((MAXL, 4)); bc = np.zeros((MAXL*MAXB, 4)); be = np.zeros((MAXL*MAXB, 4))
    bins = 0
    for i, L in enumerate(layers[:MAXL]):
        lo, hi, pk = float(val(L, 'hwhmLow_km')), float(val(L, 'hwhmHigh_km')), float(val(L, 'peak_km'))
        norm = 0.5 * math.sqrt(math.pi / math.log(2)) * (lo + hi)
        g = val(L, 'group'); disp = master * (red if g == 'red' else 1.0)
        en = val(L, 'enabled', 'True') != 'False'
        shape[i] = [pk, lo, hi, float(val(L, 'intensity_R')) * disp / norm if en else 0]
        sc = SCI.get(g, (0, 0, 0))
        colour[i] = mode_colour(mode, arr(L, 'cameraRGB'), 0, arr(L, 'cieRGB'), float(val(L, 'scotopicY')), sc)
        bs = arr(L, 'binScotopicY'); nb = min(len(bs), MAXB); bins = max(bins, nb)
        bcam, bcie, bext = arr(L, 'binCameraRGB'), arr(L, 'binCieRGB'), arr(L, 'binExtinction')
        for k in range(nb):
            bc[i*MAXB+k] = mode_colour(mode, bcam, 3*k, bcie, bs[k], sc)
            be[i*MAXB+k] = [bext[3*k], bext[3*k+1], bext[3*k+2], 0]
    p = arr(lower, 'airLogDensityPoly') + [0]*6
    top = float(val(lower, 'top_km'))
    u.update(_LayerCount=len(layers), _LayerShape=shape, _LayerColour=colour, _BinCount=bins, _BinColour=bc, _BinExtinction=be,
             _AirPoly0=p[0:4], _AirPoly1=[p[4], p[5], top, 0],
             _Ozone=[float(val(lower, 'ozoneColumn_DU')) * 2.6867e20, float(val(lower, 'ozonePeak_km')), float(val(lower, 'ozoneFwhm_km')) / 2.354820045, 0],
             _Aerosol=[float(val(lower, 'aerosolTau550')), float(val(lower, 'aerosolScaleHeight_km')), 0, 0])
    metered = 12.5 * 1.4 * 1.4 / (1.3 * 12800)
    u['_Outshine'] = [metered, 0.29 * 134634 / math.pi, 1.08e5, 10.0]
    u['_TwilightFit'] = [11.84, 1.518, -0.057, 95.0]
    return u

def view_uniforms(alt, el_deg, vfov, aspect, sun_deg, R=6371.0, az_deg=0.0):
    el = math.radians(el_deg); az = math.radians(az_deg)
    up = np.array([1.0, 0, 0])
    fwd = np.array([math.sin(el), math.cos(el) * math.cos(az), math.cos(el) * math.sin(az)])
    right = np.cross(up, fwd); right /= np.linalg.norm(right)
    upv = np.cross(fwd, right)
    th = math.tan(math.radians(vfov) / 2); tw = th * aspect
    corners = [fwd - right*tw - upv*th, fwd - right*tw + upv*th, fwd + right*tw + upv*th, fwd + right*tw - upv*th]
    sa = math.radians(sun_deg)
    return dict(_FrustumRays=[list(c) + [0] for c in corners], _CamPosKm=[R + alt, 0, 0], _CamForward=list(fwd),
                _WorldUnitKm=0.001, _RadiusKm=R, _SunDir=[math.cos(sa), math.sin(sa), 0],
                _ProjectionParams=[1, 0.1, 1000, 0.001], _ZBufferParams=[0, 1, 0, 1], _Exposure=1.0, _GammaSpace=0.0)

def compile_variant(src, defines):
    out = {}
    for st in ('vert', 'frag'):
        spv = TMP / f'tmp.{st}.spv'
        r = subprocess.run(['glslangValidator', '-D', '-V', '-S', st, '-e', st, '-I' + str(HERE), *['-D' + d for d in defines], str(src), '-o', str(spv)], capture_output=True, text=True)
        if r.returncode: raise SystemExit(r.stdout + r.stderr)
        glsl = subprocess.run(['spirv-cross', str(spv), '--version', '410', '--no-es'], capture_output=True, text=True, check=True).stdout
        msl = subprocess.run(['spirv-cross', str(spv), '--msl', '--msl-version', '20000'], capture_output=True, text=True)
        if msl.returncode: raise SystemExit('MSL: ' + msl.stderr)
        out[st] = glsl
        if st == 'frag':
            out['reflect'] = json.loads(subprocess.run(['spirv-cross', str(spv), '--reflect'], capture_output=True, text=True, check=True).stdout)
    return out

def pack(reflect, values):
    ubo = reflect['ubos'][0]; t = reflect['types'][ubo['type']]
    buf = bytearray(ubo['block_size'])
    for m in t['members']:
        v = values.get(m['name'])
        if v is None: continue
        a = np.asarray(v, dtype=np.float64)
        dt = np.int32 if m['type'] == 'int' else np.float32
        if 'array' in m:
            stride = m['array_stride']
            for i, row in enumerate(a.reshape(m['array'][0], -1)):
                b = np.asarray(row, dtype=dt).tobytes(); buf[m['offset'] + i*stride: m['offset'] + i*stride + len(b)] = b
        else:
            b = np.asarray(a.ravel(), dtype=dt).tobytes(); buf[m['offset']: m['offset'] + len(b)] = b
    return bytes(buf)

ctx = moderngl.create_standalone_context(backend='egl', require=410)
W, H = 300, 200
fbo = ctx.framebuffer(color_attachments=[ctx.texture((W, H), 4, dtype='f4')])
quad = ctx.buffer(np.array([-1, -1, 0, -1, 1, 0, 1, 1, 0, -1, -1, 0, 1, 1, 0, 1, -1, 0], dtype='f4'))

VIEWS = [  # alt km, elevation deg, vfov deg, sun angle deg (180 = midnight), azimuth
    ('iss_limb', 420, -16.18, 6.9, 180, 0), ('iss_wide', 420, -28.8, 53.1, 180, 0),
    ('ground_horizon', 0.001, 20.0, 50.0, 180, 0), ('iss_terminator', 420, -28.8, 53.1, 118, 0),
    ('ground_twilight', 0.5, 5.0, 70.0, 100, 40), ('high_orbit', 20000, -80.0, 40.0, 150, 0),
    ('in_layer', 95, 2.0, 80.0, 170, 20), ('looking_up_space', 420, 45.0, 60.0, 180, 0),
]

def render(prog_src, ubo_bytes, ubo):
    prog = ctx.program(vertex_shader=prog_src['vert'], fragment_shader=prog_src['frag'])
    names = [n for n in prog if isinstance(prog[n], moderngl.Attribute)]
    vao = ctx.vertex_array(prog, [(quad, '3f', names[0])])
    ubo.write(ubo_bytes); ubo.bind_to_uniform_block(0)
    fbo.use(); fbo.clear(0, 0, 0, 0); ctx.disable(moderngl.BLEND)
    vao.render()
    return np.frombuffer(fbo.color_attachments[0].read(), dtype='f4').reshape(H, W, 4).copy()

worst = 0.0; total = 0; exact = 0; ok = True
for mode, ext, osh in itertools.product(('CAMERA', 'EYE', 'SCIENCE'), (False, True), (False, True)):
    defines = ['MODE_' + mode] + (['EXTINCTION_ON'] if ext else []) + (['OUTSHINE_ON'] if osh else [])
    progs = {v: compile_variant(SOURCES[v], defines) for v in ('old', 'new')}
    mu = model_uniforms(mode)
    for name, alt, el, fov, sun, az in VIEWS:
        vu = view_uniforms(alt, el, fov, W / H, sun, az_deg=az)
        res = {}
        for pm in (0.0, 1.0, 2.0):
            for v in ('old', 'new'):
                vals = dict(mu, **vu, _PassMode=pm)
                ubo = ctx.buffer(reserve=progs[v]['reflect']['ubos'][0]['block_size'])
                res[v, pm] = render(progs[v], pack(progs[v]['reflect'], vals), ubo)
                ubo.release()
            a, b = res['old', pm], res['new', pm]
            if not (np.isfinite(a).all() and np.isfinite(b).all()): print('non-finite', mode, name, pm); ok = False
            peak = max(np.abs(a).max(), 1e-38)
            d = np.abs(a - b).max() / peak
            same = np.array_equal(a, b)
            total += 1; exact += same; worst = max(worst, d)
            if not same: print(f'{"+".join(defines):34s} {name:18s} pass {pm:.0f}: max diff {d:.2e} of peak, {np.count_nonzero((a != b).any(-1))} px differ')
        back = np.count_nonzero(res['old', 2.0][..., :3].max(-1) > 0); front = np.count_nonzero(res['old', 1.0][..., :3].max(-1) > 0)
        lit = np.count_nonzero(res['old', 0.0][..., :3].max(-1) > 0)
        print(f'{"+".join(defines):34s} {name:18s} lit px {lit:6d} front {front:6d} back {back:6d}  peak {np.abs(res["old", 0.0]).max():.3e}', flush=True)
print(f'{exact}/{total} renders bit-identical; worst relative difference {worst:.2e}')
if not ok or exact != total:
    sys.exit(1)
if not args.timing:
    sys.exit(0)

# Timing on llvmpipe (a CPU rasteriser: a rough proxy for GPU work, not a GPU benchmark).
defines = ['MODE_CAMERA', 'EXTINCTION_ON', 'OUTSHINE_ON']
progs = {v: compile_variant(SOURCES[v], defines) for v in ('old', 'new')}
mu = model_uniforms('CAMERA')
for name, alt, el, fov, sun, az in VIEWS[:4] + VIEWS[5:6]:
    vu = view_uniforms(alt, el, fov, W / H, sun, az_deg=az)
    line = f'{name:18s}'
    for label, passes in (('flight (front+back)', (1.0, 2.0)), ('map (single)', (0.0,))):
        t = {}
        for v in ('old', 'new'):
            prog = ctx.program(vertex_shader=progs[v]['vert'], fragment_shader=progs[v]['frag'])
            names = [n for n in prog if isinstance(prog[n], moderngl.Attribute)]
            vao = ctx.vertex_array(prog, [(quad, '3f', names[0])])
            ubos = [ctx.buffer(pack(progs[v]['reflect'], dict(mu, **vu, _PassMode=pm))) for pm in passes]
            best = 1e9
            for rep in range(5):
                ctx.finish(); t0 = time.perf_counter()
                for u in ubos:
                    u.bind_to_uniform_block(0); fbo.use(); vao.render()
                ctx.finish(); best = min(best, time.perf_counter() - t0)
            t[v] = best
        line += f'  {label}: {t["old"]*1e3:7.1f} -> {t["new"]*1e3:7.1f} ms ({t["old"]/t["new"]:.2f}x)'
    print(line)
