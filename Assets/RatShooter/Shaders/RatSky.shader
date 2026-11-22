// Rat Shooter - painted cartoon sky: three-colour gradient, soft sun disc + glow that follows the main light.
Shader "RatShooter/Sky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.25,0.55,0.95,1)
        _HorizonColor ("Horizon", Color) = (0.78,0.9,1,1)
        _BottomColor ("Below Horizon", Color) = (0.55,0.7,0.6,1)
        _Exponent ("Gradient Exponent", Range(0.1,4)) = 0.6
        _SunColor ("Sun Colour", Color) = (1,0.95,0.75,1)
        _SunSize ("Sun Size", Range(0.0005,0.05)) = 0.006
        _SunGlow ("Sun Glow", Range(0,2)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            half _Exponent, _SunSize, _SunGlow;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 col;
                if (y >= 0) col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), _Exponent));
                else col = lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y * 3), 0.7));

                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float sd = dot(d, sunDir);
                half disc = smoothstep(1 - _SunSize, 1 - _SunSize * 0.55, sd);
                half glow = pow(saturate(sd), 48) * _SunGlow + pow(saturate(sd), 6) * 0.12 * _SunGlow;
                col += _SunColor.rgb * (glow + disc * 1.5);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
