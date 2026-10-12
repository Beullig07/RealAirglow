// RealAirglow airglow program, included by both SubShaders of Airglow.shader (see there).
#include "UnityCG.cginc"

#define MAX_LAYERS 12
#define MAX_BINS 12
#define GL_N 16
#define LN2 0.69314718

static const float GL_X[GL_N] = {
    -0.9894009350, -0.9445750231, -0.8656312024, -0.7554044084, -0.6178762444, -0.4580167777,
    -0.2816035508, -0.0950125098,  0.0950125098,  0.2816035508,  0.4580167777,  0.6178762444,
     0.7554044084,  0.8656312024,  0.9445750231,  0.9894009350 };
static const float GL_W[GL_N] = {
     0.0271524594,  0.0622535239,  0.0951585117,  0.1246289713,  0.1495959888,  0.1691565194,
     0.1826034150,  0.1894506105,  0.1894506105,  0.1826034150,  0.1691565194,  0.1495959888,
     0.1246289713,  0.0951585117,  0.0622535239,  0.0271524594 };

UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

float4 _FrustumRays[4];     // world-space corner rays: bottom-left, top-left, top-right, bottom-right
float3 _CamPosKm;           // camera position relative to the planet centre, km
float3 _CamForward;         // camera forward, world space, unit
float _WorldUnitKm;         // kilometres per world unit (depth buffer conversion)
float _RadiusKm;            // planet radius, km
float3 _SunDir;             // unit vector, planet centre -> Sun
// Local outshining (AutoExposure.OutshineWeight): x metered night luminance cd m^-2,
// y sunlit-surface luminance per unit cos(Sun zenith) cd m^-2, z magnitude zero point
// cd m^-2, w twilight fit range deg. _TwilightFit: m0, m1, m2, start zenith deg.
float4 _Outshine;
float4 _TwilightFit;
int _LayerCount;
float4 _LayerShape[MAX_LAYERS];   // x peak km, y HWHM below, z HWHM above, w column scale (R / unit profile integral)
float4 _LayerColour[MAX_LAYERS];  // rgb: linear sRGB cd m^-2 per R (mode dependent); a: scotopic cd m^-2 per R
// Extinction wavelength bins, index layer * MAX_BINS + bin: colour weights as above,
// and (sigma Rayleigh m^2, sigma O3 m^2, aerosol spectral factor).
int _BinCount;
float4 _BinColour[MAX_LAYERS * MAX_BINS];
float4 _BinExtinction[MAX_LAYERS * MAX_BINS];
float4 _AirPoly0;           // ln(n / m^-3) = sum c_k (z/top)^k: c0..c3
float4 _AirPoly1;           // c4, c5, top km, unused
float4 _Ozone;              // x column (m^-2), y peak km, z sigma km
float4 _Aerosol;            // x tau(550 nm), y scale height km
float _Exposure;            // display scale: luminance (cd m^-2) -> fraction of saturation
float _GammaSpace;          // 1: encode with the sRGB curve (gamma colour space)
// 0: all emission. 1: only emission reaching the camera without crossing the lower
// atmosphere (drawn after clouds, so it lies in front of them). 2: only emission
// behind the lower atmosphere (drawn before clouds, so clouds can hide it).
float _PassMode;

struct appdata { float4 vertex : POSITION; };
struct v2f
{
    float4 pos : SV_POSITION;
    float3 ray : TEXCOORD0;
    float4 screenPos : TEXCOORD1;
};

v2f vert(appdata v)
{
    v2f o;
    // Full-screen quad in NDC. Follow Unity's render-texture flip so the quad lands
    // the same way up as the scene geometry; ComputeScreenPos then matches the depth texture.
    o.pos = float4(v.vertex.x, v.vertex.y * _ProjectionParams.x, 0.5, 1.0);
    o.screenPos = ComputeScreenPos(o.pos);
    float2 uv = v.vertex.xy * 0.5 + 0.5;
    float3 bottom = lerp(_FrustumRays[0].xyz, _FrustumRays[3].xyz, uv.x);
    float3 top = lerp(_FrustumRays[1].xyz, _FrustumRays[2].xyz, uv.x);
    o.ray = lerp(bottom, top, uv.y);
    return o;
}

// Ray-sphere intersection about the point of closest approach (stable at grazing incidence).
bool Sphere(float3 o, float3 d, float r, out float t0, out float t1)
{
    float b = dot(o, d);
    float h = length(o - d * b);
    t0 = 0; t1 = 0;
    if (h >= r) return false;
    float dt = sqrt((r - h) * (r + h));
    t0 = -b - dt; t1 = -b + dt;
    return true;
}

