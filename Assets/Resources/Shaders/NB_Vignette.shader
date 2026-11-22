// VR comfort "tunnelling" vignette. Drawn on a quad just in front of the camera (real geometry, so it works in
// each eye with any stereo rendering mode). Alpha ramps from 0 inside _Inner to 1 outside _Outer (radius in quad UV);
// _Blink fades the whole view to black for snap turns.
Shader "NeonBreach/NB_Vignette"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Inner ("Inner radius", Float) = 1.5
        _Outer ("Outer radius", Float) = 1.8
        _Strength ("Strength", Float) = 1
        _Blink ("Blink", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Inner;
            float _Outer;
            float _Strength;
            float _Blink;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = smoothstep(_Inner, _Outer, d) * _Strength;
                a = max(a, _Blink);
                return fixed4(_Color.rgb, saturate(a));
            }
            ENDCG
        }
    }
    Fallback Off
}
