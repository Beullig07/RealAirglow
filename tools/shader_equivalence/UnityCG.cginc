// Minimal stand-in for Unity's UnityCG.cginc, used only by equiv.py (glslang compile and
// llvmpipe equivalence test). Unity builds use the real file.
float4 _ProjectionParams;
float4 _ZBufferParams;
#define UNITY_DECLARE_DEPTH_TEXTURE(t) Texture2D t; SamplerState sampler##t
#define UNITY_PROJ_COORD(a) a
#define SAMPLE_DEPTH_TEXTURE_PROJ(t, uv) t.Sample(sampler##t, (uv).xy / (uv).w).r
float Linear01Depth(float z) { return 1.0 / (_ZBufferParams.x * z + _ZBufferParams.y); }
float LinearEyeDepth(float z) { return 1.0 / (_ZBufferParams.z * z + _ZBufferParams.w); }
float4 ComputeScreenPos(float4 pos) { float4 o = pos * 0.5; o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w; o.zw = pos.zw; return o; }