// Up to two path intervals with rLo <= |o + t d| <= rHi, 0 <= t <= tEnd.
int Band(float3 o, float3 d, float rLo, float rHi, float tEnd, out float2 i0, out float2 i1)
{
    i0 = 0; i1 = 0;
    float o0, o1, n0, n1;
    if (!Sphere(o, d, rHi, o0, o1)) return 0;
    int n = 0;
    float2 a, b;
    if (Sphere(o, d, rLo, n0, n1)) { a = float2(o0, n1); b = float2(n0, o1); }
    else { a = float2(o0, 1e30); b = float2(o1, -1e30); }
    [unroll] for (int k = 0; k < 2; k++)
    {
        float s = max(a[k], 0.0), e = min(b[k], tEnd);
        if (e > s) { if (n == 0) i0 = float2(s, e); else i1 = float2(s, e); n++; }
    }
    return n;
}

float Profile(float z, float4 shape)
{
    float h = z < shape.x ? shape.y : shape.z;
    float u = (z - shape.x) / h;
    return exp(-LN2 * u * u);
}

// How much sunlight at point p (km) outshines its airglow: night exposure / local
// background exposure, from the Sun's zenith distance at p (docs/PHYSICS.md §5).
// r = length(p), already known to the caller.
float Outshine(float3 p, float r)
{
    float cz = clamp(dot(p, _SunDir) / r, -1, 1);
    float zen = degrees(acos(cz));
    float x = clamp(zen - _TwilightFit.w, 0, _Outshine.w);
    float mag = _TwilightFit.x + x * (_TwilightFit.y + x * _TwilightFit.z);
    float L = _Outshine.z * exp2(-0.4 * 3.3219281 * mag);   // 10^(-0.4 m)
    if (zen < _TwilightFit.w) L = max(L, _Outshine.y * max(cz, 0));
    return min(1, _Outshine.x / L);
}

float AirDensity(float z)
{
    float x = z / _AirPoly1.z;
    float l = _AirPoly0.x + x * (_AirPoly0.y + x * (_AirPoly0.z + x * (_AirPoly0.w + x * (_AirPoly1.x + x * _AirPoly1.y))));
    return exp(l);
}

float OzoneDensity(float z)
{
    float u = (z - _Ozone.y) / _Ozone.z;
    return _Ozone.x / (_Ozone.z * 1e3 * 2.5066283) * exp(-0.5 * u * u);
}

float AerosolExt(float z)
{
    float H = _Aerosol.y * 1e3;
    return _Aerosol.x / H * exp(-max(z, 0) * 1e3 / H);
}

float3 GamutMap(float3 rgb)
{
    const float3 luma = float3(0.2126729, 0.7151522, 0.0721750);
    float Y = dot(rgb, luma);
    float mn = min(rgb.r, min(rgb.g, rgb.b));
    float t = mn < 0 ? saturate(mn / (mn - Y)) : 0;
    return lerp(rgb, Y.xxx, t);
}

float3 SrgbEncode(float3 x)
{
    x = max(x, 0);
    return x <= 0.0031308 ? 12.92 * x : 1.055 * pow(x, 1.0 / 2.4) - 0.055;
}

