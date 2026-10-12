// RealAirglow: physically based nightglow, full-screen additive pass.
//
// Every per-pixel quantity is computed from the uniforms set by the plugin, which come from
// GameData/RealAirglow/Config/Earth.cfg (exported by the reference model; docs/PHYSICS.md).
// Units: positions in km relative to the planet centre; columns in rayleighs; colours in
// linear sRGB with luminance in cd m^-2 per rayleigh. The math mirrors
// reference/airglow_ref/geometry.py and src/Core/AirglowModel.cs.
//
// The program is in AirglowPass.cginc. Two SubShaders compile the same program: DirectX 11 at
// shader model 5.0 (as in 1.0.0-1.0.3), and every other API (OpenGL Core on Linux, Windows
// and macOS; Metal on macOS) at 3.5, the highest level macOS OpenGL 4.1 supports. Nothing in
// the program needs more than 3.5.
Shader "RealAirglow/Airglow"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma only_renderers d3d11
            #pragma multi_compile MODE_CAMERA MODE_EYE MODE_SCIENCE
            #pragma multi_compile __ EXTINCTION_ON
            #pragma multi_compile __ OUTSHINE_ON
            #pragma multi_compile __ USE_DEPTH
            #include "AirglowPass.cginc"
            ENDCG
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma exclude_renderers d3d11
            #pragma multi_compile MODE_CAMERA MODE_EYE MODE_SCIENCE
            #pragma multi_compile __ EXTINCTION_ON
            #pragma multi_compile __ OUTSHINE_ON
            #pragma multi_compile __ USE_DEPTH
            #include "AirglowPass.cginc"
            ENDCG
        }
    }
}