float4 frag(v2f i) : SV_Target
{
    float3 d = normalize(i.ray);
    float3 o = _CamPosKm;
    // An observer below the reference sphere (terrain below the mean radius) is
    // treated as standing on it, so rays cannot see through the planet.
    if (length(o) < _RadiusKm + 1e-3) o = normalize(o) * (_RadiusKm + 1e-3);
    float tEnd = 1e30;
    float p0, p1;
    if (Sphere(o, d, _RadiusKm, p0, p1) && p0 > 0) tEnd = p0;
#if defined(USE_DEPTH)
    float raw = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
    if (Linear01Depth(raw) < 0.9999)
        tEnd = min(tEnd, LinearEyeDepth(raw) / dot(d, _CamForward) * _WorldUnitKm);
#endif

    // Slant absorber columns over the lower-atmosphere shell.
    float2 a0, a1;
    float3 absC0 = 0, absC1 = 0;   // (air m^-2, ozone m^-2, aerosol tau)
    int nAbs = Band(o, d, _RadiusKm - 1e-3, _RadiusKm + _AirPoly1.z, tEnd, a0, a1);
    // The behind-atmosphere pass draws nothing on a ray that never crosses the lower
    // atmosphere (most of the sky seen from orbit): the result would be exactly 0.
    if (_PassMode > 1.5 && nAbs == 0) return 0;
#if defined(EXTINCTION_ON)
    // The columns only dim emission behind the lower atmosphere, which the in-front pass
    // (mode 1) does not draw, so that pass skips them.
    bool needAbs = _PassMode < 0.5 || _PassMode > 1.5;
    [unroll] for (int j = 0; j < 2; j++)
    {
        if (needAbs && j < nAbs)
        {
        float2 iv = j == 0 ? a0 : a1;
        float half_ = 0.5 * (iv.y - iv.x), mid = 0.5 * (iv.y + iv.x);
        float3 acc = 0;
        [unroll] for (int k = 0; k < GL_N; k++)
        {
            float z = length(o + d * (mid + half_ * GL_X[k])) - _RadiusKm;
            float w = half_ * GL_W[k] * 1e3;
            acc += float3(AirDensity(z), OzoneDensity(z), AerosolExt(z)) * w;
        }
        if (j == 0) absC0 = acc; else absC1 = acc;
        }
    }
#endif

    float4 sum = 0;
    [loop] for (int l = 0; l < _LayerCount; l++)
    {
        float4 shape = _LayerShape[l];
        if (shape.w <= 0) continue;
        float2 e0, e1;
        float rLo = _RadiusKm + max(shape.x - 4.5 * shape.y, 0);
        float rHi = _RadiusKm + shape.x + 4.5 * shape.z;
        int n = Band(o, d, rLo, rHi, tEnd, e0, e1);
        [unroll] for (int k = 0; k < 2; k++)
        {
            if (k < n)
            {
            float2 iv = k == 0 ? e0 : e1;
            bool behind0 = nAbs > 0 && a0.y <= iv.x + 1e-3;
            bool behind1 = nAbs > 1 && a1.y <= iv.x + 1e-3;
            bool behind = behind0 || behind1;
            // An interval on the side of the lower atmosphere this pass does not draw adds
            // exactly 0, so it is not integrated at all.
            bool draw = !(_PassMode > 0.5 && (behind ? _PassMode < 1.5 : _PassMode > 1.5));
            [branch] if (draw)
            {
                float half_ = 0.5 * (iv.y - iv.x), mid = 0.5 * (iv.y + iv.x);
                float col = 0;
                [unroll] for (int q = 0; q < GL_N; q++)
                {
                    float3 p = o + d * (mid + half_ * GL_X[q]);
                    float r = length(p);
                    float w = Profile(r - _RadiusKm, shape) * GL_W[q];
                #if defined(OUTSHINE_ON)
                    w *= Outshine(p, r);
                #endif
                    col += w;
                }
                col *= half_ * shape.w;   // rayleighs (scale includes the display multiplier)
                float3 c = 0;   // absorber columns between the camera and this interval
            #if defined(EXTINCTION_ON)
                if (behind0) c += absC0;
                if (behind1) c += absC1;
            #endif
                if (c.x <= 0)
                    sum += col * _LayerColour[l];
                else
                {
                    [loop] for (int bi = 0; bi < _BinCount; bi++)
                    {
                        float4 ext = _BinExtinction[l * MAX_BINS + bi];
                        sum += col * exp(-ext.x * c.x - ext.y * c.y - ext.z * c.z) * _BinColour[l * MAX_BINS + bi];
                    }
                }
            }
            }
        }
    }

    float3 rgb = sum.rgb;
#if defined(MODE_EYE)
    // CIE 191:2010 mesopic luminance; chroma desaturated toward white by (1 - m).
    const float3 luma = float3(0.2126729, 0.7151522, 0.0721750);
    float Lp = max(dot(rgb, luma), 1e-30), Ls = max(sum.a, 0);
    float v0 = 683.0 / 1700.0, m = 0.5, Lmes = 0;
    [unroll] for (int it = 0; it < 12; it++)
    {
        Lmes = (m * Lp + (1 - m) * Ls * v0) / (m + (1 - m) * v0);
        m = saturate(0.7670 + 0.3334 * log10(max(Lmes, 1e-30)));
    }
    Lmes = (m * Lp + (1 - m) * Ls * v0) / (m + (1 - m) * v0);
    rgb = (m * (rgb / Lp) + (1 - m)) * Lmes;
#endif
    rgb = GamutMap(rgb) * _Exposure;
    if (_GammaSpace > 0.5) rgb = SrgbEncode(rgb);
    return float4(rgb, 0);
}
